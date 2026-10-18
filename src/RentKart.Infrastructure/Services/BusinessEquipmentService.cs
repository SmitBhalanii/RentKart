using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class BusinessEquipmentService : IBusinessEquipmentService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BusinessEquipmentService> _logger;

    public BusinessEquipmentService(ApplicationDbContext context, ILogger<BusinessEquipmentService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<EquipmentManagementSummaryDto>> GetBusinessEquipmentAsync(int businessId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var equipments = await _context.Equipments
            .AsNoTracking()
            .Where(e => e.BusinessId == businessId)
            .Include(e => e.Category)
            .Include(e => e.Images)
            .Include(e => e.MaintenanceRecords)
            .Include(e => e.BookingItems)
                .ThenInclude(bi => bi.BookingGroup)
            .OrderByDescending(e => e.CreatedAtUtc)
            .ToListAsync();

        return equipments.Select(e =>
        {
            var activeMaintenanceQty = e.MaintenanceRecords
                .Where(m => !m.IsResolved && m.StartDate <= today && m.EndDate >= today)
                .Sum(m => m.BlockedQuantity);

            var activeReservationsCount = e.BookingItems
                .Count(bi => bi.EndDate >= today 
                          && (bi.BookingGroup.Status == BookingGroupStatus.Confirmed 
                           || bi.BookingGroup.Status == BookingGroupStatus.HandedOver));

            var primaryImg = e.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                          ?? e.Images.FirstOrDefault()?.ImageUrl;

            return new EquipmentManagementSummaryDto
            {
                Id = e.Id,
                Title = e.Title,
                CategoryId = e.CategoryId,
                CategoryName = e.Category.Name,
                DailyRentalRate = e.DailyRentalRate,
                SecurityDeposit = e.SecurityDeposit,
                TotalQuantity = e.TotalQuantity,
                ActiveMaintenanceBlockedQuantity = activeMaintenanceQty,
                Condition = e.Condition,
                Status = e.Status,
                IsActive = e.IsActive,
                PrimaryImageUrl = primaryImg,
                ImagesCount = e.Images.Count,
                ActiveReservationsCount = activeReservationsCount,
                CreatedAtUtc = e.CreatedAtUtc
            };
        }).ToList();
    }

    public async Task<Equipment?> GetEquipmentForBusinessAsync(int equipmentId, int businessId)
    {
        return await _context.Equipments
            .AsNoTracking()
            .Include(e => e.Category)
            .Include(e => e.Images.OrderBy(i => i.DisplayOrder))
            .Include(e => e.MaintenanceRecords.OrderByDescending(m => m.StartDate))
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);
    }

    public async Task<EquipmentOperationResult> CreateEquipmentAsync(int businessId, CreateEquipmentDto dto)
    {
        // 1. Enforce business approval server-side
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            _logger.LogWarning("CreateEquipment rejected: Business {BusinessId} is not approved to publish listings.", businessId);
            return EquipmentOperationResult.Failed("Only approved businesses are permitted to publish equipment listings.");
        }

        // 2. Validate inputs
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return EquipmentOperationResult.Failed("Equipment title is required.");
        }
        if (dto.Title.Trim().Length > 200)
        {
            return EquipmentOperationResult.Failed("Equipment title cannot exceed 200 characters.");
        }
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            return EquipmentOperationResult.Failed("Equipment description and specifications are required.");
        }
        if (dto.DailyRentalRate <= 0.00m)
        {
            return EquipmentOperationResult.Failed("Daily rental rate must be greater than zero.");
        }
        if (dto.SecurityDeposit < 0.00m)
        {
            return EquipmentOperationResult.Failed("Security deposit cannot be negative.");
        }
        if (dto.TotalQuantity < 1)
        {
            return EquipmentOperationResult.Failed("Total inventory quantity must be at least 1.");
        }

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.IsActive);
        if (!categoryExists)
        {
            return EquipmentOperationResult.Failed("Selected category is invalid or inactive.");
        }

        // 3. Create equipment
        var equipment = new Equipment
        {
            BusinessId = businessId,
            CategoryId = dto.CategoryId,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim(),
            DailyRentalRate = decimal.Round(dto.DailyRentalRate, 2),
            SecurityDeposit = decimal.Round(dto.SecurityDeposit, 2),
            TotalQuantity = dto.TotalQuantity,
            Condition = dto.Condition,
            IsActive = dto.IsActive,
            Status = dto.IsActive ? EquipmentStatus.Active : EquipmentStatus.Inactive,
            CreatedAtUtc = DateTime.UtcNow
        };

        // 4. Add initial images if provided
        if (dto.ImageUrls.Count > 0)
        {
            var order = 1;
            var isFirst = true;
            foreach (var rawUrl in dto.ImageUrls)
            {
                var cleanUrl = rawUrl?.Trim();
                if (string.IsNullOrWhiteSpace(cleanUrl)) continue;

                if (!IsValidUrl(cleanUrl))
                {
                    return EquipmentOperationResult.Failed($"Image URL '{cleanUrl}' is not a valid absolute web URL.");
                }

                equipment.Images.Add(new EquipmentImage
                {
                    ImageUrl = cleanUrl,
                    IsPrimary = isFirst,
                    DisplayOrder = order++,
                    CreatedAtUtc = DateTime.UtcNow
                });

                isFirst = false;
            }
        }

        _context.Equipments.Add(equipment);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Equipment {EquipmentId} created by business {BusinessId}.", equipment.Id, businessId);
        return EquipmentOperationResult.Success(equipment.Id);
    }

    public async Task<EquipmentOperationResult> UpdateEquipmentAsync(int equipmentId, int businessId, UpdateEquipmentDto dto)
    {
        // 1. Enforce business approval server-side
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can manage equipment listings.");
        }

        // 2. Enforce strict server-side ownership
        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            _logger.LogWarning("Unauthorized or invalid update attempt on equipment {EquipmentId} by business {BusinessId}.", equipmentId, businessId);
            return EquipmentOperationResult.Failed("Equipment listing not found or you do not have permission to modify it.");
        }

        // 3. Validate inputs
        if (string.IsNullOrWhiteSpace(dto.Title))
        {
            return EquipmentOperationResult.Failed("Equipment title is required.");
        }
        if (dto.Title.Trim().Length > 200)
        {
            return EquipmentOperationResult.Failed("Equipment title cannot exceed 200 characters.");
        }
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            return EquipmentOperationResult.Failed("Equipment description and specifications are required.");
        }
        if (dto.DailyRentalRate <= 0.00m)
        {
            return EquipmentOperationResult.Failed("Daily rental rate must be greater than zero.");
        }
        if (dto.SecurityDeposit < 0.00m)
        {
            return EquipmentOperationResult.Failed("Security deposit cannot be negative.");
        }
        if (dto.TotalQuantity < 1)
        {
            return EquipmentOperationResult.Failed("Total inventory quantity must be at least 1.");
        }

        var categoryExists = await _context.Categories.AnyAsync(c => c.Id == dto.CategoryId && c.IsActive);
        if (!categoryExists)
        {
            return EquipmentOperationResult.Failed("Selected category is invalid or inactive.");
        }

        // 4. INVENTORY REDUCTION GUARD AGAINST CONFIRMED RESERVATIONS
        if (dto.TotalQuantity < equipment.TotalQuantity)
        {
            var maxReserved = await GetMaxActiveReservedQuantityAsync(equipmentId);
            if (dto.TotalQuantity < maxReserved)
            {
                return EquipmentOperationResult.Failed(
                    $"Cannot reduce total inventory to {dto.TotalQuantity}. There are currently {maxReserved} unit(s) committed to confirmed or active customer reservations.");
            }
        }

        // 5. Apply changes and concurrency check
        if (dto.RowVersion != null && dto.RowVersion.Length > 0)
        {
            _context.Entry(equipment).Property(e => e.RowVersion).OriginalValue = dto.RowVersion;
        }

        equipment.Title = dto.Title.Trim();
        equipment.Description = dto.Description.Trim();
        equipment.CategoryId = dto.CategoryId;
        equipment.DailyRentalRate = decimal.Round(dto.DailyRentalRate, 2);
        equipment.SecurityDeposit = decimal.Round(dto.SecurityDeposit, 2);
        equipment.TotalQuantity = dto.TotalQuantity;
        equipment.Condition = dto.Condition;
        equipment.IsActive = dto.IsActive;
        equipment.Status = dto.IsActive ? EquipmentStatus.Active : EquipmentStatus.Inactive;
        equipment.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("Equipment {EquipmentId} updated successfully by business {BusinessId}.", equipmentId, businessId);
            return EquipmentOperationResult.Success(equipment.Id);
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogWarning("Concurrency conflict updating equipment {EquipmentId}.", equipmentId);
            return EquipmentOperationResult.Failed("This listing was updated by another process. Please reload and reapply your changes.");
        }
    }

    public async Task<EquipmentOperationResult> ToggleEquipmentStatusAsync(int equipmentId, int businessId)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can change listing activation status.");
        }

        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            return EquipmentOperationResult.Failed("Equipment listing not found or unauthorized.");
        }

        equipment.IsActive = !equipment.IsActive;
        equipment.Status = equipment.IsActive ? EquipmentStatus.Active : EquipmentStatus.Inactive;
        equipment.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Equipment {EquipmentId} status toggled to IsActive={IsActive} by business {BusinessId}.", equipmentId, equipment.IsActive, businessId);

        return EquipmentOperationResult.Success(equipment.Id);
    }

    public async Task<EquipmentOperationResult> AddEquipmentImageAsync(int equipmentId, int businessId, string imageUrl, bool isPrimary = false)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can manage equipment images.");
        }

        var equipment = await _context.Equipments
            .Include(e => e.Images)
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            return EquipmentOperationResult.Failed("Equipment listing not found or unauthorized.");
        }

        var cleanUrl = imageUrl?.Trim();
        if (string.IsNullOrWhiteSpace(cleanUrl) || !IsValidUrl(cleanUrl))
        {
            return EquipmentOperationResult.Failed("Please provide a valid, absolute image URL (http or https).");
        }

        // If this image is designated primary, clear primary from existing images
        if (isPrimary || equipment.Images.Count == 0)
        {
            foreach (var img in equipment.Images)
            {
                img.IsPrimary = false;
            }
            isPrimary = true;
        }

        var nextOrder = (equipment.Images.Max(i => (int?)i.DisplayOrder) ?? 0) + 1;

        var newImage = new EquipmentImage
        {
            EquipmentId = equipmentId,
            ImageUrl = cleanUrl,
            IsPrimary = isPrimary,
            DisplayOrder = nextOrder,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.EquipmentImages.Add(newImage);
        await _context.SaveChangesAsync();

        return EquipmentOperationResult.Success(equipmentId);
    }

    public async Task<EquipmentOperationResult> DeleteEquipmentImageAsync(int imageId, int equipmentId, int businessId)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can manage equipment images.");
        }

        var equipment = await _context.Equipments
            .Include(e => e.Images)
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            return EquipmentOperationResult.Failed("Equipment listing not found or unauthorized.");
        }

        var image = equipment.Images.FirstOrDefault(i => i.Id == imageId);
        if (image == null)
        {
            return EquipmentOperationResult.Failed("Image not found.");
        }

        var wasPrimary = image.IsPrimary;
        _context.EquipmentImages.Remove(image);

        // If removed image was primary, designate the earliest remaining image as primary
        if (wasPrimary)
        {
            var nextPrimary = equipment.Images
                .Where(i => i.Id != imageId)
                .OrderBy(i => i.DisplayOrder)
                .FirstOrDefault();

            if (nextPrimary != null)
            {
                nextPrimary.IsPrimary = true;
            }
        }

        await _context.SaveChangesAsync();
        return EquipmentOperationResult.Success(equipmentId);
    }

    public async Task<EquipmentOperationResult> SetPrimaryImageAsync(int imageId, int equipmentId, int businessId)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can manage equipment images.");
        }

        var equipment = await _context.Equipments
            .Include(e => e.Images)
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            return EquipmentOperationResult.Failed("Equipment listing not found or unauthorized.");
        }

        var targetImage = equipment.Images.FirstOrDefault(i => i.Id == imageId);
        if (targetImage == null)
        {
            return EquipmentOperationResult.Failed("Image not found.");
        }

        foreach (var img in equipment.Images)
        {
            img.IsPrimary = (img.Id == imageId);
        }

        await _context.SaveChangesAsync();
        return EquipmentOperationResult.Success(equipmentId);
    }

    public async Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync(int equipmentId, int businessId)
    {
        return await _context.MaintenanceRecords
            .AsNoTracking()
            .Where(m => m.EquipmentId == equipmentId && m.Equipment.BusinessId == businessId)
            .OrderByDescending(m => m.StartDate)
            .ToListAsync();
    }

    public async Task<EquipmentOperationResult> AddMaintenanceRecordAsync(int equipmentId, int businessId, AddMaintenanceRecordDto dto)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null || business.Status != BusinessStatus.Approved)
        {
            return EquipmentOperationResult.Failed("Only approved businesses can schedule maintenance downtime.");
        }

        var equipment = await _context.Equipments
            .FirstOrDefaultAsync(e => e.Id == equipmentId && e.BusinessId == businessId);

        if (equipment == null)
        {
            return EquipmentOperationResult.Failed("Equipment listing not found or unauthorized.");
        }

        if (dto.BlockedQuantity < 1)
        {
            return EquipmentOperationResult.Failed("Blocked maintenance quantity must be at least 1.");
        }
        if (dto.BlockedQuantity > equipment.TotalQuantity)
        {
            return EquipmentOperationResult.Failed($"Blocked quantity cannot exceed total equipment inventory of {equipment.TotalQuantity}.");
        }
        if (dto.StartDate > dto.EndDate)
        {
            return EquipmentOperationResult.Failed("Maintenance start date cannot be after end date.");
        }
        if (string.IsNullOrWhiteSpace(dto.Reason))
        {
            return EquipmentOperationResult.Failed("Maintenance reason or description is required.");
        }
        if (dto.EstimatedCost.HasValue && dto.EstimatedCost.Value < 0.00m)
        {
            return EquipmentOperationResult.Failed("Estimated maintenance cost cannot be negative.");
        }

        // RESERVATION COLLISION CHECK: Maintenance cannot overcommit inventory during reservation dates
        var maxReservedInWindow = await GetMaxActiveReservedQuantityAsync(equipmentId, dto.StartDate, dto.EndDate);
        var existingBlockedInWindow = await GetMaxMaintenanceBlockedQuantityAsync(equipmentId, dto.StartDate, dto.EndDate);

        if (dto.BlockedQuantity + maxReservedInWindow + existingBlockedInWindow > equipment.TotalQuantity)
        {
            return EquipmentOperationResult.Failed(
                $"Cannot schedule maintenance for {dto.BlockedQuantity} unit(s). Overlapping confirmed reservations ({maxReservedInWindow} units) and existing maintenance ({existingBlockedInWindow} units) would exceed total inventory capacity of {equipment.TotalQuantity}.");
        }

        var record = new MaintenanceRecord
        {
            EquipmentId = equipmentId,
            BlockedQuantity = dto.BlockedQuantity,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason.Trim(),
            EstimatedCost = dto.EstimatedCost.HasValue ? decimal.Round(dto.EstimatedCost.Value, 2) : null,
            IsResolved = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.MaintenanceRecords.Add(record);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Maintenance record {RecordId} added for equipment {EquipmentId}.", record.Id, equipmentId);
        return EquipmentOperationResult.Success(record.Id);
    }

    public async Task<EquipmentOperationResult> ResolveMaintenanceRecordAsync(int recordId, int equipmentId, int businessId)
    {
        var record = await _context.MaintenanceRecords
            .Include(m => m.Equipment)
            .FirstOrDefaultAsync(m => m.Id == recordId && m.EquipmentId == equipmentId && m.Equipment.BusinessId == businessId);

        if (record == null)
        {
            return EquipmentOperationResult.Failed("Maintenance record not found or unauthorized.");
        }

        record.IsResolved = true;
        record.ResolvedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Maintenance record {RecordId} resolved.", recordId);

        return EquipmentOperationResult.Success(recordId);
    }

    public async Task<EquipmentOperationResult> DeleteMaintenanceRecordAsync(int recordId, int equipmentId, int businessId)
    {
        var record = await _context.MaintenanceRecords
            .Include(m => m.Equipment)
            .FirstOrDefaultAsync(m => m.Id == recordId && m.EquipmentId == equipmentId && m.Equipment.BusinessId == businessId);

        if (record == null)
        {
            return EquipmentOperationResult.Failed("Maintenance record not found or unauthorized.");
        }

        _context.MaintenanceRecords.Remove(record);
        await _context.SaveChangesAsync();
        _logger.LogInformation("Maintenance record {RecordId} deleted.", recordId);

        return EquipmentOperationResult.Success(recordId);
    }

    public async Task<int> GetMaxActiveReservedQuantityAsync(int equipmentId, DateOnly? fromDate = null, DateOnly? toDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _context.BookingItems
            .AsNoTracking()
            .Where(bi => bi.EquipmentId == equipmentId 
                      && (bi.BookingGroup.Status == BookingGroupStatus.Confirmed 
                       || bi.BookingGroup.Status == BookingGroupStatus.HandedOver));

        if (fromDate.HasValue)
        {
            query = query.Where(bi => bi.EndDate >= fromDate.Value);
        }
        else
        {
            query = query.Where(bi => bi.EndDate >= today);
        }

        if (toDate.HasValue)
        {
            query = query.Where(bi => bi.StartDate <= toDate.Value);
        }

        var activeBookings = await query
            .Select(bi => new { bi.StartDate, bi.EndDate, bi.Quantity })
            .ToListAsync();

        if (activeBookings.Count == 0) return 0;

        // Evaluate all boundary dates to determine peak simultaneous overlapping reservations
        var criticalDates = activeBookings
            .Select(b => b.StartDate)
            .Union(activeBookings.Select(b => b.EndDate))
            .Distinct()
            .ToList();

        var maxOverlap = 0;
        foreach (var date in criticalDates)
        {
            var overlappingSum = activeBookings
                .Where(b => b.StartDate <= date && b.EndDate >= date)
                .Sum(b => b.Quantity);

            if (overlappingSum > maxOverlap)
            {
                maxOverlap = overlappingSum;
            }
        }

        return maxOverlap;
    }

    public async Task<int> GetAvailableQuantityAsync(int equipmentId, DateOnly startDate, DateOnly endDate)
    {
        var equipment = await _context.Equipments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == equipmentId);

        if (equipment == null || !equipment.IsActive || equipment.Status != EquipmentStatus.Active)
        {
            return 0;
        }

        var maxReserved = await GetMaxActiveReservedQuantityAsync(equipmentId, startDate, endDate);
        var maxBlocked = await GetMaxMaintenanceBlockedQuantityAsync(equipmentId, startDate, endDate);

        return Math.Max(0, equipment.TotalQuantity - maxReserved - maxBlocked);
    }

    private async Task<int> GetMaxMaintenanceBlockedQuantityAsync(int equipmentId, DateOnly startDate, DateOnly endDate)
    {
        var activeMaintenance = await _context.MaintenanceRecords
            .AsNoTracking()
            .Where(m => m.EquipmentId == equipmentId 
                     && !m.IsResolved 
                     && m.StartDate <= endDate 
                     && m.EndDate >= startDate)
            .Select(m => new { m.StartDate, m.EndDate, m.BlockedQuantity })
            .ToListAsync();

        if (activeMaintenance.Count == 0) return 0;

        var criticalDates = activeMaintenance
            .Select(m => m.StartDate)
            .Union(activeMaintenance.Select(m => m.EndDate))
            .Distinct()
            .ToList();

        var maxBlocked = 0;
        foreach (var date in criticalDates)
        {
            var blockedSum = activeMaintenance
                .Where(m => m.StartDate <= date && m.EndDate >= date)
                .Sum(m => m.BlockedQuantity);

            if (blockedSum > maxBlocked)
            {
                maxBlocked = blockedSum;
            }
        }

        return maxBlocked;
    }

    private static bool IsValidUrl(string url)
    {
        return Uri.TryCreate(url, UriKind.Absolute, out var uriResult)
            && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
    }
}
