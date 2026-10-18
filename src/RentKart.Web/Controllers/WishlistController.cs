using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Catalog;

namespace RentKart.Web.Controllers;

[Authorize(Roles = $"{IdentityDataSeeder.RoleCustomer},{IdentityDataSeeder.RoleAdmin}")]
public class WishlistController : Controller
{
    private readonly IWishlistService _wishlistService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WishlistController> _logger;

    public WishlistController(
        IWishlistService wishlistService,
        UserManager<ApplicationUser> userManager,
        ILogger<WishlistController> logger)
    {
        _wishlistService = wishlistService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var wishlistItems = await _wishlistService.GetUserWishlistAsync(userId);

        var model = new WishlistViewModel
        {
            Items = wishlistItems.Select(w => new WishlistItemViewModel
            {
                WishlistId = w.Id,
                EquipmentId = w.EquipmentId,
                Title = w.Equipment.Title,
                CategoryName = w.Equipment.Category.Name,
                CategorySlug = w.Equipment.Category.Slug,
                BusinessId = w.Equipment.BusinessId,
                ShopName = w.Equipment.Business.ShopName,
                City = w.Equipment.Business.City,
                State = w.Equipment.Business.State,
                DailyRentalRate = w.Equipment.DailyRentalRate,
                SecurityDeposit = w.Equipment.SecurityDeposit,
                Condition = w.Equipment.Condition,
                PrimaryImageUrl = w.Equipment.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                               ?? w.Equipment.Images.FirstOrDefault()?.ImageUrl,
                AddedAtUtc = w.AddedAtUtc
            }).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int equipmentId, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        var inWishlist = await _wishlistService.ToggleWishlistAsync(userId, equipmentId);

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
        {
            return Json(new { success = true, inWishlist });
        }

        TempData["SuccessMessage"] = inWishlist 
            ? "Equipment added to your wishlist." 
            : "Equipment removed from your wishlist.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int equipmentId, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrEmpty(userId))
        {
            return Challenge();
        }

        await _wishlistService.RemoveFromWishlistAsync(userId, equipmentId);
        TempData["InfoMessage"] = "Item removed from your wishlist.";

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
    }
}
