using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class CatalogFilterCriteria
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
}

public class EquipmentCatalogResult
{
    public List<Equipment> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}

public interface IEquipmentCatalogService
{
    Task<EquipmentCatalogResult> SearchCatalogAsync(CatalogFilterCriteria criteria);
    Task<Equipment?> GetEquipmentDetailsAsync(int id, bool includeInactive = false);
    Task<BusinessProfile?> GetStorefrontAsync(int businessId, bool includeUnapproved = false);
    Task<List<Category>> GetCategoriesWithCountsAsync();
    Task<List<string>> GetAvailableCitiesAsync();
    Task<List<Equipment>> GetFeaturedEquipmentAsync(int count = 6);
}
