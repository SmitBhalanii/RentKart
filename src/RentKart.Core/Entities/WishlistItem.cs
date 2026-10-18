namespace RentKart.Core.Entities;

public class WishlistItem
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int EquipmentId { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Equipment Equipment { get; set; } = null!;
}
