using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Catalog;

namespace RentKart.Web.Controllers;

public class StoreController : Controller
{
    private readonly IEquipmentCatalogService _catalogService;
    private readonly IWishlistService _wishlistService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<StoreController> _logger;

    public StoreController(
        IEquipmentCatalogService catalogService,
        IWishlistService wishlistService,
        UserManager<ApplicationUser> userManager,
        ILogger<StoreController> logger)
    {
        _catalogService = catalogService;
        _wishlistService = wishlistService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Route("Store/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(IdentityDataSeeder.RoleAdmin);

        var business = await _catalogService.GetStorefrontAsync(id, includeUnapproved: isAdmin);
        if (business == null && !string.IsNullOrEmpty(currentUserId))
        {
            var candidate = await _catalogService.GetStorefrontAsync(id, includeUnapproved: true);
            if (candidate != null && candidate.UserId == currentUserId)
            {
                business = candidate;
            }
        }

        if (business == null)
        {
            return NotFound();
        }

        var wishlistIds = new HashSet<int>();
        if (!string.IsNullOrEmpty(currentUserId))
        {
            wishlistIds = await _wishlistService.GetUserWishlistEquipmentIdsAsync(currentUserId);
        }

        var listings = business.Equipments
            .Where(e => e.IsActive && e.Status == EquipmentStatus.Active)
            .Select(e => new EquipmentListItemViewModel
            {
                Id = e.Id,
                Title = e.Title,
                CategoryName = e.Category.Name,
                CategorySlug = e.Category.Slug,
                BusinessId = business.Id,
                ShopName = business.ShopName,
                City = business.City,
                State = business.State,
                DailyRentalRate = e.DailyRentalRate,
                SecurityDeposit = e.SecurityDeposit,
                TotalQuantity = e.TotalQuantity,
                Condition = e.Condition,
                PrimaryImageUrl = e.Images.FirstOrDefault(i => i.IsPrimary)?.ImageUrl 
                               ?? e.Images.FirstOrDefault()?.ImageUrl,
                IsInWishlist = wishlistIds.Contains(e.Id)
            }).ToList();

        var model = new BusinessStorefrontViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            RegistrationNumber = business.RegistrationNumber,
            ContactPhone = business.ContactPhone,
            AddressLine = business.AddressLine,
            City = business.City,
            State = business.State,
            PostalCode = business.PostalCode,
            OperatingHours = business.OperatingHours,
            PickupInstructions = business.PickupInstructions,
            Status = business.Status,
            CreatedAtUtc = business.CreatedAtUtc,
            ApprovedAtUtc = business.ApprovedAtUtc,
            Listings = listings
        };

        return View(model);
    }
}
