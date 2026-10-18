using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Controllers;

[Authorize(Roles = "Customer")]
public class BookingController : Controller
{
    private readonly IBookingService _bookingService;

    public BookingController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpGet]
    public async Task<IActionResult> MyBookings(int page = 1)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var (items, totalCount) = await _bookingService.GetCustomerBookingsAsync(customerUserId, page, pageSize: 10);
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 10.0);
        ViewBag.TotalCount = totalCount;

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var booking = await _bookingService.GetCustomerBookingDetailsAsync(id, customerUserId);
        if (booking == null)
        {
            return NotFound();
        }

        return View(booking);
    }

    [HttpGet]
    public async Task<IActionResult> Confirmation(int id)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var booking = await _bookingService.GetCustomerBookingDetailsAsync(id, customerUserId);
        if (booking == null)
        {
            return NotFound();
        }

        return View(booking);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelGroup(int id, int bookingId, string? reason)
    {
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(customerUserId))
        {
            return Challenge();
        }

        var result = await _bookingService.CancelBookingGroupByCustomerAsync(id, customerUserId, reason);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.ErrorMessage ?? "Could not cancel booking group.";
        }
        else
        {
            TempData["SuccessMessage"] = "Reservation cancelled. Reserved inventory units have been released.";
        }

        return RedirectToAction(nameof(Details), new { id = bookingId });
    }
}
