using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Core.Validation;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Customer;

namespace RentKart.Web.Controllers;

[Authorize(Roles = $"{IdentityDataSeeder.RoleCustomer},{IdentityDataSeeder.RoleAdmin}")]
public class CustomerController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBookingService _bookingService;
    private readonly IWishlistService _wishlistService;
    private readonly ILogger<CustomerController> _logger;

    public CustomerController(
        UserManager<ApplicationUser> userManager,
        IBookingService bookingService,
        IWishlistService wishlistService,
        ILogger<CustomerController> logger)
    {
        _userManager = userManager;
        _bookingService = bookingService;
        _wishlistService = wishlistService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        // Fetch real customer bookings
        var (bookings, totalCount) = await _bookingService.GetCustomerBookingsAsync(user.Id, page: 1, pageSize: 20);

        // Fetch real customer wishlist items
        var wishlist = await _wishlistService.GetUserWishlistAsync(user.Id);

        var model = new CustomerDashboardViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            MemberSinceUtc = user.CreatedAtUtc,
            TotalBookingsCount = totalCount,
            ActiveRentalsCount = bookings.Count(b => b.Status == BookingStatus.Active || b.Groups.Any(g => g.Status == BookingGroupStatus.HandedOver)),
            PendingRequestsCount = bookings.Count(b => b.Status == BookingStatus.Pending || b.Groups.Any(g => g.Status == BookingGroupStatus.PendingApproval)),
            ConfirmedCount = bookings.Count(b => b.Status == BookingStatus.Confirmed || b.Groups.Any(g => g.Status == BookingGroupStatus.Confirmed)),
            CompletedRentalsCount = bookings.Count(b => b.Status == BookingStatus.Completed),
            WishlistItemsCount = wishlist.Count,
            RecentBookings = bookings.Take(5).ToList(),
            WishlistPreview = wishlist.Take(4).Select(w => new CustomerWishlistPreviewItem
            {
                EquipmentId = w.EquipmentId,
                Title = w.Equipment?.Title ?? "Equipment",
                CategoryName = w.Equipment?.Category?.Name ?? "General",
                ShopName = w.Equipment?.Business?.ShopName ?? "Rental Store",
                DailyRentalRate = w.Equipment?.DailyRentalRate ?? 0m
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var model = new CustomerProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CustomerProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            model.Email = user.Email ?? string.Empty;
            return View(model);
        }

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = IndianPhoneNumberAttribute.Normalize(model.PhoneNumber);
        user.UpdatedAtUtc = DateTime.UtcNow;

        var result = await _userManager.UpdateAsync(user);
        if (result.Succeeded)
        {
            _logger.LogInformation("Customer profile updated for user {UserId}.", user.Id);
            TempData["SuccessMessage"] = "Your profile has been updated successfully.";
            return RedirectToAction(nameof(Profile));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }

        model.Email = user.Email ?? string.Empty;
        return View(model);
    }
}
