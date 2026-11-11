using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace RentKart.Core.Validation;

/// <summary>
/// Validates Indian 10-digit mobile numbers with optional +91 or 0 country/trunk prefixes.
/// Accepts formats: 9876543210, +91 98765 43210, +91-9876543210, 09876543210.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class IndianPhoneNumberAttribute : ValidationAttribute
{
    // Matches optional +91 or 0 prefix, followed by 10 digits starting with 6, 7, 8, or 9
    private static readonly Regex IndianPhoneRegex = new(
        @"^(?:(?:\+|0{0,2})91(\s*[\-]\s*)?|[0]?)?[6789]\d{9}$",
        RegexOptions.Compiled);

    public IndianPhoneNumberAttribute()
        : base("Please enter a valid 10-digit Indian mobile number (e.g., +91 98765 43210 or 9876543210).")
    {
    }

    public override bool IsValid(object? value)
    {
        if (value == null) return true; // Let [Required] handle null/empty checks

        var str = value as string;
        if (string.IsNullOrWhiteSpace(str)) return true;

        // Clean spaces and hyphens for regex evaluation
        var cleaned = Regex.Replace(str.Trim(), @"[\s\-\(\)]", "");
        return IndianPhoneRegex.IsMatch(cleaned);
    }

    /// <summary>
    /// Normalizes an Indian phone number string into clean standard format: +91 XXXXX XXXXX
    /// </summary>
    public static string Normalize(string rawPhone)
    {
        if (string.IsNullOrWhiteSpace(rawPhone)) return string.Empty;

        var digitsOnly = Regex.Replace(rawPhone, @"\D", "");
        if (digitsOnly.StartsWith("91") && digitsOnly.Length == 12)
        {
            digitsOnly = digitsOnly[2..];
        }
        else if (digitsOnly.StartsWith("0") && digitsOnly.Length == 11)
        {
            digitsOnly = digitsOnly[1..];
        }

        if (digitsOnly.Length == 10)
        {
            return $"+91 {digitsOnly[..5]} {digitsOnly[5..]}";
        }

        return rawPhone.Trim();
    }
}

