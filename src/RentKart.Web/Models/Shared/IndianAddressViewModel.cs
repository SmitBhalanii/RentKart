using System.ComponentModel.DataAnnotations;
using RentKart.Core.Utilities;

namespace RentKart.Web.Models.Shared;

/// <summary>
/// Reusable Indian address ViewModel supporting all 28 States and 8 Union Territories.
/// Ensures consistent field names, labels, validations, and formatting across Customer, Business, and Admin interfaces.
/// </summary>
public class IndianAddressViewModel
{
    [Required(ErrorMessage = "Address Line 1 is required.")]
    [StringLength(200, ErrorMessage = "Address Line 1 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 1 (Flat, House / Shop No., Building, Complex)")]
    public string AddressLine1 { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "Address Line 2 cannot exceed 200 characters.")]
    [Display(Name = "Address Line 2 (Street, Sector, Area, Locality)")]
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
    [StringLength(100, ErrorMessage = "State cannot exceed 100 characters.")]
    [Display(Name = "State / Union Territory")]
    public string State { get; set; } = string.Empty;

    [Required(ErrorMessage = "PIN Code is required.")]
    [RegularExpression(@"^[1-9][0-9]{5}$", ErrorMessage = "Please enter a valid 6-digit Indian PIN code (e.g. 400053).")]
    [Display(Name = "PIN Code")]
    public string PostalCode { get; set; } = string.Empty;

    [Display(Name = "Country")]
    public string Country { get; set; } = IndianLocations.DefaultCountryCode;

    public string ToFullFormattedAddress() =>
        IndianLocations.FormatFullAddress(AddressLine1, AddressLine2, Landmark, City, District, State, PostalCode, Country);
}

