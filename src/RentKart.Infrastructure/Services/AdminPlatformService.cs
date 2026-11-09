using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class AdminPlatformService : IAdminPlatformService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AdminPlatformService> _logger;

    public AdminPlatformService(ApplicationDbContext context, ILogger<AdminPlatformService> logger)
    {
        _context = context;
        _logger = logger;
    }

    private async Task<string?> ResolveValidUserIdAsync(string? userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;
        return await _context.Users.AnyAsync(u => u.Id == userId) ? userId : null;
    }

    public async Task<AdminDashboardMetricsDto> GetDashboardMetricsAsync()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalCustomers = await _context.Users.CountAsync(u => u.UserType == UserRole.Customer);
        var totalBusinesses = await _context.BusinessProfiles.CountAsync();
        var pendingBusinesses = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.PendingApproval);
        var approvedBusinesses = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.Approved);
        var suspendedBusinesses = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.Suspended);
        var rejectedBusinesses = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.Rejected);

        var totalEquipment = await _context.Equipments.CountAsync();
        var activeEquipment = await _context.Equipments.CountAsync(e => e.IsActive && e.Status == EquipmentStatus.Active);
        var inactiveEquipment = await _context.Equipments.CountAsync(e => !e.IsActive || e.Status == EquipmentStatus.Inactive);
        var maintenanceEquipment = await _context.MaintenanceRecords.Where(m => !m.IsResolved).SumAsync(m => (int?)m.BlockedQuantity) ?? 0;

        var totalBookings = await _context.Bookings.CountAsync();
        var pendingBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Pending);
        var confirmedBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Confirmed);
        var activeBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Active);
        var completedBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Completed);
        var cancelledBookings = await _context.Bookings.CountAsync(b => b.Status == BookingStatus.Cancelled);

        var nonCancelledBookings = _context.Bookings.Where(b => b.Status != BookingStatus.Cancelled);
        var totalRentalFee = await nonCancelledBookings.AnyAsync() 
            ? await nonCancelledBookings.SumAsync(b => b.TotalRentalFee) 
            : 0m;
        var totalSecurityDeposit = await nonCancelledBookings.AnyAsync() 
            ? await nonCancelledBookings.SumAsync(b => b.TotalSecurityDeposit) 
            : 0m;
        var totalTurnover = await nonCancelledBookings.AnyAsync() 
            ? await nonCancelledBookings.SumAsync(b => b.GrandTotal) 
            : 0m;

        var totalReviews = await _context.Reviews.CountAsync();
        var visibleReviews = await _context.Reviews.CountAsync(r => !r.IsHidden);
        var hiddenReviews = await _context.Reviews.CountAsync(r => r.IsHidden);
        var averageRating = visibleReviews > 0
            ? Math.Round(await _context.Reviews.Where(r => !r.IsHidden).AverageAsync(r => r.Rating), 1)
            : 0.0;

        var activeDamageReports = await _context.MaintenanceRecords.CountAsync(m => !m.IsResolved);

        return new AdminDashboardMetricsDto
        {
            TotalUsersCount = totalUsers,
            TotalCustomersCount = totalCustomers,
            TotalBusinessesCount = totalBusinesses,
            PendingBusinessesCount = pendingBusinesses,
            ApprovedBusinessesCount = approvedBusinesses,
            SuspendedBusinessesCount = suspendedBusinesses,
            RejectedBusinessesCount = rejectedBusinesses,

            TotalEquipmentCount = totalEquipment,
            ActiveEquipmentCount = activeEquipment,
            InactiveEquipmentCount = inactiveEquipment,
            MaintenanceEquipmentCount = maintenanceEquipment,

            TotalBookingsCount = totalBookings,
            PendingBookingsCount = pendingBookings,
            ConfirmedBookingsCount = confirmedBookings,
            ActiveBookingsCount = activeBookings,
            CompletedBookingsCount = completedBookings,
            CancelledBookingsCount = cancelledBookings,

            TotalRentalFeeVolume = totalRentalFee,
            TotalSecurityDepositVolume = totalSecurityDeposit,
            TotalPlatformTurnover = totalTurnover,

            TotalReviewsCount = totalReviews,
            VisibleReviewsCount = visibleReviews,
            HiddenReviewsCount = hiddenReviews,
            AverageRating = averageRating,

            ActiveDamageReportsCount = activeDamageReports
        };
    }

    public async Task<(List<AdminCustomerListItemDto> Items, int TotalCount)> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 20)
    {
        var query = _context.Users.Where(u => u.UserType == UserRole.Customer).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(u => u.FullName.Contains(s) || (u.Email != null && u.Email.Contains(s)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(s)));
        }

        var totalCount = await query.CountAsync();
        var users = await query
            .OrderByDescending(u => u.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new AdminCustomerListItemDto
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                PhoneNumber = u.PhoneNumber ?? string.Empty,
                CreatedAtUtc = u.CreatedAtUtc,
                IsLockedOut = u.LockoutEnd.HasValue && u.LockoutEnd.Value > DateTimeOffset.UtcNow,
                TotalBookingsCount = _context.Bookings.Count(b => b.CustomerId == u.Id),
                TotalReviewsCount = _context.Reviews.Count(r => r.CustomerId == u.Id)
            })
            .ToListAsync();

        return (users, totalCount);
    }

    public async Task<AdminCustomerDetailDto?> GetCustomerDetailsAsync(string customerId)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == customerId && u.UserType == UserRole.Customer);
        if (user == null) return null;

        var bookings = await _context.Bookings
            .Include(b => b.BookingGroups)
            .Where(b => b.CustomerId == customerId)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new AdminCustomerBookingSummaryDto
            {
                Id = b.Id,
                BookingNumber = b.BookingNumber,
                CreatedAtUtc = b.CreatedAtUtc,
                Status = b.Status,
                GrandTotal = b.GrandTotal,
                GroupsCount = b.BookingGroups.Count
            })
            .ToListAsync();

        var reviews = await _context.Reviews
            .Include(r => r.Equipment)
            .Where(r => r.CustomerId == customerId)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new AdminCustomerReviewSummaryDto
            {
                Id = r.Id,
                EquipmentId = r.EquipmentId,
                EquipmentTitle = r.Equipment.Title,
                Rating = r.Rating,
                Comment = r.Comment,
                IsHidden = r.IsHidden,
                CreatedAtUtc = r.CreatedAtUtc
            })
            .ToListAsync();

        return new AdminCustomerDetailDto
        {
            Id = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty,
            CreatedAtUtc = user.CreatedAtUtc,
            IsLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow,
            LockoutEnd = user.LockoutEnd,
            Bookings = bookings,
            Reviews = reviews
        };
    }

    public async Task<bool> ToggleCustomerLockoutAsync(string customerId, string adminUserId, string? reason = null, string? ipAddress = null)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == customerId);
        if (user == null) return false;

        var isCurrentlyLocked = user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTimeOffset.UtcNow;
        if (isCurrentlyLocked)
        {
            user.LockoutEnd = null;
        }
        else
        {
            user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(50);
        }

        var actionName = isCurrentlyLocked ? "UnlockCustomer" : "LockCustomer";
        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = actionName,
            EntityType = nameof(ApplicationUser),
            EntityId = customerId,
            Details = $"{{\"Action\": \"{actionName}\", \"Reason\": \"{reason ?? string.Empty}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Customer {CustomerId} lockout status updated ({Action}) by Admin {AdminUserId}", customerId, actionName, adminUserId);
        return true;
    }

    public async Task<List<AdminCategoryDto>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Include(c => c.ParentCategory)
            .Include(c => c.Equipments)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new AdminCategoryDto
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                Description = c.Description,
                IconClass = c.IconClass,
                DisplayOrder = c.DisplayOrder,
                IsActive = c.IsActive,
                ParentCategoryId = c.ParentCategoryId,
                ParentCategoryName = c.ParentCategory != null ? c.ParentCategory.Name : null,
                EquipmentCount = c.Equipments.Count
            })
            .ToListAsync();
    }

    public async Task<Category?> GetCategoryByIdAsync(int id)
    {
        return await _context.Categories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<(bool Success, string? ErrorMessage, int? CategoryId)> CreateCategoryAsync(AdminCreateCategoryDto dto, string adminUserId, string? ipAddress = null)
    {
        var slug = dto.Slug.Trim().ToLowerInvariant();
        if (await _context.Categories.AnyAsync(c => c.Slug == slug))
        {
            return (false, $"Category slug '{slug}' is already in use.", null);
        }

        var category = new Category
        {
            Name = dto.Name.Trim(),
            Slug = slug,
            Description = dto.Description?.Trim(),
            IconClass = dto.IconClass?.Trim(),
            DisplayOrder = dto.DisplayOrder,
            IsActive = dto.IsActive,
            ParentCategoryId = dto.ParentCategoryId
        };

        _context.Categories.Add(category);

        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "CreateCategory",
            EntityType = nameof(Category),
            EntityId = category.Slug,
            Details = $"{{\"Name\": \"{category.Name}\", \"Slug\": \"{category.Slug}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null, category.Id);
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateCategoryAsync(int id, AdminEditCategoryDto dto, string adminUserId, string? ipAddress = null)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return (false, "Category not found.");

        var slug = dto.Slug.Trim().ToLowerInvariant();
        if (await _context.Categories.AnyAsync(c => c.Slug == slug && c.Id != id))
        {
            return (false, $"Category slug '{slug}' is already in use by another category.");
        }

        category.Name = dto.Name.Trim();
        category.Slug = slug;
        category.Description = dto.Description?.Trim();
        category.IconClass = dto.IconClass?.Trim();
        category.DisplayOrder = dto.DisplayOrder;
        category.IsActive = dto.IsActive;
        category.ParentCategoryId = dto.ParentCategoryId;

        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "UpdateCategory",
            EntityType = nameof(Category),
            EntityId = id.ToString(),
            Details = $"{{\"Name\": \"{category.Name}\", \"Slug\": \"{category.Slug}\", \"IsActive\": {category.IsActive.ToString().ToLowerInvariant()}, \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> DeleteCategoryAsync(int id, string adminUserId, string? ipAddress = null)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null) return (false, "Category not found.");

        var equipmentCount = await _context.Equipments.CountAsync(e => e.CategoryId == id);
        if (equipmentCount > 0)
        {
            return (false, $"Cannot delete category '{category.Name}' because {equipmentCount} equipment listing(s) are assigned to it. Deactivate the category or reassign the listings first.");
        }

        var subcategoryCount = await _context.Categories.CountAsync(c => c.ParentCategoryId == id);
        if (subcategoryCount > 0)
        {
            return (false, $"Cannot delete category '{category.Name}' because {subcategoryCount} subcategory/subcategories are linked to it.");
        }

        _context.Categories.Remove(category);

        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "DeleteCategory",
            EntityType = nameof(Category),
            EntityId = id.ToString(),
            Details = $"{{\"Name\": \"{category.Name}\", \"Slug\": \"{category.Slug}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(List<AdminEquipmentListItemDto> Items, int TotalCount)> GetEquipmentListingsAsync(string? search = null, int? categoryId = null, bool? isActiveFilter = null, int page = 1, int pageSize = 20)
    {
        var query = _context.Equipments
            .Include(e => e.Business)
            .Include(e => e.Category)
            .Include(e => e.Images)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(e => e.Title.Contains(s) || e.Business.ShopName.Contains(s) || e.Category.Name.Contains(s));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(e => e.CategoryId == categoryId.Value);
        }

        if (isActiveFilter.HasValue)
        {
            query = query.Where(e => e.IsActive == isActiveFilter.Value);
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new AdminEquipmentListItemDto
            {
                Id = e.Id,
                Title = e.Title,
                BusinessId = e.BusinessId,
                ShopName = e.Business.ShopName,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                DailyRentalRate = e.DailyRentalRate,
                SecurityDeposit = e.SecurityDeposit,
                TotalQuantity = e.TotalQuantity,
                Condition = e.Condition,
                Status = e.Status,
                IsActive = e.IsActive,
                CreatedAtUtc = e.CreatedAtUtc,
                PrimaryImageUrl = e.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary) != null
                    ? e.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary)!.ImageUrl
                    : e.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault() != null
                        ? e.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()!.ImageUrl
                        : null
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(bool Success, string? ErrorMessage)> ToggleEquipmentStatusAsync(int equipmentId, bool isActive, string? reason, string adminUserId, string? ipAddress = null)
    {
        var equipment = await _context.Equipments.FindAsync(equipmentId);
        if (equipment == null) return (false, "Equipment listing not found.");

        equipment.IsActive = isActive;
        if (!isActive)
        {
            equipment.Status = EquipmentStatus.Inactive;
        }
        else if (equipment.Status == EquipmentStatus.Inactive)
        {
            equipment.Status = EquipmentStatus.Active;
        }

        var actionName = isActive ? "ActivateEquipmentListing" : "SuspendEquipmentListing";
        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = actionName,
            EntityType = nameof(Equipment),
            EntityId = equipmentId.ToString(),
            Details = $"{{\"IsActive\": {isActive.ToString().ToLowerInvariant()}, \"Reason\": \"{reason ?? string.Empty}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(List<AdminBookingListItemDto> Items, int TotalCount)> GetBookingsAsync(BookingStatus? statusFilter = null, string? search = null, int page = 1, int pageSize = 20)
    {
        var query = _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingGroups)
            .AsQueryable();

        if (statusFilter.HasValue)
        {
            query = query.Where(b => b.Status == statusFilter.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(b => b.BookingNumber.Contains(s) || b.Customer.FullName.Contains(s) || (b.Customer.Email != null && b.Customer.Email.Contains(s)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new AdminBookingListItemDto
            {
                Id = b.Id,
                BookingNumber = b.BookingNumber,
                CustomerId = b.CustomerId,
                CustomerName = b.Customer.FullName,
                CustomerEmail = b.Customer.Email ?? string.Empty,
                Status = b.Status,
                TotalRentalFee = b.TotalRentalFee,
                TotalSecurityDeposit = b.TotalSecurityDeposit,
                GrandTotal = b.GrandTotal,
                GroupsCount = b.BookingGroups.Count,
                CreatedAtUtc = b.CreatedAtUtc
            })
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<AdminBookingDetailDto?> GetBookingDetailsAsync(int bookingId)
    {
        var b = await _context.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.Business)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
                    .ThenInclude(bi => bi.Equipment)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
                    .ThenInclude(bi => bi.Review)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (b == null) return null;

        return new AdminBookingDetailDto
        {
            Id = b.Id,
            BookingNumber = b.BookingNumber,
            CustomerId = b.CustomerId,
            CustomerName = b.Customer.FullName,
            CustomerEmail = b.Customer.Email ?? string.Empty,
            CustomerPhone = b.Customer.PhoneNumber ?? string.Empty,
            Status = b.Status,
            TotalRentalFee = b.TotalRentalFee,
            TotalSecurityDeposit = b.TotalSecurityDeposit,
            GrandTotal = b.GrandTotal,
            CreatedAtUtc = b.CreatedAtUtc,
            UpdatedAtUtc = b.UpdatedAtUtc,
            Groups = b.BookingGroups.Select(g => new AdminBookingGroupDetailDto
            {
                Id = g.Id,
                GroupNumber = g.GroupNumber,
                BusinessId = g.BusinessId,
                BusinessName = g.Business.ShopName,
                Status = g.Status,
                SubtotalRentalFee = g.SubtotalRentalFee,
                SubtotalSecurityDeposit = g.SubtotalSecurityDeposit,
                PickupVerificationCode = g.PickupVerificationCode,
                HandoverNotes = g.HandoverNotes,
                HandedOverAtUtc = g.HandedOverAtUtc,
                ReturnInspectionNotes = g.ReturnInspectionNotes,
                ReturnedAtUtc = g.ReturnedAtUtc,
                CompletedAtUtc = g.CompletedAtUtc,
                RejectionReason = g.RejectionReason,
                Items = g.BookingItems.Select(i => new AdminBookingItemDto
                {
                    Id = i.Id,
                    EquipmentId = i.EquipmentId,
                    EquipmentTitle = i.Equipment.Title,
                    Quantity = i.Quantity,
                    StartDate = i.StartDate,
                    EndDate = i.EndDate,
                    DaysCount = i.DaysCount,
                    DailyRentalRate = i.DailyRentalRate,
                    TotalItemPrice = i.TotalItemPrice,
                    ReviewId = i.Review?.Id,
                    ReviewRating = i.Review?.Rating,
                    ReviewIsHidden = i.Review?.IsHidden
                }).ToList()
            }).ToList()
        };
    }

    public async Task<List<AdminDamageReportDto>> GetDamageReportsAsync(bool? isResolvedFilter = null)
    {
        var query = _context.MaintenanceRecords
            .Include(m => m.Equipment)
                .ThenInclude(e => e.Business)
            .AsQueryable();

        if (isResolvedFilter.HasValue)
        {
            query = query.Where(m => m.IsResolved == isResolvedFilter.Value);
        }

        return await query
            .OrderByDescending(m => m.CreatedAtUtc)
            .Select(m => new AdminDamageReportDto
            {
                MaintenanceRecordId = m.Id,
                EquipmentId = m.EquipmentId,
                EquipmentTitle = m.Equipment.Title,
                BusinessId = m.Equipment.BusinessId,
                ShopName = m.Equipment.Business.ShopName,
                BlockedQuantity = m.BlockedQuantity,
                StartDate = m.StartDate,
                EndDate = m.EndDate,
                Reason = m.Reason,
                EstimatedCost = m.EstimatedCost,
                IsResolved = m.IsResolved,
                CreatedAtUtc = m.CreatedAtUtc,
                ResolvedAtUtc = m.ResolvedAtUtc
            })
            .ToListAsync();
    }

    public async Task<bool> ResolveDamageReportAsync(int maintenanceRecordId, string adminUserId, string? resolutionNotes = null, string? ipAddress = null)
    {
        var record = await _context.MaintenanceRecords.FindAsync(maintenanceRecordId);
        if (record == null) return false;

        record.IsResolved = true;
        record.ResolvedAtUtc = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(resolutionNotes))
        {
            record.Reason = $"{record.Reason} | Admin Resolution: {resolutionNotes.Trim()}";
        }

        var validUserId = await ResolveValidUserIdAsync(adminUserId);
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = "ResolveDamageReport",
            EntityType = nameof(MaintenanceRecord),
            EntityId = maintenanceRecordId.ToString(),
            Details = $"{{\"Resolved\": true, \"ResolutionNotes\": \"{resolutionNotes ?? string.Empty}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<AdminPlatformAnalyticsDto> GetPlatformAnalyticsAsync()
    {
        var completedGroups = await _context.BookingGroups.CountAsync(g => g.Status == BookingGroupStatus.Completed);
        var damageCount = await _context.MaintenanceRecords.CountAsync();
        var damageRate = completedGroups > 0 ? Math.Round((double)damageCount / completedGroups * 100, 1) : 0.0;

        // Monthly trends for the past 6 months
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);
        var recentBookings = await _context.Bookings
            .Where(b => b.CreatedAtUtc >= sixMonthsAgo && b.Status != BookingStatus.Cancelled)
            .Select(b => new { b.CreatedAtUtc, b.TotalRentalFee })
            .ToListAsync();

        var monthlyTrends = recentBookings
            .GroupBy(b => new { b.CreatedAtUtc.Year, b.CreatedAtUtc.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new MonthlyBookingTrendDto
            {
                MonthLabel = new DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                BookingsCount = g.Count(),
                TotalRentalFeeVolume = g.Sum(x => x.TotalRentalFee)
            })
            .ToList();

        // Top categories
        var topCategories = await _context.BookingItems
            .Include(i => i.Equipment)
                .ThenInclude(e => e.Category)
            .GroupBy(i => i.Equipment.Category.Name)
            .Select(g => new TopCategoryMetricDto
            {
                CategoryName = g.Key,
                TotalRentalItemsCount = g.Count(),
                RentalFeeVolume = g.Sum(x => x.TotalItemPrice)
            })
            .OrderByDescending(c => c.RentalFeeVolume)
            .Take(5)
            .ToListAsync();

        // Top equipment
        var topEquipment = await _context.BookingItems
            .Include(i => i.Equipment)
                .ThenInclude(e => e.Business)
            .GroupBy(i => new { i.EquipmentId, i.Equipment.Title, i.Equipment.Business.ShopName })
            .Select(g => new TopEquipmentMetricDto
            {
                EquipmentId = g.Key.EquipmentId,
                EquipmentTitle = g.Key.Title,
                ShopName = g.Key.ShopName,
                TotalBookingsCount = g.Count(),
                RentalRevenue = g.Sum(x => x.TotalItemPrice)
            })
            .OrderByDescending(e => e.RentalRevenue)
            .Take(5)
            .ToListAsync();

        // Top stores
        var topStores = await _context.BookingGroups
            .Include(g => g.Business)
            .Where(g => g.Status == BookingGroupStatus.Completed)
            .GroupBy(g => new { g.BusinessId, g.Business.ShopName, g.Business.City })
            .Select(g => new TopStoreMetricDto
            {
                BusinessId = g.Key.BusinessId,
                ShopName = g.Key.ShopName,
                City = g.Key.City,
                CompletedGroupsCount = g.Count(),
                TotalRentalRevenue = g.Sum(x => x.SubtotalRentalFee)
            })
            .OrderByDescending(s => s.TotalRentalRevenue)
            .Take(5)
            .ToListAsync();

        return new AdminPlatformAnalyticsDto
        {
            TotalCompletedRentals = completedGroups,
            TotalDamageIncidents = damageCount,
            DamageIncidentRatePercent = damageRate,
            MonthlyTrends = monthlyTrends,
            TopCategories = topCategories,
            TopEquipment = topEquipment,
            TopStores = topStores
        };
    }

    public async Task<(List<AdminAuditLogDto> Items, int TotalCount)> GetAuditLogsAsync(string? actionFilter = null, string? entityTypeFilter = null, int page = 1, int pageSize = 30)
    {
        var query = _context.AuditLogs
            .Include(a => a.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionFilter))
        {
            query = query.Where(a => a.Action == actionFilter.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityTypeFilter))
        {
            query = query.Where(a => a.EntityType == entityTypeFilter.Trim());
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AdminAuditLogDto
            {
                Id = a.Id,
                UserId = a.UserId,
                UserName = a.User != null ? a.User.FullName : "System/Admin",
                Action = a.Action,
                EntityType = a.EntityType,
                EntityId = a.EntityId,
                Details = a.Details,
                TimestampUtc = a.TimestampUtc
            })
            .ToListAsync();

        return (items, totalCount);
    }
}
