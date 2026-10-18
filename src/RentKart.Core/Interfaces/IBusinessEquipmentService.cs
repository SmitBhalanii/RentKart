using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class EquipmentManagementSummaryDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; }
    public int ActiveMaintenanceBlockedQuantity { get; set; }
    public int AvailableQuantity => Math.Max(0, TotalQuantity - ActiveMaintenanceBlockedQuantity);
    public EquipmentCondition Condition { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsActive { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public int ImagesCount { get; set; }
    public int ActiveReservationsCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class CreateEquipmentDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; } = 1;
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public bool IsActive { get; set; } = true;
    public List<string> ImageUrls { get; set; } = new();
}

public class UpdateEquipmentDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; }
    public EquipmentCondition Condition { get; set; }
    public bool IsActive { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class AddMaintenanceRecordDto
{
    public int BlockedQuantity { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
}

public class EquipmentOperationResult
{
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public int? EquipmentId { get; set; }

    public static EquipmentOperationResult Success(int? id = null) => new() { Succeeded = true, EquipmentId = id };
    public static EquipmentOperationResult Failed(string error) => new() { Succeeded = false, ErrorMessage = error };
}

public interface IBusinessEquipmentService
{
    Task<List<EquipmentManagementSummaryDto>> GetBusinessEquipmentAsync(int businessId);
    Task<Equipment?> GetEquipmentForBusinessAsync(int equipmentId, int businessId);
    Task<EquipmentOperationResult> CreateEquipmentAsync(int businessId, CreateEquipmentDto dto);
    Task<EquipmentOperationResult> UpdateEquipmentAsync(int equipmentId, int businessId, UpdateEquipmentDto dto);
    Task<EquipmentOperationResult> ToggleEquipmentStatusAsync(int equipmentId, int businessId);
    Task<EquipmentOperationResult> AddEquipmentImageAsync(int equipmentId, int businessId, string imageUrl, bool isPrimary = false);
    Task<EquipmentOperationResult> DeleteEquipmentImageAsync(int imageId, int equipmentId, int businessId);
    Task<EquipmentOperationResult> SetPrimaryImageAsync(int imageId, int equipmentId, int businessId);
    Task<List<MaintenanceRecord>> GetMaintenanceRecordsAsync(int equipmentId, int businessId);
    Task<EquipmentOperationResult> AddMaintenanceRecordAsync(int equipmentId, int businessId, AddMaintenanceRecordDto dto);
    Task<EquipmentOperationResult> ResolveMaintenanceRecordAsync(int recordId, int equipmentId, int businessId);
    Task<EquipmentOperationResult> DeleteMaintenanceRecordAsync(int recordId, int equipmentId, int businessId);
    Task<int> GetMaxActiveReservedQuantityAsync(int equipmentId, DateOnly? fromDate = null, DateOnly? toDate = null);
    Task<int> GetAvailableQuantityAsync(int equipmentId, DateOnly startDate, DateOnly endDate);
}
