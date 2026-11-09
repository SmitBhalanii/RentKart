using System.Net;
using Microsoft.AspNetCore.Http;
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

public class Phase11EndToEndAcceptanceTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public Phase11EndToEndAcceptanceTests(WebApplicationFactory<Program> factory)
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
    public async Task CompleteRentKartMvp_EndToEndAcceptanceScenario_ExecutesSuccessfully()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var cartService = scope.ServiceProvider.GetRequiredService<ICartService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var documentService = scope.ServiceProvider.GetRequiredService<IRentalDocumentService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = new TestSession();
        httpContextAccessor.HttpContext = httpContext;

        // =========================================================================
        // SCENARIO STEP 1: Customer registers and authenticates
        // =========================================================================
        var customerEmail = $"mvp_customer_{Guid.NewGuid():N}@rentkart.local";
        var customer = new ApplicationUser
        {
            UserName = customerEmail,
            Email = customerEmail,
            FullName = "Jane MVP Customer",
            UserType = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        var createCustResult = await userManager.CreateAsync(customer, "SecurePassword123!");
        Assert.True(createCustResult.Succeeded, string.Join(", ", createCustResult.Errors.Select(e => e.Description)));
        await userManager.AddToRoleAsync(customer, IdentityDataSeeder.RoleCustomer);

        var loadedCustomer = await userManager.FindByEmailAsync(customerEmail);
        Assert.NotNull(loadedCustomer);
        Assert.True(await userManager.IsInRoleAsync(loadedCustomer, IdentityDataSeeder.RoleCustomer));
        Assert.False(await userManager.IsInRoleAsync(loadedCustomer, IdentityDataSeeder.RoleAdmin));

        // =========================================================================
        // SCENARIO STEP 2: Two approved businesses publish listings with multiple inventory units
        // =========================================================================
        var (storeA, storeB) = await GetOrCreateTwoApprovedStoresAsync(db);
        var category = await db.Categories.FirstAsync();

        // Listing A in Store A with 5 units
        var itemA = new Equipment
        {
            BusinessId = storeA.Id,
            CategoryId = category.Id,
            Title = $"Heavy Duty Tile Saw {Guid.NewGuid():N}",
            Description = "Professional grade wet tile saw with stand",
            DailyRentalRate = 45.00m,
            SecurityDeposit = 150.00m,
            TotalQuantity = 5,
            Condition = EquipmentCondition.Excellent,
            Status = EquipmentStatus.Active,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(itemA);

        // Listing B in Store B with 3 units
        var itemB = new Equipment
        {
            BusinessId = storeB.Id,
            CategoryId = category.Id,
            Title = $"Commercial Dehumidifier {Guid.NewGuid():N}",
            Description = "High capacity flood and moisture restoration dehumidifier",
            DailyRentalRate = 70.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 3,
            Condition = EquipmentCondition.LikeNew,
            Status = EquipmentStatus.Active,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(itemB);
        await db.SaveChangesAsync();

        Assert.True(itemA.Id > 0);
        Assert.True(itemB.Id > 0);

        // =========================================================================
        // SCENARIO STEP 3: Customer selects multiple listings belonging to both businesses into cart
        // =========================================================================
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(500));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(502)); // 3 rental days

        var addCartA = await cartService.AddToCartAsync(itemA.Id, 2, startDate, endDate);
        Assert.True(addCartA.Success, addCartA.Message);

        var addCartB = await cartService.AddToCartAsync(itemB.Id, 1, startDate, endDate);
        Assert.True(addCartB.Success, addCartB.Message);

        var cart = await cartService.GetCartAsync();
        Assert.Equal(2, cart.Items.Count);
        Assert.Equal(3, cart.TotalItemsCount);
        Assert.Equal(2, cart.DistinctStoresCount);

        // =========================================================================
        // SCENARIO STEP 4: Customer checks out creating parent booking with separate business subgroups
        // =========================================================================
        var bookingRequest = new CreateBookingRequestDto
        {
            CustomerNotes = "Contractor project on soundstage B",
            Items = cart.Items.Select(ci => new CreateBookingItemRequestDto
            {
                EquipmentId = ci.EquipmentId,
                Quantity = ci.Quantity,
                StartDate = ci.StartDate,
                EndDate = ci.EndDate
            }).ToList()
        };

        var bookingResult = await bookingService.CreateBookingAsync(customer.Id, bookingRequest);
        Assert.True(bookingResult.Success, bookingResult.ErrorMessage);
        int parentBookingId = bookingResult.BookingId!.Value;

        var parentBooking = await db.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .FirstAsync(b => b.Id == parentBookingId);

        Assert.Equal(BookingStatus.Pending, parentBooking.Status);
        Assert.Equal(2, parentBooking.BookingGroups.Count);

        var groupA = parentBooking.BookingGroups.First(g => g.BusinessId == storeA.Id);
        var groupB = parentBooking.BookingGroups.First(g => g.BusinessId == storeB.Id);

        Assert.Equal(BookingGroupStatus.PendingApproval, groupA.Status);
        Assert.Equal(BookingGroupStatus.PendingApproval, groupB.Status);
        Assert.False(string.IsNullOrWhiteSpace(groupA.PickupVerificationCode));
        Assert.False(string.IsNullOrWhiteSpace(groupB.PickupVerificationCode));
        Assert.NotEqual(groupA.PickupVerificationCode, groupB.PickupVerificationCode);

        // =========================================================================
        // SCENARIO STEP 5: Businesses make independent approval decisions
        // =========================================================================
        // Store A confirms Group A
        var acceptA = await bookingService.AcceptBookingGroupAsync(groupA.Id, storeA.Id);
        Assert.True(acceptA.Success, acceptA.ErrorMessage);

        var reloadedGroupA = await db.BookingGroups.FindAsync(groupA.Id);
        Assert.Equal(BookingGroupStatus.Confirmed, reloadedGroupA!.Status);

        // Store B remains pending until its own merchant accepts
        var reloadedGroupB = await db.BookingGroups.FindAsync(groupB.Id);
        Assert.Equal(BookingGroupStatus.PendingApproval, reloadedGroupB!.Status);

        // Now Store B confirms Group B
        var acceptB = await bookingService.AcceptBookingGroupAsync(groupB.Id, storeB.Id);
        Assert.True(acceptB.Success, acceptB.ErrorMessage);

        var reloadedParent = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Confirmed, reloadedParent!.Status);

        // =========================================================================
        // SCENARIO STEP 6: The system prevents inventory overbooking and handles conflicting requests
        // =========================================================================
        // Item A total quantity = 5. Customer 1 has reserved 2 units.
        // Another customer tries to book 4 units of Item A for the overlapping period (2 + 4 = 6 > 5)
        var conflictingCustomer = await CreateDistinctCustomerAsync(db);
        var overbookingResult = await bookingService.CreateBookingAsync(conflictingCustomer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = itemA.Id, Quantity = 4, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.False(overbookingResult.Success);
        Assert.Contains("Insufficient units available", overbookingResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Booking 2 units (2 + 2 = 4 <= 5) succeeds
        var validBookingResult = await bookingService.CreateBookingAsync(conflictingCustomer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = itemA.Id, Quantity = 2, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(validBookingResult.Success, validBookingResult.ErrorMessage);

        // =========================================================================
        // SCENARIO STEP 7: Customer collects equipment at each applicable business location
        // =========================================================================
        // Store A verifies pickup code and records handover
        var invalidCodeHandover = await bookingService.HandoverBookingGroupAsync(groupA.Id, storeA.Id, "WRONG-CODE", "Notes");
        Assert.False(invalidCodeHandover.Success);

        var handoverA = await bookingService.HandoverBookingGroupAsync(groupA.Id, storeA.Id, groupA.PickupVerificationCode, "Equipment checked, blades sharp and tested");
        Assert.True(handoverA.Success, handoverA.ErrorMessage);
        Assert.Equal(BookingGroupStatus.HandedOver, handoverA.NewStatus);

        // Store B has NOT handed over yet; Store B subgroup remains Confirmed
        var checkGroupB = await db.BookingGroups.FindAsync(groupB.Id);
        Assert.Equal(BookingGroupStatus.Confirmed, checkGroupB!.Status);

        // Store B now hands over
        var handoverB = await bookingService.HandoverBookingGroupAsync(groupB.Id, storeB.Id, groupB.PickupVerificationCode, "Handed over with filter clean");
        Assert.True(handoverB.Success, handoverB.ErrorMessage);
        Assert.Equal(BookingGroupStatus.HandedOver, handoverB.NewStatus);

        var activeParent = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Active, activeParent!.Status);

        // =========================================================================
        // SCENARIO STEP 8: Each business records its own return and inspection
        // =========================================================================
        var bookingItemA = await db.BookingItems.FirstAsync(bi => bi.BookingGroupId == groupA.Id);
        var bookingItemB = await db.BookingItems.FirstAsync(bi => bi.BookingGroupId == groupB.Id);

        // Store A inspects clean return: no damage
        var returnReqA = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Tile saw returned clean, stand intact, blade in good condition",
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = bookingItemA.Id,
                    ReturnedQuantity = bookingItemA.Quantity,
                    MissingQuantity = 0,
                    Condition = "Good",
                    HasDamage = false
                }
            }
        };
        var returnResA = await bookingService.CompleteReturnWithInspectionAsync(groupA.Id, storeA.Id, returnReqA);
        Assert.True(returnResA.Success, returnResA.ErrorMessage);
        Assert.Equal(BookingGroupStatus.Completed, returnResA.NewStatus);

        // Store B is still in HandedOver! Store A's return did NOT complete Store B's subgroup
        var verifyGroupBBeforeReturn = await db.BookingGroups.FindAsync(groupB.Id);
        Assert.Equal(BookingGroupStatus.HandedOver, verifyGroupBBeforeReturn!.Status);

        // =========================================================================
        // SCENARIO STEP 9: Damage and maintenance rules update availability correctly
        // =========================================================================
        // Store B records return with damage on Item B
        var returnReqB = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Return inspection revealed damaged casing and cracked condensation hose",
            AssessedDamageFee = 80.00m,
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = bookingItemB.Id,
                    ReturnedQuantity = bookingItemB.Quantity,
                    MissingQuantity = 0,
                    Condition = "Fair",
                    HasDamage = true,
                    DamageDescription = "Cracked condensation hose requiring maintenance downtime",
                    EstimatedRepairCost = 80.00m,
                    CreateMaintenanceBlock = true,
                    EstimatedMaintenanceDays = 7
                }
            }
        };
        var returnResB = await bookingService.CompleteReturnWithInspectionAsync(groupB.Id, storeB.Id, returnReqB);
        Assert.True(returnResB.Success, returnResB.ErrorMessage);
        Assert.Equal(BookingGroupStatus.Completed, returnResB.NewStatus);

        // Verify maintenance downtime record exists for item B
        var maintenanceRecord = await db.MaintenanceRecords
            .FirstOrDefaultAsync(m => m.EquipmentId == itemB.Id && !m.IsResolved);
        Assert.NotNull(maintenanceRecord);
        Assert.True(maintenanceRecord.BlockedQuantity > 0);

        // =========================================================================
        // SCENARIO STEP 10: The parent booking reflects subgroup completion accurately
        // =========================================================================
        var completedParent = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Completed, completedParent!.Status);

        // =========================================================================
        // SCENARIO STEP 11: Eligible reviews, notifications, and rental documents work
        // =========================================================================
        // Customer reviews Item A from the completed subgroup
        var eligibilityA = await reviewService.CheckEligibilityAsync(bookingItemA.Id, customer.Id);
        Assert.True(eligibilityA.CanReview, eligibilityA.Reason);

        var submitReviewResult = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = bookingItemA.Id,
            Rating = 5,
            Comment = "Phenomenal wet saw cut through granite smoothly. Quick in-store return inspection."
        }, customer.Id);
        Assert.True(submitReviewResult.Success, submitReviewResult.ErrorMessage);

        // Duplicate review blocked
        var duplicateReview = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = bookingItemA.Id,
            Rating = 4,
            Comment = "Duplicate review attempt."
        }, customer.Id);
        Assert.False(duplicateReview.Success);

        // Reviews appear in public equipment summary
        var reviewSummary = await reviewService.GetEquipmentReviewsSummaryAsync(itemA.Id);
        Assert.Equal(1, reviewSummary.TotalReviews);
        Assert.Equal(5.0, reviewSummary.AverageRating);

        // Notifications exist for customer
        var customerNotifs = await notificationService.GetUserNotificationsAsync(customer.Id);
        Assert.NotEmpty(customerNotifs.Items);

        // Documents: Agreement and invoice exist and are downloadable
        var customerDocs = await documentService.GetCustomerDocumentsAsync(customer.Id);
        Assert.NotEmpty(customerDocs.Items);

        var firstDoc = customerDocs.Items.First();
        var downloadResult = await documentService.GetDocumentForDownloadAsync(firstDoc.Id, customer.Id);
        Assert.True(downloadResult.Success, downloadResult.ErrorMessage);
        Assert.NotNull(downloadResult.FileBytes);
        Assert.True(downloadResult.FileBytes.Length > 0);

        // =========================================================================
        // SCENARIO STEP 12: Admin can manage the platform
        // =========================================================================
        var admin = await GetOrCreateAdminUserAsync(db);

        // Dashboard metrics
        var metrics = await adminService.GetDashboardMetricsAsync();
        Assert.True(metrics.TotalBookingsCount > 0);
        Assert.True(metrics.CompletedBookingsCount > 0);
        Assert.True(metrics.TotalPlatformTurnover > 0);
        Assert.True(metrics.ActiveDamageReportsCount > 0);

        // Category protection: Deleting category with listings fails
        var (delCatSuccess, delCatError) = await adminService.DeleteCategoryAsync(category.Id, admin.Id);
        Assert.False(delCatSuccess);
        Assert.Contains("equipment listing(s) are assigned", delCatError, StringComparison.OrdinalIgnoreCase);

        // Review Moderation: Hide review
        var modReviewSuccess = await reviewService.ModerateReviewAsync(submitReviewResult.ReviewId!.Value, isHidden: true, "Routine compliance audit", admin.Id);
        Assert.True(modReviewSuccess);
        var reviewSummaryAfterHide = await reviewService.GetEquipmentReviewsSummaryAsync(itemA.Id);
        Assert.Equal(0, reviewSummaryAfterHide.TotalReviews); // Hidden reviews do not skew public rating

        // Damage incident resolution
        var resolveDamageSuccess = await adminService.ResolveDamageReportAsync(maintenanceRecord.Id, admin.Id, "Replaced hose and tested pressure");
        Assert.True(resolveDamageSuccess);
        var updatedMaintenance = await db.MaintenanceRecords.FindAsync(maintenanceRecord.Id);
        Assert.True(updatedMaintenance!.IsResolved);

        // =========================================================================
        // SCENARIO STEP 13: Unauthorized users cannot access restricted data
        // =========================================================================
        // Customer A cannot access Customer B's documents
        var unauthorizedDocDownload = await documentService.GetDocumentForDownloadAsync(firstDoc.Id, conflictingCustomer.Id);
        Assert.False(unauthorizedDocDownload.Success);
        Assert.Contains("access denied", unauthorizedDocDownload.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Store A cannot download Store B's documents
        var storeBDoc = await db.RentalDocuments.FirstOrDefaultAsync(d => d.BookingGroupId == groupB.Id);
        if (storeBDoc != null)
        {
            var storeUnauthorizedDoc = await documentService.GetDocumentForDownloadAsync(storeBDoc.Id, storeA.UserId);
            Assert.False(storeUnauthorizedDoc.Success);
        }

        // Web Client role-based route guard: Anonymous client blocked from /Admin
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var adminEndpointResponse = await client.GetAsync("/Admin/Dashboard");
        Assert.True(adminEndpointResponse.StatusCode == HttpStatusCode.Redirect || adminEndpointResponse.StatusCode == HttpStatusCode.Unauthorized);

        // =========================================================================
        // SCENARIO STEP 14: The application continues to work after restart
        // =========================================================================
        // Recreate a brand new DI scope simulating full application restart
        using var newScope = _factory.Services.CreateScope();
        var restartedDb = newScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var restartedAdminService = newScope.ServiceProvider.GetRequiredService<IAdminPlatformService>();

        var persistedBooking = await restartedDb.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .FirstOrDefaultAsync(b => b.Id == parentBookingId);

        Assert.NotNull(persistedBooking);
        Assert.Equal(BookingStatus.Completed, persistedBooking.Status);
        Assert.Equal(2, persistedBooking.BookingGroups.Count);
        Assert.All(persistedBooking.BookingGroups, bg => Assert.Equal(BookingGroupStatus.Completed, bg.Status));

        var restartedMetrics = await restartedAdminService.GetDashboardMetricsAsync();
        Assert.NotNull(restartedMetrics);
        Assert.True(restartedMetrics.TotalBookingsCount >= 1);
        Assert.True(restartedMetrics.CompletedBookingsCount >= 1);
    }

    private static async Task<(BusinessProfile StoreA, BusinessProfile StoreB)> GetOrCreateTwoApprovedStoresAsync(ApplicationDbContext db)
    {
        var approvedStores = await db.BusinessProfiles
            .Include(b => b.User)
            .Where(b => b.Status == BusinessStatus.Approved)
            .Take(2)
            .ToListAsync();

        if (approvedStores.Count >= 2)
        {
            return (approvedStores[0], approvedStores[1]);
        }

        while (approvedStores.Count < 2)
        {
            var user = new ApplicationUser
            {
                UserName = $"merchant_{Guid.NewGuid():N}@rentkart.local",
                Email = $"merchant_{Guid.NewGuid():N}@rentkart.local",
                FullName = $"Merchant Store Owner {Guid.NewGuid():N}",
                UserType = UserRole.Business,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();

            var biz = new BusinessProfile
            {
                UserId = user.Id,
                ShopName = $"Pro Tool Equipment Co #{Guid.NewGuid().ToString("N")[..6]}",
                AddressLine = "100 Industrial Parkway",
                City = "Seattle",
                State = "WA",
                PostalCode = "98101",
                ContactPhone = "206-555-0199",
                OperatingHours = "Mon-Sat: 07:00 AM - 06:00 PM",
                Status = BusinessStatus.Approved,
                ApprovedAtUtc = DateTime.UtcNow,
                CreatedAtUtc = DateTime.UtcNow
            };
            db.BusinessProfiles.Add(biz);
            await db.SaveChangesAsync();
            approvedStores.Add(biz);
        }

        return (approvedStores[0], approvedStores[1]);
    }

    private static async Task<ApplicationUser> CreateDistinctCustomerAsync(ApplicationDbContext db)
    {
        var user = new ApplicationUser
        {
            UserName = $"distinct_cust_{Guid.NewGuid():N}@rentkart.local",
            Email = $"distinct_cust_{Guid.NewGuid():N}@rentkart.local",
            FullName = "Distinct Customer",
            UserType = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<ApplicationUser> GetOrCreateAdminUserAsync(ApplicationDbContext db)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserType == UserRole.Admin);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            UserName = $"e2e_admin_{Guid.NewGuid():N}@rentkart.local",
            Email = $"e2e_admin_{Guid.NewGuid():N}@rentkart.local",
            FullName = "Platform Administrator",
            UserType = UserRole.Admin,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }
}

internal class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _storage = new();
    public bool IsAvailable => true;
    public string Id => Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _storage.Keys;

    public void Clear() => _storage.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _storage.Remove(key);
    public void Set(string key, byte[] value) => _storage[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _storage.TryGetValue(key, out value!);
}
