using System.ComponentModel.DataAnnotations;
using RentKart.Core.Enums;

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
}

public class BusinessProfileViewModel
{
    public int BusinessId { get; set; }

    [Required(ErrorMessage = "Shop / Business name is required.")]
    [StringLength(150, ErrorMessage = "Shop name cannot exceed 150 characters.")]
    [Display(Name = "Shop / Business Name")]
    public string ShopName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Registration number cannot exceed 50 characters.")]
    [Display(Name = "Registration / License Number (Optional)")]
    public string? RegistrationNumber { get; set; }

    [Required(ErrorMessage = "Contact phone number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Store Phone Number")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Store address is required.")]
    [StringLength(200, ErrorMessage = "Address cannot exceed 200 characters.")]
    [Display(Name = "Store Address (Street & Suite)")]
    public string AddressLine { get; set; } = string.Empty;

    [Required(ErrorMessage = "City is required.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City")]
    public string City { get; set; } = string.Empty;

    [Required(ErrorMessage = "State / Province is required.")]
    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    [Display(Name = "State")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal code is required.")]
    [StringLength(20, ErrorMessage = "Postal code cannot exceed 20 characters.")]
    [Display(Name = "Postal / ZIP Code")]
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
