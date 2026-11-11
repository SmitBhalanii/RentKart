using System.Globalization;

namespace RentKart.Core.Utilities;

/// <summary>
/// Authoritative Indian Currency (₹ INR) formatting helper.
/// Provides standardized en-IN culture currency formatting across all views, ViewModels, and services.
/// </summary>
public static class CurrencyHelper
{
    public const string CurrencySymbol = "₹";
    public const string CurrencyCode = "INR";
    private static readonly CultureInfo IndianCulture = new("en-IN");

    /// <summary>
    /// Formats a decimal amount to Indian Rupees string, e.g. ₹1,250.00, ₹25,000.00
    /// </summary>
    public static string Format(decimal amount, bool includeSymbol = true)
    {
        var formatted = amount.ToString("N2", IndianCulture);
        return includeSymbol ? $"{CurrencySymbol}{formatted}" : formatted;
    }

    /// <summary>
    /// Formats a nullable decimal amount to Indian Rupees string, defaulting to ₹0.00 (or optional fallback like "—") if null.
    /// </summary>
    public static string Format(decimal? amount, bool includeSymbol = true, string? nullFallback = null)
    {
        if (!amount.HasValue)
        {
            return nullFallback ?? (includeSymbol ? $"{CurrencySymbol}0.00" : "0.00");
        }
        return Format(amount.Value, includeSymbol);
    }
}

/// <summary>
/// Extension methods for clean currency formatting in C# and Razor templates.
/// </summary>
public static class CurrencyExtensions
{
    public static string ToInr(this decimal amount, bool includeSymbol = true) =>
        CurrencyHelper.Format(amount, includeSymbol);

    public static string ToInr(this decimal? amount, bool includeSymbol = true, string? nullFallback = null) =>
        CurrencyHelper.Format(amount, includeSymbol, nullFallback);
}

