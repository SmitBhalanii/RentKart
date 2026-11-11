using Microsoft.AspNetCore.Identity;
using RentKart.Core.Enums;
using RentKart.Core.Utilities;

namespace RentKart.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public UserRole UserType { get; set; } = UserRole.Customer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // India-wide customer address fields
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? City { get; set; }
    public string? District { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? Country { get; set; } = IndianLocations.DefaultCountryCode;

    /// <summary>
    /// Complete formatted Indian address for the user, if address fields are populated.
    /// </summary>
    public string? FullFormattedAddress =>
        string.IsNullOrWhiteSpace(AddressLine1)
            ? null
            : IndianLocations.FormatFullAddress(AddressLine1, AddressLine2, Landmark, City ?? string.Empty, District, State ?? string.Empty, PostalCode ?? string.Empty, Country ?? IndianLocations.DefaultCountryCode);

    // Navigation properties
    public virtual BusinessProfile? BusinessProfile { get; set; }
    public virtual ICollection<Booking> Bookings { get; set; } = new List<Booking>();
    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();
    public virtual ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public virtual ICollection<WishlistItem> WishlistItems { get; set; } = new List<WishlistItem>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<RentalDocument> RentalDocuments { get; set; } = new List<RentalDocument>();
}
