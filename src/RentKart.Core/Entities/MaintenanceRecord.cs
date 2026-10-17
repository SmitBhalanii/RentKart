namespace RentKart.Core.Entities;

public class MaintenanceRecord
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int BlockedQuantity { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public bool IsResolved { get; set; } = false;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAtUtc { get; set; }

    // Navigation property
    public virtual Equipment Equipment { get; set; } = null!;
}
