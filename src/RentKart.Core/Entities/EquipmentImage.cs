namespace RentKart.Core.Entities;

public class EquipmentImage
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
    public bool IsPrimary { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual Equipment Equipment { get; set; } = null!;
}
