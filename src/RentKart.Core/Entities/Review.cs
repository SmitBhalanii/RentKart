namespace RentKart.Core.Entities;

public class Review
{
    public int Id { get; set; }
    public int BookingItemId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int EquipmentId { get; set; }
    public int Rating { get; set; } // 1 to 5 stars
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual BookingItem BookingItem { get; set; } = null!;
    public virtual ApplicationUser Customer { get; set; } = null!;
    public virtual Equipment Equipment { get; set; } = null!;
}
