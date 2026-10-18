using System.ComponentModel.DataAnnotations;

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

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Phone Number")]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long.")]
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
    [Display(Name = "Registration / License Number (Optional)")]
    public string? RegistrationNumber { get; set; }

    [Required(ErrorMessage = "Contact phone number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [Display(Name = "Store Phone Number")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Store address is required for customer equipment pickups.")]
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

    [Required(ErrorMessage = "Operating hours are required for pickup and return scheduling.")]
    [StringLength(100, ErrorMessage = "Operating hours cannot exceed 100 characters.")]
    [Display(Name = "Operating Hours (e.g. Mon-Sat: 09:00 - 18:00)")]
    public string OperatingHours { get; set; } = "Mon-Sat: 09:00 - 18:00";

    [StringLength(500, ErrorMessage = "Pickup instructions cannot exceed 500 characters.")]
    [Display(Name = "Pickup & Counter Instructions (Optional)")]
    public string? PickupInstructions { get; set; }
}
