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
}
