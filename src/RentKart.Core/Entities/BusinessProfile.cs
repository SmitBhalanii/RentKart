using RentKart.Core.Enums;
using RentKart.Core.Utilities;

namespace RentKart.Core.Entities;

public class BusinessProfile
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string ContactPhone { get; set; } = string.Empty;

    // India-wide physical storefront pickup address
    public string AddressLine { get; set; } = string.Empty;
    public string AddressLine1
    {
        get => AddressLine;
        set => AddressLine = value;
    }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string City { get; set; } = string.Empty;
    public string? District { get; set; }
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = IndianLocations.DefaultCountryCode;

    public string OperatingHours { get; set; } = string.Empty;
    public string? PickupInstructions { get; set; }
    public BusinessStatus Status { get; set; } = BusinessStatus.PendingApproval;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }

    /// <summary>
    /// Complete, formatted Indian address suitable for vouchers, invoices, and customer pickup instructions.
    /// </summary>
    public string FullFormattedAddress =>
        IndianLocations.FormatFullAddress(AddressLine, AddressLine2, Landmark, City, District, State, PostalCode, Country);

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual ICollection<Equipment> Equipments { get; set; } = new List<Equipment>();
    public virtual ICollection<BookingGroup> BookingGroups { get; set; } = new List<BookingGroup>();
}
