using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class EquipmentCatalogService : IEquipmentCatalogService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<EquipmentCatalogService> _logger;

    public EquipmentCatalogService(ApplicationDbContext context, ILogger<EquipmentCatalogService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<EquipmentCatalogResult> SearchCatalogAsync(CatalogFilterCriteria criteria)
    {
        // Authoritative requirement: Only active listings belonging to approved businesses
        var query = _context.Equipments
            .AsNoTracking()
            .Include(e => e.Business)
            .Include(e => e.Category)
            .Include(e => e.Images)
            .Where(e => e.IsActive 
                     && e.Status == EquipmentStatus.Active 
                     && e.Business.Status == BusinessStatus.Approved);

        // 1. Keyword search (Title and Description)
        if (!string.IsNullOrWhiteSpace(criteria.Search))
        {
            var search = criteria.Search.Trim().ToLower();
            query = query.Where(e => e.Title.ToLower().Contains(search) 
                                  || e.Description.ToLower().Contains(search));
        }

        // 2. Category filter (by ID or Slug)
        if (criteria.CategoryId.HasValue && criteria.CategoryId.Value > 0)
        {
            query = query.Where(e => e.CategoryId == criteria.CategoryId.Value);
        }
        else if (!string.IsNullOrWhiteSpace(criteria.CategorySlug))
        {
            var slug = criteria.CategorySlug.Trim().ToLower();
            query = query.Where(e => e.Category.Slug.ToLower() == slug);
        }

        // 3. Location filter (City)
        if (!string.IsNullOrWhiteSpace(criteria.City))
        {
            var city = criteria.City.Trim().ToLower();
            query = query.Where(e => e.Business.City.ToLower() == city);
        }

        // 4. Price range filter
        if (criteria.MinPrice.HasValue)
        {
            query = query.Where(e => e.DailyRentalRate >= criteria.MinPrice.Value);
        }
        if (criteria.MaxPrice.HasValue)
        {
            query = query.Where(e => e.DailyRentalRate <= criteria.MaxPrice.Value);
        }

        // 5. Condition filter
        if (criteria.Condition.HasValue)
        {
            query = query.Where(e => e.Condition == criteria.Condition.Value);
        }

        // Count total matching items before pagination
        var totalCount = await query.CountAsync();

        // 6. Safe sorting
        query = criteria.SortBy?.ToLowerInvariant() switch
        {
            "price_asc" => query.OrderBy(e => e.DailyRentalRate).ThenByDescending(e => e.CreatedAtUtc),
            "price_desc" => query.OrderByDescending(e => e.DailyRentalRate).ThenByDescending(e => e.CreatedAtUtc),
            "title_asc" => query.OrderBy(e => e.Title),
            _ => query.OrderByDescending(e => e.CreatedAtUtc)
        };

        // 7. Server-side pagination
        var page = criteria.Page < 1 ? 1 : criteria.Page;
        var pageSize = criteria.PageSize is < 1 or > 48 ? 12 : criteria.PageSize;

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new EquipmentCatalogResult
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<Equipment?> GetEquipmentDetailsAsync(int id, bool includeInactive = false)
    {
        var query = _context.Equipments
            .AsNoTracking()
            .Include(e => e.Business)
            .Include(e => e.Category)
            .Include(e => e.Images.OrderBy(i => i.DisplayOrder))
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive 
                                  && e.Status == EquipmentStatus.Active 
                                  && e.Business.Status == BusinessStatus.Approved);
        }

        return await query.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<BusinessProfile?> GetStorefrontAsync(int businessId, bool includeUnapproved = false)
    {
        var query = _context.BusinessProfiles
            .AsNoTracking()
            .Include(b => b.User)
            .Include(b => b.Equipments.Where(e => e.IsActive && e.Status == EquipmentStatus.Active))
                .ThenInclude(e => e.Category)
            .Include(b => b.Equipments.Where(e => e.IsActive && e.Status == EquipmentStatus.Active))
                .ThenInclude(e => e.Images)
            .AsQueryable();

        if (!includeUnapproved)
        {
            query = query.Where(b => b.Status == BusinessStatus.Approved);
        }

        return await query.FirstOrDefaultAsync(b => b.Id == businessId);
    }

    public async Task<List<Category>> GetCategoriesWithCountsAsync()
    {
        return await _context.Categories
            .AsNoTracking()
            .Where(c => c.IsActive)
            .Include(c => c.Equipments.Where(e => e.IsActive && e.Status == EquipmentStatus.Active && e.Business.Status == BusinessStatus.Approved))
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();
    }

    public async Task<List<string>> GetAvailableCitiesAsync()
    {
        return await _context.BusinessProfiles
            .AsNoTracking()
            .Where(b => b.Status == BusinessStatus.Approved)
            .Select(b => b.City)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync();
    }

    public async Task<List<Equipment>> GetFeaturedEquipmentAsync(int count = 6)
    {
        return await _context.Equipments
            .AsNoTracking()
            .Include(e => e.Business)
            .Include(e => e.Category)
            .Include(e => e.Images)
            .Where(e => e.IsActive 
                     && e.Status == EquipmentStatus.Active 
                     && e.Business.Status == BusinessStatus.Approved)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(count)
            .ToListAsync();
    }
}
