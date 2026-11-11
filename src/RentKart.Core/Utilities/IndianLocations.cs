using System.Text.RegularExpressions;

namespace RentKart.Core.Utilities;

/// <summary>
/// Authoritative Indian geography constants, address formatting, and PIN code validation utilities.
/// Supports all 28 States and 8 Union Territories across the Republic of India.
/// </summary>
public static class IndianLocations
{
    public const string DefaultCountryCode = "IN";
    public const string DefaultCountryName = "India";

    /// <summary>
    /// Complete list of 28 States and 8 Union Territories in India (36 administrative entities).
    /// </summary>
    public static readonly IReadOnlyList<string> StatesAndUnionTerritories = new List<string>
    {
        // 28 States
        "Andhra Pradesh",
        "Arunachal Pradesh",
        "Assam",
        "Bihar",
        "Chhattisgarh",
        "Goa",
        "Gujarat",
        "Haryana",
        "Himachal Pradesh",
        "Jharkhand",
        "Karnataka",
        "Kerala",
        "Madhya Pradesh",
        "Maharashtra",
        "Manipur",
        "Meghalaya",
        "Mizoram",
        "Nagaland",
        "Odisha",
        "Punjab",
        "Rajasthan",
        "Sikkim",
        "Tamil Nadu",
        "Telangana",
        "Tripura",
        "Uttar Pradesh",
        "Uttarakhand",
        "West Bengal",

        // 8 Union Territories
        "Andaman and Nicobar Islands",
        "Chandigarh",
        "Dadra and Nagar Haveli and Daman and Diu",
        "Delhi (NCT)",
        "Jammu and Kashmir",
        "Ladakh",
        "Lakshadweep",
        "Puducherry"
    };

    /// <summary>
    /// Validates standard 6-digit Indian Postal Index Number (PIN Code).
    /// Leading digit must be 1-9 (110001 - 855117).
    /// </summary>
    public static bool IsValidPinCode(string? pinCode)
    {
        if (string.IsNullOrWhiteSpace(pinCode))
            return false;

        var cleaned = pinCode.Trim().Replace(" ", "");
        return Regex.IsMatch(cleaned, @"^[1-9][0-9]{5}$");
    }

    /// <summary>
    /// Formats a complete, professional Indian address according to national postal conventions:
    /// [Address Line 1], [Address Line 2], [Landmark], [City/Town], [District], [State] - [PIN Code], [Country]
    /// </summary>
    public static string FormatFullAddress(
        string addressLine1,
        string? addressLine2,
        string? landmark,
        string city,
        string? district,
        string state,
        string postalCode,
        string country = DefaultCountryName)
    {
        var segments = new List<string>();

        if (!string.IsNullOrWhiteSpace(addressLine1))
            segments.Add(addressLine1.Trim());

        if (!string.IsNullOrWhiteSpace(addressLine2))
            segments.Add(addressLine2.Trim());

        if (!string.IsNullOrWhiteSpace(landmark))
            segments.Add(landmark.Trim().StartsWith("Near ", StringComparison.OrdinalIgnoreCase) ||
                         landmark.Trim().StartsWith("Opp. ", StringComparison.OrdinalIgnoreCase) ||
                         landmark.Trim().StartsWith("Behind ", StringComparison.OrdinalIgnoreCase)
                         ? landmark.Trim()
                         : $"Near {landmark.Trim()}");

        if (!string.IsNullOrWhiteSpace(city))
            segments.Add(city.Trim());

        if (!string.IsNullOrWhiteSpace(district) &&
            !string.Equals(district.Trim(), city.Trim(), StringComparison.OrdinalIgnoreCase))
            segments.Add(district.Trim());

        var statePin = $"{state.Trim()} - {postalCode.Trim()}";
        segments.Add(statePin);

        var countryDisplay = string.Equals(country.Trim(), "IN", StringComparison.OrdinalIgnoreCase)
            ? DefaultCountryName
            : country.Trim();

        segments.Add(countryDisplay);

        return string.Join(", ", segments);
    }
}

