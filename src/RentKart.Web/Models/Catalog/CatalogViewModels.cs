using RentKart.Core.Enums;

namespace RentKart.Web.Models.Catalog;

public class EquipmentCatalogQuery
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public string? CategorySlug { get; set; }
    public string? City { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public EquipmentCondition? Condition { get; set; }
    public string SortBy { get; set; } = "newest";
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;

    public void Sanitize()
    {
        if (Page < 1) Page = 1;
        if (PageSize is < 1 or > 48) PageSize = 12;
        if (MinPrice.HasValue && MinPrice.Value < 0) MinPrice = 0;
        if (MaxPrice.HasValue && MaxPrice.Value < 0) MaxPrice = 0;
        if (MinPrice.HasValue && MaxPrice.HasValue && MaxPrice.Value < MinPrice.Value)
        {
            (MinPrice, MaxPrice) = (MaxPrice, MinPrice);
        }

        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim();
        City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
        CategorySlug = string.IsNullOrWhiteSpace(CategorySlug) ? null : CategorySlug.Trim();

        var allowedSorts = new[] { "newest", "price_asc", "price_desc", "title_asc" };
        if (!allowedSorts.Contains(SortBy?.ToLowerInvariant()))
        {
            SortBy = "newest";
        }
    }
}

public class EquipmentListItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; }
    public EquipmentCondition Condition { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public bool IsInWishlist { get; set; }
}

public class CategoryOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? IconClass { get; set; }
    public int ActiveListingsCount { get; set; }
}

public class EquipmentCatalogViewModel
{
    public EquipmentCatalogQuery Query { get; set; } = new();
    public List<EquipmentListItemViewModel> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / Query.PageSize);
    public int CurrentPage => Query.Page;
    public bool HasPreviousPage => CurrentPage > 1;
    public bool HasNextPage => CurrentPage < TotalPages;

    public List<CategoryOptionViewModel> AvailableCategories { get; set; } = new();
    public List<string> AvailableCities { get; set; } = new();
    public string? CurrentCategoryName { get; set; }
}

public class EquipmentDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; }
    public EquipmentCondition Condition { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public List<string> ImageUrls { get; set; } = new();

    // Business & Physical Store Details
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public string? PickupInstructions { get; set; }
    public BusinessStatus BusinessStatus { get; set; }

    public bool IsInWishlist { get; set; }
    public bool IsPreview { get; set; }
}

public class BusinessStorefrontViewModel
{
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string? RegistrationNumber { get; set; }
    public string ContactPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public string? PickupInstructions { get; set; }
    public BusinessStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public List<EquipmentListItemViewModel> Listings { get; set; } = new();
}

public class WishlistViewModel
{
    public List<WishlistItemViewModel> Items { get; set; } = new();
    public int TotalItems => Items.Count;
}

public class WishlistItemViewModel
{
    public int WishlistId { get; set; }
    public int EquipmentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategorySlug { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public EquipmentCondition Condition { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public DateTime AddedAtUtc { get; set; }
}
