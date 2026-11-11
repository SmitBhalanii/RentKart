using System.ComponentModel.DataAnnotations;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Core.Validation;

namespace RentKart.Web.Models.Business;

public class BusinessDashboardViewModel
{
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public BusinessStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }

    public bool CanPublishListings => Status == BusinessStatus.Approved;

    // Inventory & Catalog Metrics
    public int TotalListings { get; set; }
    public int ActiveListings { get; set; }
    public int TotalUnits { get; set; }
    public int UnderMaintenanceUnits { get; set; }

    // Booking Lifecycle Counters
    public int PendingApprovalCount { get; set; }
    public int ConfirmedCount { get; set; }
    public int ActiveRentalsCount { get; set; }
    public int CompletedCount { get; set; }

    public List<BusinessBookingGroupListItemDto> RecentBookingGroups { get; set; } = new();
}

public class BusinessProfileViewModel
{
    public int BusinessId { get; set; }

    [Required(ErrorMessage = "Shop / Business name is required.")]
    [StringLength(150, ErrorMessage = "Shop name cannot exceed 150 characters.")]
    [Display(Name = "Shop / Business Name")]
    public string ShopName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Registration number cannot exceed 50 characters.")]
    [Display(Name = "Registration / License / GSTIN (Optional)")]
    public string? RegistrationNumber { get; set; }

    [Required(ErrorMessage = "Contact phone number is required.")]
    [IndianPhoneNumber]
    [Display(Name = "Store Mobile / Phone Number")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Store address is required for customer equipment pickups.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    [Display(Name = "Store Address (Street & Locality)")]
    public string AddressLine { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State is required.")]
    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    [Display(Name = "State")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal PIN code is required.")]
    [RegularExpression(@"^[1-9][0-9]{5}$", ErrorMessage = "Please enter a valid 6-digit Indian PIN code (e.g. 400001).")]
    [Display(Name = "PIN Code")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Operating hours are required.")]
    [StringLength(100, ErrorMessage = "Operating hours cannot exceed 100 characters.")]
    [Display(Name = "Operating Hours")]
    public string OperatingHours { get; set; } = string.Empty;

    [StringLength(500, ErrorMessage = "Pickup instructions cannot exceed 500 characters.")]
    [Display(Name = "Pickup & Counter Instructions (Optional)")]
    public string? PickupInstructions { get; set; }

    [Display(Name = "Account Status")]
    public BusinessStatus Status { get; set; }
}
