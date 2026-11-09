using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Reviews;

namespace RentKart.Web.Controllers;

[Authorize(Roles = IdentityDataSeeder.RoleCustomer)]
public class ReviewsController : Controller
{
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewService reviewService, ILogger<ReviewsController> logger)
    {
        _reviewService = reviewService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int bookingItemId)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var eligibility = await _reviewService.CheckEligibilityAsync(bookingItemId, customerUserId);
        if (!eligibility.CanReview)
        {
            TempData["ErrorMessage"] = eligibility.Reason ?? "You are not eligible to review this rental.";
            return RedirectToAction("MyBookings", "Booking");
        }

        var model = new ReviewCreateViewModel
        {
            BookingItemId = eligibility.BookingItemId,
            EquipmentId = eligibility.EquipmentId,
            EquipmentTitle = eligibility.EquipmentTitle,
            PrimaryImageUrl = eligibility.PrimaryImageUrl,
            CategoryName = eligibility.CategoryName,
            ShopName = eligibility.ShopName,
            DailyRentalRate = eligibility.DailyRentalRate,
            StartDate = eligibility.StartDate,
            EndDate = eligibility.EndDate,
            DaysCount = eligibility.DaysCount,
            Quantity = eligibility.Quantity,
            Rating = 5
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReviewCreateViewModel model)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new SubmitReviewRequestDto
        {
            BookingItemId = model.BookingItemId,
            Rating = model.Rating,
            Comment = model.Comment
        };

        var result = await _reviewService.SubmitReviewAsync(request, customerUserId);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "Could not submit review.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Thank you! Your verified review has been published.";
        return RedirectToAction("Details", "Catalog", new { id = model.EquipmentId });
    }
}

