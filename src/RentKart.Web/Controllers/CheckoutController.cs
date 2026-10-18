using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Controllers;

[Authorize(Roles = "Customer")]
public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly IBookingService _bookingService;

    public CheckoutController(ICartService cartService, IBookingService bookingService)
    {
        _cartService = cartService;
        _bookingService = bookingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        if (cart.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "Your cart is currently empty. Please add equipment before checking out.";
            return RedirectToAction("Index", "Cart");
        }

        if (cart.HasUnavailableItems)
        {
            TempData["ErrorMessage"] = "Some items in your cart have insufficient inventory or have become unavailable. Please adjust dates or quantities.";
            return RedirectToAction("Index", "Cart");
        }

        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitOrder(string? customerNotes, bool acceptTerms)
    {
        if (!acceptTerms)
        {
            ModelState.AddModelError("Terms", "You must agree to the in-store pickup and return inspection terms to continue.");
            var cartForTerms = await _cartService.GetCartAsync();
            return View("Index", cartForTerms);
        }

        var cart = await _cartService.GetCartAsync();
        if (cart.Items.Count == 0)
        {
            TempData["ErrorMessage"] = "Your cart is empty.";
            return RedirectToAction("Index", "Cart");
        }

        if (cart.HasUnavailableItems)
        {
            TempData["ErrorMessage"] = "One or more items in your cart cannot be booked due to availability conflicts.";
            return RedirectToAction("Index", "Cart");
        }

        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var requestDto = new CreateBookingRequestDto
        {
            CustomerNotes = customerNotes,
            Items = cart.Items.Select(i => new CreateBookingItemRequestDto
            {
                EquipmentId = i.EquipmentId,
                Quantity = i.Quantity,
                StartDate = i.StartDate,
                EndDate = i.EndDate
            }).ToList()
        };

        var result = await _bookingService.CreateBookingAsync(customerUserId, requestDto);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not complete booking.";
            return View("Index", cart);
        }

        // Clear cart after confirmed booking placement
        await _cartService.ClearCartAsync();

        TempData["SuccessMessage"] = $"Your booking voucher #{result.BookingNumber} has been created!";
        return RedirectToAction("Confirmation", "Booking", new { id = result.BookingId });
    }
}
