using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Interfaces;
using RentKart.Web.Models;
using RentKart.Web.Models.Catalog;

namespace RentKart.Web.Controllers;

public class HomeController : Controller
{
    private readonly IEquipmentCatalogService _catalogService;
    private readonly IWishlistService _wishlistService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IEquipmentCatalogService catalogService,
        IWishlistService wishlistService,
        UserManager<ApplicationUser> userManager,
        ILogger<HomeController> logger)
    {
        _catalogService = catalogService;
        _wishlistService = wishlistService;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _catalogService.GetCategoriesWithCountsAsync();
        var featured = await _catalogService.GetFeaturedEquipmentAsync(6);
        var cities = await _catalogService.GetAvailableCitiesAsync();

        var wishlistIds = new HashSet<int>();
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(userId))
        {
            wishlistIds = await _wishlistService.GetUserWishlistEquipmentIdsAsync(userId);
        }

        var model = new HomeIndexViewModel
        {
            Categories = categories.Select(c => new CategoryOptionViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                IconClass = c.IconClass,
                ActiveListingsCount = c.Equipments.Count
            }).ToList(),
            FeaturedListings = featured.Select(e => new EquipmentListItemViewModel
            {
                Id = e.Id,
                Title = e.Title,
                CategoryName = e.Category.Name,
                CategorySlug = e.Category.Slug,
                BusinessId = e.BusinessId,
                ShopName = e.Business.ShopName,
                City = e.Business.City,
                State = e.Business.State,
                DailyRentalRate = e.DailyRentalRate,
                SecurityDeposit = e.SecurityDeposit,
                TotalQuantity = e.TotalQuantity,
                Condition = e.Condition,
                PrimaryImageUrl = e.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                               ?? e.Images.FirstOrDefault()?.ImageUrl,
                IsInWishlist = wishlistIds.Contains(e.Id)
            }).ToList(),
            AvailableCities = cities
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
