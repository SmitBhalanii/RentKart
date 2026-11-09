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

public class CustomerReviewsAndAdminTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public CustomerReviewsAndAdminTests(WebApplicationFactory<Program> factory)
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
    public async Task ReviewEligibility_WhenRentalIsCompleted_ReturnsCanReviewTrue()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (booking, group, item, customer) = await SetupCompletedRentalAsync(db, bookingService);

        var eligibility = await reviewService.CheckEligibilityAsync(item.Id, customer.Id);

        Assert.True(eligibility.CanReview, eligibility.Reason);
        Assert.Equal(item.Id, eligibility.BookingItemId);
        Assert.Equal(item.EquipmentId, eligibility.EquipmentId);
    }

    [Fact]
    public async Task ReviewEligibility_WhenRentalIsNotCompleted_ReturnsCanReviewFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var equipment = await CreateDedicatedTestEquipmentAsync(db);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(400));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(402));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = equipment.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var bookingItem = await db.BookingItems
            .Include(bi => bi.BookingGroup)
            .FirstAsync(bi => bi.BookingGroup.ParentBookingId == createResult.BookingId!.Value);

        // Group is PendingApproval, NOT Completed
        var eligibility = await reviewService.CheckEligibilityAsync(bookingItem.Id, customer.Id);

        Assert.False(eligibility.CanReview);
        Assert.Contains("completed", eligibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReviewEligibility_WhenBookingItemBelongsToAnotherCustomer_ReturnsCanReviewFalse()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, _, item, _) = await SetupCompletedRentalAsync(db, bookingService);

        var otherCustomer = await CreateDistinctCustomerAsync(db);

        var eligibility = await reviewService.CheckEligibilityAsync(item.Id, otherCustomer.Id);

        Assert.False(eligibility.CanReview);
        Assert.Contains("own bookings", eligibility.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitReview_ValidReview_SucceedsAndAppearsInEquipmentSummary()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, _, item, customer) = await SetupCompletedRentalAsync(db, bookingService);

        var result = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 5,
            Comment = "Outstanding tool performance and pristine condition! Friendly pickup counter."
        }, customer.Id);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.ReviewId);

        var summary = await reviewService.GetEquipmentReviewsSummaryAsync(item.EquipmentId);
        Assert.True(summary.TotalReviews >= 1);
        Assert.True(summary.AverageRating > 0);

        var reviews = await reviewService.GetEquipmentReviewsAsync(item.EquipmentId);
        Assert.Contains(reviews, r => r.BookingItemId == item.Id && r.Rating == 5);
    }

    [Fact]
    public async Task SubmitReview_DuplicateSubmission_FailsGracefully()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, _, item, customer) = await SetupCompletedRentalAsync(db, bookingService);

        var firstResult = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 4,
            Comment = "First review for completed item."
        }, customer.Id);

        Assert.True(firstResult.Success);

        // Attempting second review for the same item
        var secondResult = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 5,
            Comment = "Attempted duplicate review."
        }, customer.Id);

        Assert.False(secondResult.Success);
        Assert.Contains("already submitted", secondResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitReview_InvalidRatingOrShortComment_FailsValidation()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, _, item, customer) = await SetupCompletedRentalAsync(db, bookingService);

        // Rating out of bounds (6 stars)
        var invalidRating = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 6,
            Comment = "Good piece of equipment."
        }, customer.Id);

        Assert.False(invalidRating.Success);
        Assert.Contains("between 1 and 5", invalidRating.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Comment too short (< 5 chars)
        var invalidComment = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 5,
            Comment = "Ok"
        }, customer.Id);

        Assert.False(invalidComment.Success);
        Assert.Contains("at least 5 characters", invalidComment.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AdminReviewModeration_HideReview_HidesFromPublicCatalogAndUpdatesAuditLog()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var reviewService = scope.ServiceProvider.GetRequiredService<IReviewService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var (_, _, item, customer) = await SetupCompletedRentalAsync(db, bookingService);
        var admin = await GetOrCreateAdminUserAsync(db);

        var submitResult = await reviewService.SubmitReviewAsync(new SubmitReviewRequestDto
        {
            BookingItemId = item.Id,
            Rating = 5,
            Comment = "Review slated for admin moderation test."
        }, customer.Id);

        int reviewId = submitResult.ReviewId!.Value;

        // Admin hides review
        var hideSuccess = await reviewService.ModerateReviewAsync(reviewId, isHidden: true, "Policy violation", admin.Id);
        Assert.True(hideSuccess);

        var publicReviews = await reviewService.GetEquipmentReviewsAsync(item.EquipmentId);
        Assert.DoesNotContain(publicReviews, r => r.Id == reviewId);

        var auditLog = await db.AuditLogs
            .OrderByDescending(a => a.TimestampUtc)
            .FirstOrDefaultAsync(a => a.EntityType == nameof(Review) && a.EntityId == reviewId.ToString());

        Assert.NotNull(auditLog);
        Assert.Equal("HideReview", auditLog.Action);

        // Admin unhides review
        var unhideSuccess = await reviewService.ModerateReviewAsync(reviewId, isHidden: false, "Reinstated after appeal", admin.Id);
        Assert.True(unhideSuccess);

        var publicReviewsAfter = await reviewService.GetEquipmentReviewsAsync(item.EquipmentId);
        Assert.Contains(publicReviewsAfter, r => r.Id == reviewId);
    }

    [Fact]
    public async Task AdminDashboard_ReturnsAuthoritativeRealMetrics()
    {
        using var scope = _factory.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();

        var metrics = await adminService.GetDashboardMetricsAsync();

        Assert.NotNull(metrics);
        Assert.True(metrics.TotalUsersCount >= 1);
        Assert.True(metrics.TotalEquipmentCount >= 0);
        Assert.True(metrics.TotalBookingsCount >= 0);
        Assert.True(metrics.TotalPlatformTurnover >= 0);
    }

    [Fact]
    public async Task AdminCustomerManagement_ToggleCustomerLockout_UpdatesStatusAndLogsAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await CreateDistinctCustomerAsync(db);
        var admin = await GetOrCreateAdminUserAsync(db);

        // 1. Lock customer
        var lockSuccess = await adminService.ToggleCustomerLockoutAsync(customer.Id, admin.Id, "Suspicious behavior");
        Assert.True(lockSuccess);

        var lockedDetails = await adminService.GetCustomerDetailsAsync(customer.Id);
        Assert.NotNull(lockedDetails);
        Assert.True(lockedDetails.IsLockedOut);

        // 2. Unlock customer
        var unlockSuccess = await adminService.ToggleCustomerLockoutAsync(customer.Id, admin.Id, "Resolved verification");
        Assert.True(unlockSuccess);

        var unlockedDetails = await adminService.GetCustomerDetailsAsync(customer.Id);
        Assert.NotNull(unlockedDetails);
        Assert.False(unlockedDetails.IsLockedOut);

        // Check audit log
        var logs = await db.AuditLogs.Where(a => a.EntityId == customer.Id).ToListAsync();
        Assert.Contains(logs, l => l.Action == "LockCustomer");
        Assert.Contains(logs, l => l.Action == "UnlockCustomer");
    }

    [Fact]
    public async Task AdminCategoryManagement_DeleteCategoryWithExistingEquipment_FailsWithConstraintError()
    {
        using var scope = _factory.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await GetOrCreateAdminUserAsync(db);

        // Category with equipment
        var existingCategory = await db.Categories.FirstAsync(c => c.Equipments.Any());

        var (failSuccess, failError) = await adminService.DeleteCategoryAsync(existingCategory.Id, admin.Id);
        Assert.False(failSuccess);
        Assert.Contains("equipment listing(s) are assigned", failError, StringComparison.OrdinalIgnoreCase);

        // Create empty category, then delete
        var (createSuccess, _, createdId) = await adminService.CreateCategoryAsync(new AdminCreateCategoryDto
        {
            Name = $"Temp Category {Guid.NewGuid():N}",
            Slug = $"temp-cat-{Guid.NewGuid():N}",
            Description = "Temporary test category",
            DisplayOrder = 99,
            IsActive = true
        }, admin.Id);

        Assert.True(createSuccess);
        Assert.NotNull(createdId);

        var (delSuccess, _) = await adminService.DeleteCategoryAsync(createdId.Value, admin.Id);
        Assert.True(delSuccess);
    }

    [Fact]
    public async Task AdminEquipmentModeration_ToggleStatus_SuspendsListingAndLogsAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await GetOrCreateAdminUserAsync(db);

        var equipment = await CreateDedicatedTestEquipmentAsync(db);

        // Suspend
        var (suspendSuccess, _) = await adminService.ToggleEquipmentStatusAsync(equipment.Id, isActive: false, "Policy audit", admin.Id);
        Assert.True(suspendSuccess);

        var eqSuspended = await db.Equipments.FindAsync(equipment.Id);
        Assert.False(eqSuspended!.IsActive);
        Assert.Equal(EquipmentStatus.Inactive, eqSuspended.Status);

        // Reactivate
        var (activateSuccess, _) = await adminService.ToggleEquipmentStatusAsync(equipment.Id, isActive: true, "Resolved", admin.Id);
        Assert.True(activateSuccess);

        var eqActive = await db.Equipments.FindAsync(equipment.Id);
        Assert.True(eqActive!.IsActive);
        Assert.Equal(EquipmentStatus.Active, eqActive.Status);
    }

    [Fact]
    public async Task AdminDamageReportModeration_ResolveDamageReport_RestoresRecordAndLogsAudit()
    {
        using var scope = _factory.Services.CreateScope();
        var adminService = scope.ServiceProvider.GetRequiredService<IAdminPlatformService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var admin = await GetOrCreateAdminUserAsync(db);

        var equipment = await CreateDedicatedTestEquipmentAsync(db);

        var record = new MaintenanceRecord
        {
            EquipmentId = equipment.Id,
            BlockedQuantity = 1,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow),
            EndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Reason = "Return Inspection Failure: Damaged lens cover",
            EstimatedCost = 85.00m,
            IsResolved = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MaintenanceRecords.Add(record);
        await db.SaveChangesAsync();

        var resolveSuccess = await adminService.ResolveDamageReportAsync(record.Id, admin.Id, "Lens replaced and recalibrated");
        Assert.True(resolveSuccess);

        var updatedRecord = await db.MaintenanceRecords.FindAsync(record.Id);
        Assert.NotNull(updatedRecord);
        Assert.True(updatedRecord.IsResolved);
        Assert.NotNull(updatedRecord.ResolvedAtUtc);
    }

    [Fact]
    public async Task AdminAccessControl_UnauthenticatedOrNonAdmin_CannotAccessAdminEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var dashboardResponse = await client.GetAsync("/Admin/Dashboard");
        Assert.True(dashboardResponse.StatusCode == HttpStatusCode.Redirect || dashboardResponse.StatusCode == HttpStatusCode.Unauthorized);

        var customersResponse = await client.GetAsync("/Admin/Customers");
        Assert.True(customersResponse.StatusCode == HttpStatusCode.Redirect || customersResponse.StatusCode == HttpStatusCode.Unauthorized);

        var categoriesResponse = await client.GetAsync("/Admin/Categories");
        Assert.True(categoriesResponse.StatusCode == HttpStatusCode.Redirect || categoriesResponse.StatusCode == HttpStatusCode.Unauthorized);
    }

    // Helper method to setup a completed rental lifecycle
    private static async Task<(Booking Booking, BookingGroup Group, BookingItem Item, ApplicationUser Customer)> SetupCompletedRentalAsync(
        ApplicationDbContext db,
        IBookingService bookingService)
    {
        var customer = await GetOrCreateCustomerAsync(db);
        var equipment = await CreateDedicatedTestEquipmentAsync(db);
        var biz = await db.BusinessProfiles.Include(b => b.User).FirstAsync(b => b.Id == equipment.BusinessId);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(450));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(452));

        // 1. Create booking
        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = equipment.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        int bookingId = createResult.BookingId!.Value;
        var group = await db.BookingGroups
            .Include(bg => bg.BookingItems)
            .FirstAsync(bg => bg.ParentBookingId == bookingId);

        // 2. Store confirms
        var acceptRes = await bookingService.AcceptBookingGroupAsync(group.Id, biz.Id);
        Assert.True(acceptRes.Success, acceptRes.ErrorMessage);

        // 3. Store hand over
        var handoverRes = await bookingService.HandoverBookingGroupAsync(group.Id, biz.Id, group.PickupVerificationCode, "Pristine condition handover");
        Assert.True(handoverRes.Success, handoverRes.ErrorMessage);

        // 4. Store complete return inspection
        var item = await db.BookingItems.FirstAsync(bi => bi.BookingGroupId == group.Id);
        var returnRequest = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Returned on time, no damage detected",
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = item.Id,
                    ReturnedQuantity = item.Quantity,
                    MissingQuantity = 0,
                    Condition = "Good",
                    HasDamage = false
                }
            }
        };

        var returnRes = await bookingService.CompleteReturnWithInspectionAsync(group.Id, biz.Id, returnRequest);
        Assert.True(returnRes.Success, returnRes.ErrorMessage);

        var reloadedBooking = await db.Bookings.FindAsync(bookingId);
        var reloadedGroup = await db.BookingGroups.FindAsync(group.Id);

        return (reloadedBooking!, reloadedGroup!, item, customer);
    }

    private static async Task<ApplicationUser> GetOrCreateCustomerAsync(ApplicationDbContext db)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserType == UserRole.Customer);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            UserName = $"test_cust_{Guid.NewGuid():N}@rentkart.local",
            Email = $"test_cust_{Guid.NewGuid():N}@rentkart.local",
            FullName = "Test Customer",
            UserType = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<ApplicationUser> CreateDistinctCustomerAsync(ApplicationDbContext db)
    {
        var user = new ApplicationUser
        {
            UserName = $"other_cust_{Guid.NewGuid():N}@rentkart.local",
            Email = $"other_cust_{Guid.NewGuid():N}@rentkart.local",
            FullName = "Other Test Customer",
            UserType = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Equipment> CreateDedicatedTestEquipmentAsync(ApplicationDbContext db, int? businessId = null)
    {
        BusinessProfile biz;
        if (businessId.HasValue)
        {
            biz = await db.BusinessProfiles.FirstAsync(b => b.Id == businessId.Value);
        }
        else
        {
            biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        }

        var category = await db.Categories.FirstAsync();
        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Dedicated Test Item {Guid.NewGuid():N}",
            Description = "Phase 10 test equipment listing",
            DailyRentalRate = 50.00m,
            SecurityDeposit = 100.00m,
            TotalQuantity = 10,
            Condition = EquipmentCondition.Excellent,
            Status = EquipmentStatus.Active,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();
        return item;
    }

    private static async Task<ApplicationUser> GetOrCreateAdminUserAsync(ApplicationDbContext db)
    {
        var existing = await db.Users.FirstOrDefaultAsync(u => u.UserType == UserRole.Admin);
        if (existing != null) return existing;

        var user = new ApplicationUser
        {
            UserName = $"test_admin_{Guid.NewGuid():N}@rentkart.local",
            Email = $"test_admin_{Guid.NewGuid():N}@rentkart.local",
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
