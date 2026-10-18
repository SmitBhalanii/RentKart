using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Controllers;

public class CartController : Controller
{
    private readonly ICartService _cartService;

    public CartController(ICartService cartService)
    {
        _cartService = cartService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        return View(cart);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate, string? returnUrl = null)
    {
        var result = await _cartService.AddToCartAsync(equipmentId, quantity, startDate, endDate);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(nameof(Index));
        }

        TempData["SuccessMessage"] = result.Message ?? "Item added to rental cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int equipmentId, int quantity, DateOnly startDate, DateOnly endDate)
    {
        var result = await _cartService.UpdateCartItemAsync(equipmentId, quantity, startDate, endDate);
        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Message;
        }
        else
        {
            TempData["SuccessMessage"] = "Cart item updated.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int equipmentId)
    {
        await _cartService.RemoveFromCartAsync(equipmentId);
        TempData["SuccessMessage"] = "Item removed from cart.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Clear()
    {
        await _cartService.ClearCartAsync();
        TempData["SuccessMessage"] = "Rental cart emptied.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Count()
    {
        int count = await _cartService.GetCartItemCountAsync();
        return Json(new { count });
    }
}
