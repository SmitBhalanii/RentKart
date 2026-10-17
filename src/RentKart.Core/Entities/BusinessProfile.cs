using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class BusinessProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public string? PickupInstructions { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.PendingApproval;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    public virtual ICollection<BookingGroup> BookingGroups { get; set; } = new List<BookingGroup>();
}
