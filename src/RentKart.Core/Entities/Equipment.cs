using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class Equipment
{
    public int Id { get; set; }
    public int BusinessId { get; set; }
    public int CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; } = 1;
    public EquipmentCondition Condition { get; set; } = EquipmentCondition.Good;
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Active;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Concurrency token to protect against simultaneous inventory mutation
    public byte[]? RowVersion { get; set; }

    // Navigation properties
    public virtual BusinessProfile Business { get; set; } = null!;
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<EquipmentImage> Images { get; set; } = new List<EquipmentImage>();
    public virtual ICollection<BookingItem> BookingItems { get; set; } = new List<BookingItem>();
    public virtual ICollection<MaintenanceRecord> MaintenanceRecords { get; set; } = new List<MaintenanceRecord>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
}
