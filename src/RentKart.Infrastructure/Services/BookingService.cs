using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class BookingService : IBookingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<BookingService> _logger;
    private readonly INotificationService? _notificationService;
    private readonly IRentalDocumentService? _rentalDocumentService;

    public BookingService(
        ApplicationDbContext dbContext, 
        ILogger<BookingService> logger,
        INotificationService? notificationService = null,
        IRentalDocumentService? rentalDocumentService = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _notificationService = notificationService;
        _rentalDocumentService = rentalDocumentService;
    }

    public async Task<BookingOperationResult> CreateBookingAsync(string customerUserId, CreateBookingRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return BookingOperationResult.Fail("Customer must be authenticated.");
        }

        if (request.Items == null || request.Items.Count == 0)
        {
            return BookingOperationResult.Fail("Booking must contain at least one item.");
        }

        var validationErrors = new List<string>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Basic item request validation
        for (int i = 0; i < request.Items.Count; i++)
        {
            var item = request.Items[i];
            if (item.Quantity <= 0)
            {
                validationErrors.Add($"Item #{i + 1}: Quantity must be greater than zero.");
            }
            if (item.StartDate < today)
            {
                validationErrors.Add($"Item #{i + 1}: Start date cannot be in the past.");
            }
            if (item.EndDate < item.StartDate)
            {
                validationErrors.Add($"Item #{i + 1}: End date cannot be before start date.");
            }
        }

        if (validationErrors.Count > 0)
        {
            return BookingOperationResult.Fail(validationErrors);
        }

        // Concurrency-safe execution:
        // Begin transaction with ReadCommitted isolation.
        // Lock equipment records in ascending ID order with UPDLOCK/HOLDLOCK to prevent deadlocks and concurrent overselling.
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        try
        {
            var orderedEquipmentIds = request.Items
                .Select(i => i.EquipmentId)
                .Distinct()
                .OrderBy(id => id)
                .ToList();

            // Acquire exclusive update lock on the equipment rows if SQL Server
            if (_dbContext.Database.IsSqlServer())
            {
                foreach (var eqId in orderedEquipmentIds)
                {
                    await _dbContext.Equipments
                        .FromSqlInterpolated($"SELECT * FROM Equipments WITH (UPDLOCK, HOLDLOCK) WHERE Id = {eqId}")
                        .AsNoTracking()
                        .ToListAsync();
                }
            }

            // Retrieve the locked equipment with their business profiles and categories
            var equipmentMap = await _dbContext.Equipments
                .Include(e => e.Business)
                .Include(e => e.Category)
                .Where(e => orderedEquipmentIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            // Validate that all equipment exists, is active, and belongs to an approved business
            foreach (var reqItem in request.Items)
            {
                if (!equipmentMap.TryGetValue(reqItem.EquipmentId, out var equipment))
                {
                    validationErrors.Add($"Equipment with ID {reqItem.EquipmentId} was not found.");
                    continue;
                }

                if (!equipment.IsActive || equipment.Status != EquipmentStatus.Active)
                {
                    validationErrors.Add($"Equipment '{equipment.Title}' is not currently available for rent.");
                    continue;
                }

                if (equipment.Business.Status != BusinessStatus.Approved)
                {
                    validationErrors.Add($"The store '{equipment.Business.ShopName}' is not approved to accept rentals.");
                    continue;
                }

                // Check real-time date-range availability within the locked transaction
                var availableQty = await CalculateAvailableQuantityInternalAsync(equipment.Id, equipment.TotalQuantity, reqItem.StartDate, reqItem.EndDate);
                if (availableQty < reqItem.Quantity)
                {
                    validationErrors.Add($"Insufficient units available for '{equipment.Title}'. Requested: {reqItem.Quantity}, Available: {Math.Max(0, availableQty)} between {reqItem.StartDate} and {reqItem.EndDate}.");
                }
            }

            if (validationErrors.Count > 0)
            {
                await transaction.RollbackAsync();
                return BookingOperationResult.Fail(validationErrors);
            }

            // Group items by BusinessId
            var itemsByBusiness = request.Items
                .GroupBy(i => equipmentMap[i.EquipmentId].BusinessId)
                .ToList();

            // Create Parent Booking
            var bookingNumber = GenerateBookingNumber();
            var parentBooking = new Booking
            {
                BookingNumber = bookingNumber,
                CustomerId = customerUserId,
                Status = BookingStatus.Pending,
                CreatedAtUtc = DateTime.UtcNow
            };

            decimal totalRentalFee = 0;
            decimal totalSecurityDeposit = 0;

            foreach (var businessGroup in itemsByBusiness)
            {
                var businessId = businessGroup.Key;
                var groupNumber = GenerateGroupNumber();
                var verificationCode = GeneratePickupVerificationCode();

                var bookingGroupEntity = new BookingGroup
                {
                    BusinessId = businessId,
                    GroupNumber = groupNumber,
                    PickupVerificationCode = verificationCode,
                    Status = BookingGroupStatus.PendingApproval,
                    CreatedAtUtc = DateTime.UtcNow
                };

                decimal groupRentalSubtotal = 0;
                decimal groupDepositSubtotal = 0;

                foreach (var itemReq in businessGroup)
                {
                    var equipment = equipmentMap[itemReq.EquipmentId];
                    int daysCount = Math.Max(1, itemReq.EndDate.DayNumber - itemReq.StartDate.DayNumber + 1);

                    decimal dailyRateSnapshot = equipment.DailyRentalRate;
                    decimal depositSnapshot = equipment.SecurityDeposit * itemReq.Quantity;
                    decimal totalPriceSnapshot = dailyRateSnapshot * itemReq.Quantity * daysCount;

                    var bookingItem = new BookingItem
                    {
                        EquipmentId = equipment.Id,
                        Quantity = itemReq.Quantity,
                        StartDate = itemReq.StartDate,
                        EndDate = itemReq.EndDate,
                        DaysCount = daysCount,
                        DailyRentalRate = dailyRateSnapshot,
                        ItemSecurityDeposit = depositSnapshot,
                        TotalItemPrice = totalPriceSnapshot
                    };

                    groupRentalSubtotal += totalPriceSnapshot;
                    groupDepositSubtotal += depositSnapshot;

                    bookingGroupEntity.BookingItems.Add(bookingItem);
                }

                bookingGroupEntity.SubtotalRentalFee = groupRentalSubtotal;
                bookingGroupEntity.SubtotalSecurityDeposit = groupDepositSubtotal;

                totalRentalFee += groupRentalSubtotal;
                totalSecurityDeposit += groupDepositSubtotal;

                parentBooking.BookingGroups.Add(bookingGroupEntity);
            }

            parentBooking.TotalRentalFee = totalRentalFee;
            parentBooking.TotalSecurityDeposit = totalSecurityDeposit;
            parentBooking.GrandTotal = totalRentalFee + totalSecurityDeposit;

            _dbContext.Bookings.Add(parentBooking);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            _logger.LogInformation("Successfully created Booking #{BookingNumber} (ID: {BookingId}) for Customer {CustomerId} across {StoreCount} stores.",
                parentBooking.BookingNumber, parentBooking.Id, customerUserId, parentBooking.BookingGroups.Count);

            // Phase 9: Automatic notification and document dispatch
            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyBookingCreatedAsync(parentBooking.Id);
                }
                catch (Exception nEx)
                {
                    _logger.LogWarning(nEx, "Failed to send notifications for booking {BookingId}.", parentBooking.Id);
                }
            }

            if (_rentalDocumentService != null)
            {
                try
                {
                    await _rentalDocumentService.GenerateParentBookingInvoiceAsync(parentBooking.Id);
                    foreach (var bg in parentBooking.BookingGroups)
                    {
                        await _rentalDocumentService.GenerateStoreInvoiceAsync(bg.Id);
                        await _rentalDocumentService.GenerateRentalAgreementAsync(bg.Id);
                    }
                }
                catch (Exception docEx)
                {
                    _logger.LogWarning(docEx, "Failed to generate initial documents for booking {BookingId}.", parentBooking.Id);
                }
            }

            return BookingOperationResult.Ok(parentBooking.Id, parentBooking.BookingNumber);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Failed to create booking for customer {CustomerId}. Transaction rolled back.", customerUserId);
            return BookingOperationResult.Fail("An unexpected error occurred while placing your booking. Please try again.");
        }
    }

    public async Task<(List<CustomerBookingSummaryDto> Items, int TotalCount)> GetCustomerBookingsAsync(string customerUserId, int page = 1, int pageSize = 10)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.Business)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .Where(b => b.CustomerId == customerUserId)
            .OrderByDescending(b => b.CreatedAtUtc);

        int totalCount = await query.CountAsync();

        var bookings = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new CustomerBookingSummaryDto
            {
                Id = b.Id,
                BookingNumber = b.BookingNumber,
                CreatedAtUtc = b.CreatedAtUtc,
                Status = b.Status,
                TotalRentalFee = b.TotalRentalFee,
                TotalSecurityDeposit = b.TotalSecurityDeposit,
                GrandTotal = b.GrandTotal,
                TotalGroupsCount = b.BookingGroups.Count,
                TotalItemsCount = b.BookingGroups.SelectMany(g => g.BookingItems).Sum(i => i.Quantity),
                Groups = b.BookingGroups.Select(g => new CustomerBookingGroupSummaryDto
                {
                    Id = g.Id,
                    GroupNumber = g.GroupNumber,
                    BusinessId = g.BusinessId,
                    BusinessName = g.Business.ShopName,
                    BusinessCity = g.Business.City,
                    PickupVerificationCode = g.PickupVerificationCode,
                    Status = g.Status,
                    SubtotalRentalFee = g.SubtotalRentalFee,
                    SubtotalSecurityDeposit = g.SubtotalSecurityDeposit,
                    ItemsCount = g.BookingItems.Count,
                    EarliestStartDate = g.BookingItems.Min(i => i.StartDate),
                    LatestEndDate = g.BookingItems.Max(i => i.EndDate)
                }).ToList()
            })
            .ToListAsync();

        return (bookings, totalCount);
    }

    public async Task<CustomerBookingDetailsDto?> GetCustomerBookingDetailsAsync(int bookingId, string customerUserId)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.Business)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
                    .ThenInclude(bi => bi.Equipment)
                        .ThenInclude(e => e.Category)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
                    .ThenInclude(bi => bi.Equipment)
                        .ThenInclude(e => e.Images)
            .FirstOrDefaultAsync(b => b.Id == bookingId && b.CustomerId == customerUserId);

        if (booking == null) return null;

        return new CustomerBookingDetailsDto
        {
            Id = booking.Id,
            BookingNumber = booking.BookingNumber,
            CreatedAtUtc = booking.CreatedAtUtc,
            Status = booking.Status,
            TotalRentalFee = booking.TotalRentalFee,
            TotalSecurityDeposit = booking.TotalSecurityDeposit,
            GrandTotal = booking.GrandTotal,
            Groups = booking.BookingGroups.Select(g => new BookingGroupDetailDto
            {
                Id = g.Id,
                ParentBookingId = g.ParentBookingId,
                GroupNumber = g.GroupNumber,
                BusinessId = g.BusinessId,
                BusinessName = g.Business.ShopName,
                BusinessAddressLine = g.Business.AddressLine,
                BusinessCity = g.Business.City,
                BusinessPostalCode = g.Business.PostalCode,
                BusinessPhone = g.Business.ContactPhone,
                OperatingHours = g.Business.OperatingHours,
                PickupVerificationCode = g.PickupVerificationCode,
                Status = g.Status,
                SubtotalRentalFee = g.SubtotalRentalFee,
                SubtotalSecurityDeposit = g.SubtotalSecurityDeposit,
                HandoverNotes = g.HandoverNotes,
                HandedOverAtUtc = g.HandedOverAtUtc,
                ReturnInspectionNotes = g.ReturnInspectionNotes,
                ReturnedAtUtc = g.ReturnedAtUtc,
                CompletedAtUtc = g.CompletedAtUtc,
                RejectionReason = g.RejectionReason,
                Items = g.BookingItems.Select(i => new BookingItemDetailDto
                {
                    Id = i.Id,
                    EquipmentId = i.EquipmentId,
                    EquipmentTitle = i.Equipment.Title,
                    PrimaryImageUrl = i.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                        ?? i.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl,
                    CategoryName = i.Equipment.Category.Name,
                    Quantity = i.Quantity,
                    StartDate = i.StartDate,
                    EndDate = i.EndDate,
                    DaysCount = i.DaysCount,
                    DailyRentalRate = i.DailyRentalRate,
                    ItemSecurityDeposit = i.ItemSecurityDeposit,
                    TotalItemPrice = i.TotalItemPrice
                }).ToList()
            }).ToList()
        };
    }

    public async Task<BookingGroupOperationResult> CancelBookingGroupByCustomerAsync(int bookingGroupId, string customerUserId, string? reason)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.BookingGroups)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null || group.ParentBooking.CustomerId != customerUserId)
        {
            return BookingGroupOperationResult.Fail("Booking group not found or access denied.");
        }

        if (group.Status != BookingGroupStatus.PendingApproval && group.Status != BookingGroupStatus.Confirmed)
        {
            return BookingGroupOperationResult.Fail($"Cannot cancel a reservation with status '{group.Status}'. Equipment has already been handed over or finalized.");
        }

        group.Status = BookingGroupStatus.CancelledByCustomer;
        group.RejectionReason = string.IsNullOrWhiteSpace(reason) ? "Cancelled by customer." : $"Cancelled by customer: {reason.Trim()}";

        UpdateParentBookingStatus(group.ParentBooking);

        await _dbContext.SaveChangesAsync();

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyBookingGroupStatusChangedAsync(group.Id, BookingGroupStatus.CancelledByCustomer, group.RejectionReason);
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to send cancellation notification for group {GroupId}.", group.Id);
            }
        }

        _logger.LogInformation("Customer {CustomerId} cancelled booking group {GroupNumber}.", customerUserId, group.GroupNumber);
        return BookingGroupOperationResult.Ok(group.Status);
    }

    public async Task<(List<BusinessBookingGroupListItemDto> Items, int TotalCount)> GetBusinessBookingGroupsAsync(int businessId, BookingGroupStatus? statusFilter = null, int page = 1, int pageSize = 15)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);

        var query = _dbContext.BookingGroups
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.Customer)
            .Include(bg => bg.BookingItems)
            .Where(bg => bg.BusinessId == businessId);

        if (statusFilter.HasValue)
        {
            query = query.Where(bg => bg.Status == statusFilter.Value);
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(bg => bg.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(bg => new BusinessBookingGroupListItemDto
            {
                Id = bg.Id,
                ParentBookingId = bg.ParentBookingId,
                GroupNumber = bg.GroupNumber,
                CustomerName = bg.ParentBooking.Customer.FullName,
                CustomerEmail = bg.ParentBooking.Customer.Email ?? string.Empty,
                CustomerPhone = bg.ParentBooking.Customer.PhoneNumber ?? string.Empty,
                Status = bg.Status,
                SubtotalRentalFee = bg.SubtotalRentalFee,
                SubtotalSecurityDeposit = bg.SubtotalSecurityDeposit,
                ItemsCount = bg.BookingItems.Count,
                EarliestStartDate = bg.BookingItems.Min(i => i.StartDate),
                LatestEndDate = bg.BookingItems.Max(i => i.EndDate),
                CreatedAtUtc = bg.CreatedAtUtc
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<BookingGroupDetailDto?> GetBusinessBookingGroupDetailsAsync(int bookingGroupId, int businessId)
    {
        var g = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
                    .ThenInclude(e => e.Category)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
                    .ThenInclude(e => e.Images)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId && bg.BusinessId == businessId);

        if (g == null) return null;

        return new BookingGroupDetailDto
        {
            Id = g.Id,
            ParentBookingId = g.ParentBookingId,
            GroupNumber = g.GroupNumber,
            BusinessId = g.BusinessId,
            BusinessName = g.Business.ShopName,
            BusinessAddressLine = g.Business.AddressLine,
            BusinessCity = g.Business.City,
            BusinessPostalCode = g.Business.PostalCode,
            BusinessPhone = g.Business.ContactPhone,
            OperatingHours = g.Business.OperatingHours,
            PickupVerificationCode = g.PickupVerificationCode,
            Status = g.Status,
            SubtotalRentalFee = g.SubtotalRentalFee,
            SubtotalSecurityDeposit = g.SubtotalSecurityDeposit,
            HandoverNotes = g.HandoverNotes,
            HandedOverAtUtc = g.HandedOverAtUtc,
            ReturnInspectionNotes = g.ReturnInspectionNotes,
            ReturnedAtUtc = g.ReturnedAtUtc,
            CompletedAtUtc = g.CompletedAtUtc,
            RejectionReason = g.RejectionReason,
            Items = g.BookingItems.Select(i => new BookingItemDetailDto
            {
                Id = i.Id,
                EquipmentId = i.EquipmentId,
                EquipmentTitle = i.Equipment.Title,
                PrimaryImageUrl = i.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                    ?? i.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl,
                CategoryName = i.Equipment.Category.Name,
                Quantity = i.Quantity,
                StartDate = i.StartDate,
                EndDate = i.EndDate,
                DaysCount = i.DaysCount,
                DailyRentalRate = i.DailyRentalRate,
                ItemSecurityDeposit = i.ItemSecurityDeposit,
                TotalItemPrice = i.TotalItemPrice
            }).ToList()
        };
    }

    public async Task<BookingGroupOperationResult> AcceptBookingGroupAsync(int bookingGroupId, int businessId)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.BookingGroups)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null || group.BusinessId != businessId)
        {
            return BookingGroupOperationResult.Fail("Booking group not found or access denied.");
        }

        if (group.Business.Status != BusinessStatus.Approved)
        {
            return BookingGroupOperationResult.Fail("Your business account is not approved to accept bookings.");
        }

        if (group.Status != BookingGroupStatus.PendingApproval)
        {
            return BookingGroupOperationResult.Fail($"Cannot accept a reservation with status '{group.Status}'. Only pending groups can be accepted.");
        }

        group.Status = BookingGroupStatus.Confirmed;
        UpdateParentBookingStatus(group.ParentBooking);

        await _dbContext.SaveChangesAsync();

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyBookingGroupStatusChangedAsync(group.Id, BookingGroupStatus.Confirmed);
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to send confirmation notification for group {GroupId}.", group.Id);
            }
        }

        if (_rentalDocumentService != null)
        {
            try
            {
                // Ensure agreement is regenerated/current upon store confirmation
                await _rentalDocumentService.GenerateRentalAgreementAsync(group.Id);
            }
            catch (Exception docEx)
            {
                _logger.LogWarning(docEx, "Failed to generate rental agreement on confirmation for group {GroupId}.", group.Id);
            }
        }

        _logger.LogInformation("Business {BusinessId} accepted booking group {GroupNumber}.", businessId, group.GroupNumber);
        return BookingGroupOperationResult.Ok(group.Status);
    }

    public async Task<BookingGroupOperationResult> RejectBookingGroupAsync(int bookingGroupId, int businessId, string? rejectionReason)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.BookingGroups)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null || group.BusinessId != businessId)
        {
            return BookingGroupOperationResult.Fail("Booking group not found or access denied.");
        }

        if (group.Status != BookingGroupStatus.PendingApproval)
        {
            return BookingGroupOperationResult.Fail($"Cannot reject a reservation with status '{group.Status}'. Only pending groups can be rejected.");
        }

        group.Status = BookingGroupStatus.Rejected;
        group.RejectionReason = string.IsNullOrWhiteSpace(rejectionReason) ? "Declined by store." : rejectionReason.Trim();

        UpdateParentBookingStatus(group.ParentBooking);

        await _dbContext.SaveChangesAsync();

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyBookingGroupStatusChangedAsync(group.Id, BookingGroupStatus.Rejected, group.RejectionReason);
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to send rejection notification for group {GroupId}.", group.Id);
            }
        }

        _logger.LogInformation("Business {BusinessId} rejected booking group {GroupNumber}. Reason: {Reason}", businessId, group.GroupNumber, group.RejectionReason);
        return BookingGroupOperationResult.Ok(group.Status);
    }

    public Task<BookingGroupOperationResult> HandoverBookingGroupAsync(int bookingGroupId, int businessId, string verificationCode, string? handoverNotes)
    {
        return HandoverBookingGroupDetailedAsync(bookingGroupId, businessId, new HandoverBookingGroupRequestDto
        {
            VerificationCode = verificationCode,
            GeneralHandoverNotes = handoverNotes
        });
    }

    public async Task<BookingGroupOperationResult> HandoverBookingGroupDetailedAsync(int bookingGroupId, int businessId, HandoverBookingGroupRequestDto request)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.BookingGroups)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null || group.BusinessId != businessId)
        {
            return BookingGroupOperationResult.Fail("Booking group not found or access denied.");
        }

        if (group.Business.Status != BusinessStatus.Approved)
        {
            return BookingGroupOperationResult.Fail("Your business account is not approved to perform handovers.");
        }

        if (group.Status != BookingGroupStatus.Confirmed)
        {
            return BookingGroupOperationResult.Fail($"Cannot handover equipment for group with status '{group.Status}'. Reservation must be Confirmed.");
        }

        if (string.IsNullOrWhiteSpace(request.VerificationCode) ||
            !string.Equals(group.PickupVerificationCode.Trim(), request.VerificationCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BookingGroupOperationResult.Fail("Invalid pickup verification code. Please check the customer's voucher code.");
        }

        var checklistSummary = new List<string>();
        if (request.Items != null && request.Items.Count > 0)
        {
            foreach (var bi in group.BookingItems)
            {
                var check = request.Items.FirstOrDefault(c => c.BookingItemId == bi.Id);
                if (check != null)
                {
                    if (check.QuantityHandedOver < 0 || check.QuantityHandedOver > bi.Quantity)
                    {
                        return BookingGroupOperationResult.Fail($"Invalid handover quantity for item '{bi.Equipment.Title}'. Handed over quantity must be between 0 and requested {bi.Quantity}.");
                    }

                    string itemNotes = string.IsNullOrWhiteSpace(check.ConditionNotes) ? "Good condition" : check.ConditionNotes.Trim();
                    checklistSummary.Add($"[Item #{bi.Id} {bi.Equipment.Title}] Qty: {check.QuantityHandedOver}/{bi.Quantity}. Condition: {itemNotes}. Accessories: {(check.AccessoriesConfirmed ? "Verified" : "Unverified")}");
                }
                else
                {
                    checklistSummary.Add($"[Item #{bi.Id} {bi.Equipment.Title}] Qty: {bi.Quantity}/{bi.Quantity}. Condition: Good condition. Accessories: Verified");
                }
            }
        }
        else
        {
            foreach (var bi in group.BookingItems)
            {
                checklistSummary.Add($"[Item #{bi.Id} {bi.Equipment.Title}] Qty: {bi.Quantity}/{bi.Quantity}. Condition: Good condition. Accessories: Verified");
            }
        }

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.GeneralHandoverNotes))
        {
            sb.AppendLine(request.GeneralHandoverNotes.Trim());
            sb.AppendLine();
        }
        sb.AppendLine("Handover Checklist:");
        foreach (var line in checklistSummary)
        {
            sb.AppendLine("- " + line);
        }

        group.Status = BookingGroupStatus.HandedOver;
        group.HandedOverAtUtc = DateTime.UtcNow;
        group.HandoverNotes = sb.ToString().Trim();

        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "EquipmentHandover",
            EntityType = nameof(BookingGroup),
            EntityId = group.Id.ToString(),
            Details = $"Handover recorded for Group {group.GroupNumber}. Notes: {group.HandoverNotes}",
            TimestampUtc = DateTime.UtcNow
        });

        UpdateParentBookingStatus(group.ParentBooking);

        await _dbContext.SaveChangesAsync();

        if (_notificationService != null)
        {
            try
            {
                await _notificationService.NotifyBookingGroupStatusChangedAsync(group.Id, BookingGroupStatus.HandedOver);
            }
            catch (Exception nEx)
            {
                _logger.LogWarning(nEx, "Failed to send handover notification for group {GroupId}.", group.Id);
            }
        }

        _logger.LogInformation("Business {BusinessId} completed handover for booking group {GroupNumber}.", businessId, group.GroupNumber);
        return BookingGroupOperationResult.Ok(group.Status);
    }

    public async Task<BookingGroupOperationResult> CompleteReturnBookingGroupAsync(int bookingGroupId, int businessId, string? inspectionNotes)
    {
        var result = await CompleteReturnWithInspectionAsync(bookingGroupId, businessId, new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = inspectionNotes
        });

        if (!result.Success)
        {
            return BookingGroupOperationResult.Fail(result.ErrorMessage ?? "Could not complete return.");
        }

        return BookingGroupOperationResult.Ok(result.NewStatus);
    }

    public async Task<ReturnInspectionResultDto> CompleteReturnWithInspectionAsync(int bookingGroupId, int businessId, CompleteReturnBookingGroupRequestDto request)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.BookingItems)
                .ThenInclude(bi => bi.Equipment)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.BookingGroups)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null || group.BusinessId != businessId)
        {
            return ReturnInspectionResultDto.Fail("Booking group not found or access denied.");
        }

        if (group.Status != BookingGroupStatus.HandedOver)
        {
            return ReturnInspectionResultDto.Fail($"Cannot complete return for group with status '{group.Status}'. Equipment must currently be Handed Over.");
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            decimal calculatedDamageFee = 0;
            var maintenanceRecordEntities = new List<MaintenanceRecord>();
            var inspectionSummaryLines = new List<string>();

            foreach (var item in group.BookingItems)
            {
                var insp = request.Items?.FirstOrDefault(i => i.BookingItemId == item.Id);
                int returnedQty = insp != null ? insp.ReturnedQuantity : item.Quantity;
                if (returnedQty < 0) returnedQty = 0;
                if (returnedQty > item.Quantity) returnedQty = item.Quantity;

                int missingQty = insp != null && insp.MissingQuantity >= 0
                    ? insp.MissingQuantity
                    : Math.Max(0, item.Quantity - returnedQty);

                string condition = insp?.Condition ?? "Good";
                bool isDamaged = insp?.HasDamage == true || string.Equals(condition, "Damaged", StringComparison.OrdinalIgnoreCase);
                decimal repairCost = insp?.EstimatedRepairCost ?? 0;
                if (repairCost < 0) repairCost = 0;

                calculatedDamageFee += repairCost;

                int damagedQty = isDamaged ? returnedQty : 0;
                int blockedQty = damagedQty + missingQty;

                if (blockedQty > 0 && (insp == null || insp.CreateMaintenanceBlock))
                {
                    int maintenanceDays = (insp?.EstimatedMaintenanceDays.HasValue == true && insp.EstimatedMaintenanceDays.Value > 0)
                        ? insp.EstimatedMaintenanceDays.Value
                        : 7;

                    var mRecord = new MaintenanceRecord
                    {
                        EquipmentId = item.EquipmentId,
                        BlockedQuantity = blockedQty,
                        StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
                        EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(maintenanceDays)),
                        Reason = $"Post-rental inspection damage/missing from Booking Group {group.GroupNumber}: {(string.IsNullOrWhiteSpace(insp?.DamageDescription) ? condition : insp.DamageDescription.Trim())}",
                        EstimatedCost = repairCost > 0 ? repairCost : null,
                        IsResolved = false,
                        CreatedAtUtc = DateTime.UtcNow
                    };

                    _dbContext.MaintenanceRecords.Add(mRecord);
                    maintenanceRecordEntities.Add(mRecord);
                }

                var lineSb = new StringBuilder();
                lineSb.Append($"[Item #{item.Id} {item.Equipment.Title}] Returned: {returnedQty}/{item.Quantity}. Condition: {condition}.");
                if (missingQty > 0)
                {
                    lineSb.Append($" Missing: {missingQty}.");
                }
                if (isDamaged)
                {
                    lineSb.Append($" Damage: {(string.IsNullOrWhiteSpace(insp?.DamageDescription) ? "Reported" : insp.DamageDescription.Trim())}.");
                }
                if (repairCost > 0)
                {
                    lineSb.Append($" Repair Cost: ${repairCost:F2}.");
                }
                if (!string.IsNullOrWhiteSpace(insp?.PhotoEvidenceUrl))
                {
                    lineSb.Append($" Evidence: {insp.PhotoEvidenceUrl.Trim()}.");
                }

                inspectionSummaryLines.Add(lineSb.ToString());
            }

            decimal finalDamageFee = request.AssessedDamageFee.HasValue && request.AssessedDamageFee.Value >= 0
                ? request.AssessedDamageFee.Value
                : calculatedDamageFee;

            decimal totalDepositHeld = group.SubtotalSecurityDeposit;
            decimal forfeitedDeposit = Math.Min(totalDepositHeld, finalDamageFee);
            decimal refundAmount = Math.Max(0, totalDepositHeld - forfeitedDeposit);

            var reportSb = new StringBuilder();
            if (!string.IsNullOrWhiteSpace(request.GeneralInspectionNotes))
            {
                reportSb.AppendLine(request.GeneralInspectionNotes.Trim());
                reportSb.AppendLine();
            }
            reportSb.AppendLine("Inspection Findings:");
            foreach (var line in inspectionSummaryLines)
            {
                reportSb.AppendLine("- " + line);
            }
            reportSb.AppendLine();
            reportSb.AppendLine("Financial Assessment:");
            reportSb.AppendLine($"- Security Deposit Held: ${totalDepositHeld:F2}");
            reportSb.AppendLine($"- Damage / Missing Fee Assessed: ${finalDamageFee:F2}");
            reportSb.AppendLine($"- Deposit Forfeited / Deducted: ${forfeitedDeposit:F2}");
            reportSb.AppendLine($"- Net Deposit Refund: ${refundAmount:F2}");

            if (maintenanceRecordEntities.Count > 0)
            {
                reportSb.AppendLine($"- Maintenance Quarantine: {maintenanceRecordEntities.Sum(m => m.BlockedQuantity)} unit(s) placed into repair downtime.");
            }

            group.Status = BookingGroupStatus.Completed;
            group.ReturnedAtUtc = DateTime.UtcNow;
            group.CompletedAtUtc = DateTime.UtcNow;
            group.ReturnInspectionNotes = reportSb.ToString().Trim();

            // Save changes to persist maintenance records and generate their primary keys
            await _dbContext.SaveChangesAsync();

            var createdRecordIds = maintenanceRecordEntities.Select(m => m.Id).ToList();

            _dbContext.AuditLogs.Add(new AuditLog
            {
                Action = "ReturnInspectionCompleted",
                EntityType = nameof(BookingGroup),
                EntityId = group.Id.ToString(),
                Details = $"Return inspection completed for Group {group.GroupNumber}. Forfeited Deposit: ${forfeitedDeposit:F2}, Refund: ${refundAmount:F2}, Maintenance Blocks: {createdRecordIds.Count}.",
                TimestampUtc = DateTime.UtcNow
            });

            UpdateParentBookingStatus(group.ParentBooking);
            await _dbContext.SaveChangesAsync();

            await transaction.CommitAsync();

            if (_notificationService != null)
            {
                try
                {
                    await _notificationService.NotifyBookingGroupStatusChangedAsync(group.Id, BookingGroupStatus.Completed);
                }
                catch (Exception nEx)
                {
                    _logger.LogWarning(nEx, "Failed to send completion notification for group {GroupId}.", group.Id);
                }
            }

            _logger.LogInformation("Business {BusinessId} completed return & inspection for booking group {GroupNumber}. Net refund: ${Refund:F2}",
                businessId, group.GroupNumber, refundAmount);

            return ReturnInspectionResultDto.Ok(
                group.Status,
                totalDepositHeld,
                finalDamageFee,
                refundAmount,
                forfeitedDeposit,
                createdRecordIds,
                inspectionSummaryLines);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed while inspecting and completing booking group {GroupId}.", bookingGroupId);
            return ReturnInspectionResultDto.Fail("An error occurred while saving the return inspection: " + ex.Message);
        }
    }

    public async Task<int> GetAuthoritativeAvailableQuantityAsync(int equipmentId, DateOnly startDate, DateOnly endDate)
    {
        var equipment = await _dbContext.Equipments.FirstOrDefaultAsync(e => e.Id == equipmentId);
        if (equipment == null) return 0;

        return await CalculateAvailableQuantityInternalAsync(equipmentId, equipment.TotalQuantity, startDate, endDate);
    }

    private async Task<int> CalculateAvailableQuantityInternalAsync(int equipmentId, int totalQuantity, DateOnly startDate, DateOnly endDate)
    {
        // Query overlapping active reservations:
        // Statuses holding inventory: PendingApproval, Confirmed, HandedOver
        var overlappingBookings = await _dbContext.BookingItems
            .Include(bi => bi.BookingGroup)
            .Where(bi => bi.EquipmentId == equipmentId &&
                         bi.StartDate <= endDate &&
                         bi.EndDate >= startDate &&
                         (bi.BookingGroup.Status == BookingGroupStatus.PendingApproval ||
                          bi.BookingGroup.Status == BookingGroupStatus.Confirmed ||
                          bi.BookingGroup.Status == BookingGroupStatus.HandedOver))
            .Select(bi => new { bi.StartDate, bi.EndDate, bi.Quantity })
            .ToListAsync();

        // Query overlapping unresolved maintenance records
        var overlappingMaintenance = await _dbContext.MaintenanceRecords
            .Where(mr => mr.EquipmentId == equipmentId &&
                         !mr.IsResolved &&
                         mr.StartDate <= endDate &&
                         mr.EndDate >= startDate)
            .Select(mr => new { mr.StartDate, mr.EndDate, mr.BlockedQuantity })
            .ToListAsync();

        // Calculate peak usage on any individual day in [startDate, endDate]
        int minAvailable = totalQuantity;

        for (var day = startDate; day <= endDate; day = day.AddDays(1))
        {
            int dayReserved = overlappingBookings
                .Where(b => b.StartDate <= day && b.EndDate >= day)
                .Sum(b => b.Quantity);

            int dayMaintenance = overlappingMaintenance
                .Where(m => m.StartDate <= day && m.EndDate >= day)
                .Sum(m => m.BlockedQuantity);

            int dayAvailable = totalQuantity - dayReserved - dayMaintenance;
            if (dayAvailable < minAvailable)
            {
                minAvailable = dayAvailable;
            }
        }

        return Math.Max(0, minAvailable);
    }

    private static void UpdateParentBookingStatus(Booking parent)
    {
        var groups = parent.BookingGroups.ToList();
        if (groups.Count == 0) return;

        // Terminal cancelled state: all groups are cancelled or rejected
        if (groups.All(g => g.Status == BookingGroupStatus.CancelledByCustomer || g.Status == BookingGroupStatus.Rejected))
        {
            parent.Status = BookingStatus.Cancelled;
            parent.UpdatedAtUtc = DateTime.UtcNow;
            return;
        }

        // Active groups that were not cancelled or rejected
        var applicableGroups = groups.Where(g => g.Status != BookingGroupStatus.CancelledByCustomer && g.Status != BookingGroupStatus.Rejected).ToList();

        // Terminal completed state: every applicable accepted subgroup has reached Completed
        if (applicableGroups.Count > 0 && applicableGroups.All(g => g.Status == BookingGroupStatus.Completed))
        {
            parent.Status = BookingStatus.Completed;
        }
        // Active state: at least one applicable group is HandedOver
        else if (applicableGroups.Any(g => g.Status == BookingGroupStatus.HandedOver))
        {
            parent.Status = BookingStatus.Active;
        }
        // Fully confirmed state: all groups in booking are confirmed
        else if (groups.All(g => g.Status == BookingGroupStatus.Confirmed))
        {
            parent.Status = BookingStatus.Confirmed;
        }
        // Partially confirmed: at least one is Confirmed, HandedOver, or Completed
        else if (applicableGroups.Any(g => g.Status == BookingGroupStatus.Confirmed || g.Status == BookingGroupStatus.HandedOver || g.Status == BookingGroupStatus.Completed))
        {
            parent.Status = BookingStatus.PartiallyConfirmed;
        }
        // Otherwise, still pending
        else
        {
            parent.Status = BookingStatus.Pending;
        }

        parent.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string GenerateBookingNumber()
    {
        int rand = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"BK-{DateTime.UtcNow:yyyyMMdd}-{rand}";
    }

    private static string GenerateGroupNumber()
    {
        int rand = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"GRP-{DateTime.UtcNow:yyyyMMdd}-{rand}";
    }

    private static string GeneratePickupVerificationCode()
    {
        int rand = RandomNumberGenerator.GetInt32(100000, 999999);
        return $"RK-{rand}";
    }
}
