using System.ComponentModel.DataAnnotations;
using RentKart.Core.Interfaces;
using RentKart.Core.Validation;

namespace RentKart.Web.Models.Customer;

public class CustomerDashboardViewModel
{
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public DateTime MemberSinceUtc { get; set; }

    // Real Metrics
    public int TotalBookingsCount { get; set; }
    public int ActiveRentalsCount { get; set; }
    public int PendingRequestsCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int CompletedRentalsCount { get; set; }
    public int WishlistItemsCount { get; set; }

    // Real Lists
    public List<CustomerBookingSummaryDto> RecentBookings { get; set; } = new();
    public List<CustomerWishlistPreviewItem> WishlistPreview { get; set; } = new();
}

public class CustomerWishlistPreviewItem
{
    public int EquipmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
}

public class CustomerProfileViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indian mobile number is required.")]
    [IndianPhoneNumber]
    [Display(Name = "Mobile Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    // India-wide customer billing and rental verification address
    [StringLength(200, ErrorMessage = "Address Line 1 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 1 (Flat, House no., Building, Apartment)")]
    public string? AddressLine1 { get; set; }

    [StringLength(200, ErrorMessage = "Address Line 2 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 2 (Street, Area, Locality)")]
    public string? AddressLine2 { get; set; }

    [StringLength(150, ErrorMessage = "Landmark cannot exceed 150 characters.")]
    [Display(Name = "Landmark (e.g. Near Metro Station)")]
    public string? Landmark { get; set; }

    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City / Town / Village")]
    public string? City { get; set; }

    [StringLength(100, ErrorMessage = "District cannot exceed 100 characters.")]
    [Display(Name = "District")]
    public string? District { get; set; }

    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    [Display(Name = "State / Union Territory")]
    public string? State { get; set; }

    [RegularExpression(@"^[1-9][0-9]{5}$", ErrorMessage = "Please enter a valid 6-digit Indian PIN code (e.g. 400053).")]
    [Display(Name = "PIN Code")]
    public string? PostalCode { get; set; }

    [Display(Name = "Country")]
    public string Country { get; set; } = "IN";

    public string? FullFormattedAddress =>
        string.IsNullOrWhiteSpace(AddressLine1)
            ? null
            : RentKart.Core.Utilities.IndianLocations.FormatFullAddress(AddressLine1, AddressLine2, Landmark, City ?? string.Empty, District, State ?? string.Empty, PostalCode ?? string.Empty, Country);
}
