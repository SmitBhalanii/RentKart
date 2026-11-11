using System.ComponentModel.DataAnnotations;
using RentKart.Core.Utilities;
using RentKart.Core.Validation;

namespace RentKart.Web.Models.Account;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember this device")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterCustomerViewModel
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Indian mobile number is required.")]
    [IndianPhoneNumber]
    [Display(Name = "Mobile Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class RegisterBusinessViewModel
{
    // Owner User Account Information
    [Required(ErrorMessage = "Owner full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    [Display(Name = "Owner Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Business Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
    [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{8,}$",
        ErrorMessage = "Password must contain at least one uppercase letter, one lowercase letter, one number, and one special character.")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please confirm your password.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "Password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Business Profile Information
    [Required(ErrorMessage = "Shop / Company name is required.")]
    [StringLength(150, ErrorMessage = "Shop name cannot exceed 150 characters.")]
    [Display(Name = "Shop / Business Name")]
    public string ShopName { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "Registration number cannot exceed 50 characters.")]
    [Display(Name = "Registration / License / GSTIN (Optional)")]
    public string? RegistrationNumber { get; set; }

    [Required(ErrorMessage = "Contact phone number is required.")]
    [IndianPhoneNumber]
    [Display(Name = "Store Mobile / Contact Number")]
    public string ContactPhone { get; set; } = string.Empty;

    // India-wide store pickup address
    [Required(ErrorMessage = "Address Line 1 is required for store pickup location.")]
    [StringLength(200, ErrorMessage = "Address Line 1 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 1 (Building, Shop / Flat No., Complex)")]
    public string AddressLine1 { get; set; } = string.Empty;

    /// <summary>
    /// Backward-compatible alias for AddressLine1.
    /// </summary>
    public string AddressLine
    {
        get => AddressLine1;
        set => AddressLine1 = value;
    }

    [StringLength(200, ErrorMessage = "Address Line 2 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 2 (Street, Area, Road, Sector)")]
    public string? AddressLine2 { get; set; }

    [StringLength(150, ErrorMessage = "Landmark cannot exceed 150 characters.")]
    [Display(Name = "Landmark (e.g. Near Metro Station, Opp. City Mall)")]
    public string? Landmark { get; set; }

    [Required(ErrorMessage = "City / Town / Village is required.")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters.")]
    [Display(Name = "City / Town / Village")]
    public string City { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "District cannot exceed 100 characters.")]
    [Display(Name = "District (Optional)")]
    public string? District { get; set; }

    [Required(ErrorMessage = "State / Union Territory is required.")]
    [StringLength(100, ErrorMessage = "State / UT is required.")]
    [Display(Name = "State / Union Territory")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "Postal PIN code is required.")]
    [RegularExpression(@"^[1-9][0-9]{5}$", ErrorMessage = "Please enter a valid 6-digit Indian PIN code (e.g. 400053 or 560001).")]
    [Display(Name = "PIN Code")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Country")]
    public string Country { get; set; } = "IN";

    [Required(ErrorMessage = "Operating hours are required for pickup and return scheduling.")]
    [StringLength(100, ErrorMessage = "Operating hours cannot exceed 100 characters.")]
    [Display(Name = "Operating Hours (e.g. Mon-Sat: 09:00 - 18:00)")]
    public string OperatingHours { get; set; } = "Mon-Sat: 09:00 - 18:00";

    [StringLength(500, ErrorMessage = "Pickup instructions cannot exceed 500 characters.")]
    [Display(Name = "Pickup & Counter Instructions (Optional)")]
    public string? PickupInstructions { get; set; }

    public string ToFullFormattedAddress() =>
        IndianLocations.FormatFullAddress(AddressLine1, AddressLine2, Landmark, City, District, State, PostalCode, Country);
}
