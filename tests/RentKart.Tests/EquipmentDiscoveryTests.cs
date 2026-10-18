using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using Xunit;

namespace RentKart.Tests;

public class EquipmentDiscoveryTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public EquipmentDiscoveryTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await CatalogDataSeeder.SeedCatalogDataAsync(scope.ServiceProvider);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Catalog_ReturnsOnlyActiveListings_FromApprovedBusinesses()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        var result = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Page = 1,
            PageSize = 50
        });

        Assert.NotEmpty(result.Items);

        // Every item displayed must be active and belonging to an approved business
        foreach (var item in result.Items)
        {
            Assert.True(item.IsActive, $"Item {item.Title} (ID: {item.Id}) must be Active.");
            Assert.Equal(EquipmentStatus.Active, item.Status);
            Assert.Equal(BusinessStatus.Approved, item.Business.Status);
        }

        // Must not contain draft items or unapproved business listings
        Assert.DoesNotContain(result.Items, e => e.Title.Contains("Draft", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(result.Items, e => e.Business.ShopName.Contains("Bay Tools Rental", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Catalog_SearchByKeyword_FiltersMatchesAccurately()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // 1. Search for "Sony"
        var sonyResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Search = "Sony"
        });

        Assert.NotEmpty(sonyResult.Items);
        Assert.All(sonyResult.Items, e =>
        {
            var match = e.Title.Contains("Sony", StringComparison.OrdinalIgnoreCase) ||
                        e.Description.Contains("Sony", StringComparison.OrdinalIgnoreCase);
            Assert.True(match, $"Expected item {e.Title} to match 'Sony'");
        });

        // 2. Search for "Generator"
        var generatorResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Search = "Generator"
        });

        Assert.NotEmpty(generatorResult.Items);
        Assert.Contains(generatorResult.Items, e => e.Title.Contains("Generator", StringComparison.OrdinalIgnoreCase));

        // 3. Search for non-existent keyword
        var emptyResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Search = "NonExistentKeywordXYZ12345"
        });

        Assert.Empty(emptyResult.Items);
        Assert.Equal(0, emptyResult.TotalCount);
    }

    [Fact]
    public async Task Catalog_FilterByCategorySlug_ReturnsOnlyMatchingCategoryListings()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        var result = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            CategorySlug = "cameras-optics"
        });

        Assert.NotEmpty(result.Items);
        Assert.All(result.Items, e =>
        {
            Assert.Equal("cameras-optics", e.Category.Slug);
        });
    }

    [Fact]
    public async Task Catalog_FilterByCityLocation_ReturnsStoredLocationMatches_WithoutGPS()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // Filter by San Francisco
        var sfResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            City = "San Francisco"
        });

        Assert.NotEmpty(sfResult.Items);
        Assert.All(sfResult.Items, e =>
        {
            Assert.Equal("San Francisco", e.Business.City, ignoreCase: true);
        });

        // Filter by Oakland
        var oaklandResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            City = "Oakland"
        });

        Assert.NotEmpty(oaklandResult.Items);
        Assert.All(oaklandResult.Items, e =>
        {
            Assert.Equal("Oakland", e.Business.City, ignoreCase: true);
        });
    }

    [Fact]
    public async Task Catalog_SafeSorting_ByPriceAscendingAndDescending()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // Price Ascending
        var ascResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            SortBy = "price_asc",
            PageSize = 20
        });

        Assert.True(ascResult.Items.Count >= 2);
        for (int i = 0; i < ascResult.Items.Count - 1; i++)
        {
            Assert.True(ascResult.Items[i].DailyRentalRate <= ascResult.Items[i + 1].DailyRentalRate,
                $"Expected {ascResult.Items[i].DailyRentalRate} <= {ascResult.Items[i + 1].DailyRentalRate}");
        }

        // Price Descending
        var descResult = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            SortBy = "price_desc",
            PageSize = 20
        });

        Assert.True(descResult.Items.Count >= 2);
        for (int i = 0; i < descResult.Items.Count - 1; i++)
        {
            Assert.True(descResult.Items[i].DailyRentalRate >= descResult.Items[i + 1].DailyRentalRate,
                $"Expected {descResult.Items[i].DailyRentalRate} >= {descResult.Items[i + 1].DailyRentalRate}");
        }
    }

    [Fact]
    public async Task Catalog_Pagination_CalculatesSafeBounds()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // Page 1 with PageSize = 2
        var page1 = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Page = 1,
            PageSize = 2
        });

        Assert.Equal(2, page1.Items.Count);
        Assert.True(page1.TotalCount > 2);
        Assert.Equal(1, page1.Page);

        // Page 2 with PageSize = 2
        var page2 = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Page = 2,
            PageSize = 2
        });

        Assert.Equal(2, page2.Items.Count);
        Assert.Equal(2, page2.Page);

        // Assert different items on page 1 vs page 2
        Assert.NotEqual(page1.Items[0].Id, page2.Items[0].Id);

        // Out of bounds page
        var outOfBounds = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria
        {
            Page = 999,
            PageSize = 10
        });

        Assert.Empty(outOfBounds.Items);
        Assert.Equal(999, outOfBounds.Page);
    }

    [Fact]
    public async Task CatalogDetails_ActiveApprovedListing_ReturnsCompleteStoreLocationAndHours()
    {
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // Get an active listing
        var catalog = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria { PageSize = 1 });
        var firstItem = catalog.Items.First();

        var details = await catalogService.GetEquipmentDetailsAsync(firstItem.Id, includeInactive: false);

        Assert.NotNull(details);
        Assert.Equal(firstItem.Id, details.Id);
        Assert.NotNull(details.Category);
        Assert.NotNull(details.Business);
        Assert.NotEmpty(details.Business.AddressLine);
        Assert.NotEmpty(details.Business.City);
        Assert.NotEmpty(details.Business.OperatingHours);
        Assert.True(details.DailyRentalRate > 0);
        Assert.True(details.SecurityDeposit >= 0);
        Assert.NotEmpty(details.Images);
    }

    [Fact]
    public async Task CatalogDetails_UnpublishedListing_HiddenFromPublicDiscovery_VisibleInAdminInspection()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        // Find the seeded draft listing
        var draftEquipment = await db.Equipments
            .Include(e => e.Business)
            .FirstOrDefaultAsync(e => !e.IsActive || e.Status == EquipmentStatus.Inactive);

        Assert.NotNull(draftEquipment);

        // Public retrieval (includeInactive: false) must return NULL
        var publicView = await catalogService.GetEquipmentDetailsAsync(draftEquipment.Id, includeInactive: false);
        Assert.Null(publicView);

        // Privileged inspection (includeInactive: true) must return the listing
        var privilegedView = await catalogService.GetEquipmentDetailsAsync(draftEquipment.Id, includeInactive: true);
        Assert.NotNull(privilegedView);
        Assert.Equal(draftEquipment.Id, privilegedView.Id);
    }

    [Fact]
    public async Task Storefront_ApprovedBusiness_ReturnsStoreInfoAndOnlyActiveListings()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        var approvedBusiness = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.Status == BusinessStatus.Approved && b.ShopName.Contains("Apex Machinery"));

        Assert.NotNull(approvedBusiness);

        var storefront = await catalogService.GetStorefrontAsync(approvedBusiness.Id);
        Assert.NotNull(storefront);
        Assert.Equal(approvedBusiness.ShopName, storefront.ShopName);
        Assert.Equal(BusinessStatus.Approved, storefront.Status);
        Assert.NotEmpty(storefront.AddressLine);
        Assert.NotEmpty(storefront.City);
        Assert.NotEmpty(storefront.OperatingHours);

        // All listings in storefront must be active
        Assert.NotEmpty(storefront.Equipments);
        Assert.All(storefront.Equipments, e =>
        {
            Assert.True(e.IsActive);
            Assert.Equal(EquipmentStatus.Active, e.Status);
        });
    }

    [Fact]
    public async Task Storefront_UnapprovedBusiness_ReturnsNullToPublic()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();

        var pendingBusiness = await db.BusinessProfiles
            .FirstOrDefaultAsync(b => b.Status == BusinessStatus.PendingApproval);

        Assert.NotNull(pendingBusiness);

        var storefront = await catalogService.GetStorefrontAsync(pendingBusiness.Id);
        Assert.Null(storefront);
    }

    [Fact]
    public async Task Wishlist_CustomerCanToggleAndRetrieve_SavedEquipment()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();
        var wishlistService = scope.ServiceProvider.GetRequiredService<IWishlistService>();

        // Create a unique test customer
        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var customer = new ApplicationUser
        {
            UserName = $"wish_{testSuffix}@test.rentkart.local",
            Email = $"wish_{testSuffix}@test.rentkart.local",
            FullName = $"Wishlist Tester {testSuffix}",
            UserType = UserRole.Customer,
            IsActive = true
        };
        var createResult = await userManager.CreateAsync(customer, "Password123!");
        Assert.True(createResult.Succeeded);
        await userManager.AddToRoleAsync(customer, IdentityDataSeeder.RoleCustomer);

        // Get an active equipment item
        var catalog = await catalogService.SearchCatalogAsync(new CatalogFilterCriteria { PageSize = 1 });
        var equipment = catalog.Items.First();

        // 1. Initial state: not in wishlist
        Assert.False(await wishlistService.IsInWishlistAsync(customer.Id, equipment.Id));

        // 2. Toggle on (Add)
        var added = await wishlistService.ToggleWishlistAsync(customer.Id, equipment.Id);
        Assert.True(added, "Toggle should return true when item is added.");
        Assert.True(await wishlistService.IsInWishlistAsync(customer.Id, equipment.Id));

        var userWishlist = await wishlistService.GetUserWishlistAsync(customer.Id);
        Assert.Single(userWishlist);
        Assert.Equal(equipment.Id, userWishlist[0].EquipmentId);
        Assert.Equal(equipment.Title, userWishlist[0].Equipment.Title);
        Assert.NotNull(userWishlist[0].Equipment.Business);

        // 3. Toggle off (Remove)
        var removed = await wishlistService.ToggleWishlistAsync(customer.Id, equipment.Id);
        Assert.False(removed, "Toggle should return false when item is removed.");
        Assert.False(await wishlistService.IsInWishlistAsync(customer.Id, equipment.Id));

        var emptyWishlist = await wishlistService.GetUserWishlistAsync(customer.Id);
        Assert.Empty(emptyWishlist);
    }

    [Fact]
    public async Task Wishlist_CannotAddDraftOrUnapprovedEquipment()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var wishlistService = scope.ServiceProvider.GetRequiredService<IWishlistService>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var customer = new ApplicationUser
        {
            UserName = $"wish_sec_{testSuffix}@test.rentkart.local",
            Email = $"wish_sec_{testSuffix}@test.rentkart.local",
            FullName = $"Security Tester {testSuffix}",
            UserType = UserRole.Customer,
            IsActive = true
        };
        await userManager.CreateAsync(customer, "Password123!");

        // Locate draft equipment
        var draft = await db.Equipments.FirstOrDefaultAsync(e => !e.IsActive || e.Status == EquipmentStatus.Inactive);
        Assert.NotNull(draft);

        // Attempting to add draft equipment to wishlist must be rejected
        var addDraftResult = await wishlistService.ToggleWishlistAsync(customer.Id, draft.Id);
        Assert.False(addDraftResult, "Must reject adding draft/inactive equipment to customer wishlist.");

        // Attempting to add non-existent equipment must be rejected
        var addNonExistentResult = await wishlistService.ToggleWishlistAsync(customer.Id, 999999);
        Assert.False(addNonExistentResult, "Must reject adding nonexistent equipment to wishlist.");
    }

    [Fact]
    public async Task WebRoutes_PublicDiscoveryPages_ReturnSuccess()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Homepage
        var homeResponse = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, homeResponse.StatusCode);

        // 2. Catalog Index
        var catalogResponse = await client.GetAsync("/Catalog");
        Assert.Equal(HttpStatusCode.OK, catalogResponse.StatusCode);

        // 3. Search query
        var searchResponse = await client.GetAsync("/Catalog?search=Sony&city=San+Francisco");
        Assert.Equal(HttpStatusCode.OK, searchResponse.StatusCode);

        // 4. Equipment route alias
        var equipmentResponse = await client.GetAsync("/Equipment");
        Assert.Equal(HttpStatusCode.OK, equipmentResponse.StatusCode);

        // 5. Category redirect/filter route
        var categoryResponse = await client.GetAsync("/Catalog/Category/cameras-optics");
        Assert.Equal(HttpStatusCode.Redirect, categoryResponse.StatusCode);
        Assert.Equal("/Catalog?categorySlug=cameras-optics", categoryResponse.Headers.Location?.OriginalString);

        // 6. Active listing details page renders OK with HTML
        using var scope = _factory.Services.CreateScope();
        var catalogService = scope.ServiceProvider.GetRequiredService<IEquipmentCatalogService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var firstActive = (await catalogService.SearchCatalogAsync(new CatalogFilterCriteria { PageSize = 1 })).Items.First();
        var detailsResponse = await client.GetAsync($"/Catalog/Details/{firstActive.Id}");
        Assert.Equal(HttpStatusCode.OK, detailsResponse.StatusCode);
        var detailsHtml = await detailsResponse.Content.ReadAsStringAsync();
        Assert.Contains(firstActive.Title, detailsHtml);

        // 7. Approved business storefront renders OK with HTML
        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var storeResponse = await client.GetAsync($"/Store/{approvedBiz.Id}");
        Assert.Equal(HttpStatusCode.OK, storeResponse.StatusCode);
        var storeHtml = await storeResponse.Content.ReadAsStringAsync();
        Assert.Contains(WebUtility.HtmlEncode(approvedBiz.ShopName), storeHtml);

        // 8. Security: Inactive/Draft listing returns 404 to anonymous web user
        var draftEquipment = await db.Equipments.FirstAsync(e => !e.IsActive || e.Status == EquipmentStatus.Inactive);
        var draftResponse = await client.GetAsync($"/Catalog/Details/{draftEquipment.Id}");
        Assert.Equal(HttpStatusCode.NotFound, draftResponse.StatusCode);

        // 9. Security: Unapproved business storefront returns 404 to anonymous web user
        var pendingBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.PendingApproval);
        var pendingStoreResponse = await client.GetAsync($"/Store/{pendingBiz.Id}");
        Assert.Equal(HttpStatusCode.NotFound, pendingStoreResponse.StatusCode);

        // 10. Anonymous Wishlist access requires authentication
        var wishlistResponse = await client.GetAsync("/Wishlist");
        Assert.Equal(HttpStatusCode.Redirect, wishlistResponse.StatusCode);
        Assert.Contains("/Account/Login", wishlistResponse.Headers.Location?.OriginalString);
    }
}
