using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Catalog;

namespace RentKart.Web.Controllers;

public class CatalogController : Controller
{
    private readonly IEquipmentCatalogService _catalogService;
    private readonly IWishlistService _wishlistService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CatalogController> _logger;

    public CatalogController(
        IEquipmentCatalogService catalogService,
        IWishlistService wishlistService,
        UserManager<ApplicationUser> userManager,
        ILogger<CatalogController> logger)
    {
        _catalogService = catalogService;
        _wishlistService = wishlistService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Route("Catalog")]
    [Route("Equipment")]
    public async Task<IActionResult> Index([FromQuery] EquipmentCatalogQuery query)
    {
        // Sanitize and guard user inputs
        query.Sanitize();

        var criteria = new CatalogFilterCriteria
        {
            Search = query.Search,
            CategoryId = query.CategoryId,
            CategorySlug = query.CategorySlug,
            City = query.City,
            MinPrice = query.MinPrice,
            MaxPrice = query.MaxPrice,
            Condition = query.Condition,
            SortBy = query.SortBy,
            Page = query.Page,
            PageSize = query.PageSize
        };

        var result = await _catalogService.SearchCatalogAsync(criteria);

        // Check customer wishlist status if signed in
        var wishlistIds = new HashSet<int>();
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(userId))
        {
            wishlistIds = await _wishlistService.GetUserWishlistEquipmentIdsAsync(userId);
        }

        var items = result.Items.Select(e => new EquipmentListItemViewModel
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
        }).ToList();

        var categories = await _catalogService.GetCategoriesWithCountsAsync();
        var availableCities = await _catalogService.GetAvailableCitiesAsync();

        string? currentCategoryName = null;
        if (!string.IsNullOrEmpty(query.CategorySlug))
        {
            currentCategoryName = categories.FirstOrDefault(c => c.Slug.Equals(query.CategorySlug, StringComparison.OrdinalIgnoreCase))?.Name;
        }
        else if (query.CategoryId.HasValue)
        {
            currentCategoryName = categories.FirstOrDefault(c => c.Id == query.CategoryId.Value)?.Name;
        }

        var model = new EquipmentCatalogViewModel
        {
            Query = query,
            Items = items,
            TotalCount = result.TotalCount,
            AvailableCategories = categories.Select(c => new CategoryOptionViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Slug = c.Slug,
                IconClass = c.IconClass,
                ActiveListingsCount = c.Equipments.Count
            }).ToList(),
            AvailableCities = availableCities,
            CurrentCategoryName = currentCategoryName
        };

        return View(model);
    }

    [HttpGet]
    [Route("Catalog/Details/{id:int}")]
    [Route("Equipment/{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var currentUserId = _userManager.GetUserId(User);
        var isAdmin = User.IsInRole(IdentityDataSeeder.RoleAdmin);

        // Fetch equipment: if user is admin, allow inactive inspection; otherwise fetch active only
        var equipment = await _catalogService.GetEquipmentDetailsAsync(id, includeInactive: isAdmin);

        // Check if owner preview is needed
        if (equipment == null && !string.IsNullOrEmpty(currentUserId))
        {
            var previewCandidate = await _catalogService.GetEquipmentDetailsAsync(id, includeInactive: true);
            if (previewCandidate != null && previewCandidate.Business.UserId == currentUserId)
            {
                equipment = previewCandidate;
            }
        }

        if (equipment == null)
        {
            return NotFound();
        }

        var isOwner = !string.IsNullOrEmpty(currentUserId) && equipment.Business.UserId == currentUserId;
        var isPreview = !equipment.IsActive 
                     || equipment.Status != EquipmentStatus.Active 
                     || equipment.Business.Status != BusinessStatus.Approved;

        var isInWishlist = false;
        if (!string.IsNullOrEmpty(currentUserId))
        {
            isInWishlist = await _wishlistService.IsInWishlistAsync(currentUserId, equipment.Id);
        }

        var model = new EquipmentDetailViewModel
        {
            Id = equipment.Id,
            Title = equipment.Title,
            Description = equipment.Description,
            CategoryId = equipment.CategoryId,
            CategoryName = equipment.Category.Name,
            CategorySlug = equipment.Category.Slug,
            DailyRentalRate = equipment.DailyRentalRate,
            SecurityDeposit = equipment.SecurityDeposit,
            TotalQuantity = equipment.TotalQuantity,
            Condition = equipment.Condition,
            Status = equipment.Status,
            IsActive = equipment.IsActive,
            CreatedAtUtc = equipment.CreatedAtUtc,
            ImageUrls = equipment.Images.Select(i => i.ImageUrl).ToList(),
            BusinessId = equipment.BusinessId,
            ShopName = equipment.Business.ShopName,
            ContactPhone = equipment.Business.ContactPhone,
            AddressLine = equipment.Business.AddressLine,
            City = equipment.Business.City,
            State = equipment.Business.State,
            PostalCode = equipment.Business.PostalCode,
            OperatingHours = equipment.Business.OperatingHours,
            PickupInstructions = equipment.Business.PickupInstructions,
            BusinessStatus = equipment.Business.Status,
            IsInWishlist = isInWishlist,
            IsPreview = isPreview
        };

        return View(model);
    }

    [HttpGet]
    [Route("Catalog/Category/{slug}")]
    public IActionResult Category(string slug)
    {
        return RedirectToAction(nameof(Index), new { categorySlug = slug });
    }
}
