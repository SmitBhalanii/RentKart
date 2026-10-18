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

public class BusinessEquipmentManagementTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public BusinessEquipmentManagementTests(WebApplicationFactory<Program> factory)
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
    public async Task CreateEquipment_ApprovedBusiness_CreatesAndPersistsInDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles
            .FirstAsync(b => b.Status == BusinessStatus.Approved && b.ShopName.Contains("Apex Machinery"));

        var category = await db.Categories.FirstAsync(c => c.IsActive && c.Slug == "power-tools");

        var testTitle = $"Milwaukee M18 Fuel Sawzall - Test {Guid.NewGuid():N}";
        var dto = new CreateEquipmentDto
        {
            Title = testTitle,
            Description = "High-demand reciprocating saw with 1-1/8 inch stroke length and Powerstate brushless motor.",
            CategoryId = category.Id,
            DailyRentalRate = 35.50m,
            SecurityDeposit = 100.00m,
            TotalQuantity = 4,
            Condition = EquipmentCondition.LikeNew,
            IsActive = true,
            ImageUrls = new List<string>
            {
                "https://images.unsplash.com/photo-1504148455328-c376907d081c?auto=format&fit=crop&w=800&q=80",
                "https://images.unsplash.com/photo-1581092160607-ee22621dd758?auto=format&fit=crop&w=800&q=80"
            }
        };

        var result = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, dto);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.NotNull(result.EquipmentId);

        // Verify direct persistence in SQL Server
        var created = await db.Equipments
            .Include(e => e.Images)
            .FirstOrDefaultAsync(e => e.Id == result.EquipmentId.Value);

        Assert.NotNull(created);
        Assert.Equal(testTitle, created.Title);
        Assert.Equal(approvedBiz.Id, created.BusinessId);
        Assert.Equal(category.Id, created.CategoryId);
        Assert.Equal(35.50m, created.DailyRentalRate);
        Assert.Equal(100.00m, created.SecurityDeposit);
        Assert.Equal(4, created.TotalQuantity);
        Assert.Equal(EquipmentCondition.LikeNew, created.Condition);
        Assert.Equal(EquipmentStatus.Active, created.Status);
        Assert.True(created.IsActive);
        Assert.Equal(2, created.Images.Count);
        Assert.Contains(created.Images, i => i.IsPrimary);
    }

    [Fact]
    public async Task CreateEquipment_UnapprovedOrSuspendedBusiness_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Pending approval business
        var pendingBiz = await db.BusinessProfiles
            .FirstAsync(b => b.Status == BusinessStatus.PendingApproval);

        var category = await db.Categories.FirstAsync(c => c.IsActive);

        var pendingResult = await equipmentService.CreateEquipmentAsync(pendingBiz.Id, new CreateEquipmentDto
        {
            Title = "Unauthorized Pending Listing",
            Description = "Should be rejected server-side",
            CategoryId = category.Id,
            DailyRentalRate = 25.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 1
        });

        Assert.False(pendingResult.Succeeded);
        Assert.Contains("Only approved businesses", pendingResult.ErrorMessage);

        // 2. Suspended business
        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var suspendedUser = new ApplicationUser
        {
            UserName = $"susp_{testSuffix}@test.rentkart.local",
            Email = $"susp_{testSuffix}@test.rentkart.local",
            FullName = $"Suspended User {testSuffix}",
            UserType = UserRole.Business,
            IsActive = false
        };
        db.Users.Add(suspendedUser);
        await db.SaveChangesAsync();

        var suspendedBiz = new BusinessProfile
        {
            UserId = suspendedUser.Id,
            ShopName = $"Suspended Store {testSuffix}",
            ContactPhone = "555-0199",
            AddressLine = "123 Suspended Way",
            City = "San Jose",
            State = "CA",
            PostalCode = "95112",
            OperatingHours = "Closed",
            Status = BusinessStatus.Suspended,
            SuspendedAtUtc = DateTime.UtcNow
        };
        db.BusinessProfiles.Add(suspendedBiz);
        await db.SaveChangesAsync();

        var suspendedResult = await equipmentService.CreateEquipmentAsync(suspendedBiz.Id, new CreateEquipmentDto
        {
            Title = "Unauthorized Suspended Listing",
            Description = "Should be rejected server-side",
            CategoryId = category.Id,
            DailyRentalRate = 25.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 1
        });

        Assert.False(suspendedResult.Succeeded);
        Assert.Contains("Only approved businesses", suspendedResult.ErrorMessage);
    }

    [Fact]
    public async Task CreateEquipment_InvalidPricingOrQuantity_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var category = await db.Categories.FirstAsync(c => c.IsActive);

        // Daily rate <= 0
        var zeroRateResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = "Invalid Zero Price",
            Description = "Valid description here",
            CategoryId = category.Id,
            DailyRentalRate = 0.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 1
        });
        Assert.False(zeroRateResult.Succeeded);
        Assert.Contains("greater than zero", zeroRateResult.ErrorMessage);

        // Security deposit < 0
        var negDepositResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = "Invalid Negative Deposit",
            Description = "Valid description here",
            CategoryId = category.Id,
            DailyRentalRate = 45.00m,
            SecurityDeposit = -10.00m,
            TotalQuantity = 1
        });
        Assert.False(negDepositResult.Succeeded);
        Assert.Contains("cannot be negative", negDepositResult.ErrorMessage);

        // Total quantity < 1
        var zeroQtyResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = "Invalid Zero Quantity",
            Description = "Valid description here",
            CategoryId = category.Id,
            DailyRentalRate = 45.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 0
        });
        Assert.False(zeroQtyResult.Succeeded);
        Assert.Contains("at least 1", zeroQtyResult.ErrorMessage);
    }

    [Fact]
    public async Task UpdateEquipment_AnotherBusinessEquipment_IsForbidden_OwnershipSecurity()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Business 1 owns equipment
        var biz1 = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved && b.ShopName.Contains("Bay City"));
        var biz2 = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved && b.ShopName.Contains("Apex"));

        var biz1Equipment = await db.Equipments.FirstAsync(e => e.BusinessId == biz1.Id);

        // Biz 2 attempts to edit Biz 1's equipment
        var hijackAttempt = await equipmentService.UpdateEquipmentAsync(biz1Equipment.Id, biz2.Id, new UpdateEquipmentDto
        {
            Title = "Hijacked Listing Title",
            Description = "Attacking ownership",
            CategoryId = biz1Equipment.CategoryId,
            DailyRentalRate = 1.00m,
            SecurityDeposit = 0.00m,
            TotalQuantity = 10,
            Condition = EquipmentCondition.Fair,
            IsActive = true
        });

        Assert.False(hijackAttempt.Succeeded);
        Assert.Contains("not found or you do not have permission", hijackAttempt.ErrorMessage);

        // Verify title remained unchanged in DB
        var unmodified = await db.Equipments.AsNoTracking().FirstAsync(e => e.Id == biz1Equipment.Id);
        Assert.NotEqual("Hijacked Listing Title", unmodified.Title);
    }

    [Fact]
    public async Task UpdateEquipment_InventoryReduction_BlockedWhenConfirmedReservationsExist()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var category = await db.Categories.FirstAsync(c => c.IsActive);

        // 1. Create equipment with 5 total units
        var createResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = $"Reservation Guard Test {Guid.NewGuid():N}",
            Description = "Testing inventory reduction collision guards.",
            CategoryId = category.Id,
            DailyRentalRate = 60.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 5,
            Condition = EquipmentCondition.LikeNew,
            IsActive = true
        });
        Assert.True(createResult.Succeeded);
        var equipmentId = createResult.EquipmentId!.Value;

        // 2. Insert a simulated active customer booking reserving 3 units
        var customerUser = new ApplicationUser
        {
            UserName = $"cust_res_{Guid.NewGuid():N}@test.rentkart.local",
            Email = $"cust_res_{Guid.NewGuid():N}@test.rentkart.local",
            FullName = "Reservation Customer",
            UserType = UserRole.Customer,
            IsActive = true
        };
        db.Users.Add(customerUser);
        await db.SaveChangesAsync();

        var testBookingNumber = $"RK-RES-{Guid.NewGuid():N}"[..12].ToUpper();
        var parentBooking = new Booking
        {
            CustomerId = customerUser.Id,
            BookingNumber = testBookingNumber,
            Status = BookingStatus.Confirmed,
            TotalRentalFee = 360.00m,
            TotalSecurityDeposit = 600.00m,
            GrandTotal = 960.00m,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Bookings.Add(parentBooking);
        await db.SaveChangesAsync();

        var bookingGroup = new BookingGroup
        {
            ParentBookingId = parentBooking.Id,
            BusinessId = approvedBiz.Id,
            GroupNumber = $"{testBookingNumber}-G1",
            Status = BookingGroupStatus.Confirmed,
            PickupVerificationCode = "123456",
            SubtotalRentalFee = 360.00m,
            SubtotalSecurityDeposit = 600.00m,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.BookingGroups.Add(bookingGroup);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var bookingItem = new BookingItem
        {
            BookingGroupId = bookingGroup.Id,
            EquipmentId = equipmentId,
            Quantity = 3, // 3 units reserved!
            StartDate = today.AddDays(2),
            EndDate = today.AddDays(5),
            DaysCount = 3,
            DailyRentalRate = 60.00m,
            ItemSecurityDeposit = 200.00m,
            TotalItemPrice = 360.00m
        };
        db.BookingItems.Add(bookingItem);
        await db.SaveChangesAsync();

        // 3. Attempt to reduce inventory below 3 (e.g. to 2 units) -> MUST BE REJECTED!
        var reduceBelowReservations = await equipmentService.UpdateEquipmentAsync(equipmentId, approvedBiz.Id, new UpdateEquipmentDto
        {
            Title = "Updated Title",
            Description = "Updated Description",
            CategoryId = category.Id,
            DailyRentalRate = 60.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 2, // Less than the 3 reserved units!
            Condition = EquipmentCondition.LikeNew,
            IsActive = true
        });

        Assert.False(reduceBelowReservations.Succeeded);
        Assert.Contains("committed to confirmed or active customer reservations", reduceBelowReservations.ErrorMessage);

        // 4. Reducing inventory to 4 units (still >= 3 reserved) -> MUST SUCCEED!
        var reduceSafely = await equipmentService.UpdateEquipmentAsync(equipmentId, approvedBiz.Id, new UpdateEquipmentDto
        {
            Title = "Updated Title Safely",
            Description = "Updated Description Safely",
            CategoryId = category.Id,
            DailyRentalRate = 60.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 4,
            Condition = EquipmentCondition.LikeNew,
            IsActive = true
        });

        Assert.True(reduceSafely.Succeeded);

        var verify = await db.Equipments.AsNoTracking().FirstAsync(e => e.Id == equipmentId);
        Assert.Equal(4, verify.TotalQuantity);
    }

    [Fact]
    public async Task ToggleEquipmentStatus_TogglesActiveAndInactive_PersistsInDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var equipment = await db.Equipments.FirstAsync(e => e.BusinessId == approvedBiz.Id);

        var initialActive = equipment.IsActive;

        // Toggle 1
        var result1 = await equipmentService.ToggleEquipmentStatusAsync(equipment.Id, approvedBiz.Id);
        Assert.True(result1.Succeeded);

        var toggled1 = await db.Equipments.AsNoTracking().FirstAsync(e => e.Id == equipment.Id);
        Assert.Equal(!initialActive, toggled1.IsActive);
        Assert.Equal(!initialActive ? EquipmentStatus.Active : EquipmentStatus.Inactive, toggled1.Status);

        // Toggle 2 (back to initial)
        var result2 = await equipmentService.ToggleEquipmentStatusAsync(equipment.Id, approvedBiz.Id);
        Assert.True(result2.Succeeded);

        var toggled2 = await db.Equipments.AsNoTracking().FirstAsync(e => e.Id == equipment.Id);
        Assert.Equal(initialActive, toggled2.IsActive);
    }

    [Fact]
    public async Task ImageManagement_AddSetPrimaryAndDelete_WorksCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var category = await db.Categories.FirstAsync(c => c.IsActive);

        var createResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = $"Image Management Test {Guid.NewGuid():N}",
            Description = "Testing image CRUD",
            CategoryId = category.Id,
            DailyRentalRate = 50.00m,
            SecurityDeposit = 100.00m,
            TotalQuantity = 1,
            IsActive = true
        });
        var equipmentId = createResult.EquipmentId!.Value;

        // 1. Invalid image URL rejected
        var invalidUrlResult = await equipmentService.AddEquipmentImageAsync(equipmentId, approvedBiz.Id, "invalid-not-a-url");
        Assert.False(invalidUrlResult.Succeeded);
        Assert.Contains("valid, absolute image URL", invalidUrlResult.ErrorMessage);

        // 2. Add first image (becomes primary)
        var imgUrl1 = "https://images.unsplash.com/photo-1516035069371-29a1b244cc32?w=800";
        var add1 = await equipmentService.AddEquipmentImageAsync(equipmentId, approvedBiz.Id, imgUrl1, isPrimary: true);
        Assert.True(add1.Succeeded);

        // 3. Add second image (non-primary)
        var imgUrl2 = "https://images.unsplash.com/photo-1542751371-adc38448a05e?w=800";
        var add2 = await equipmentService.AddEquipmentImageAsync(equipmentId, approvedBiz.Id, imgUrl2, isPrimary: false);
        Assert.True(add2.Succeeded);

        var images = await db.EquipmentImages.Where(i => i.EquipmentId == equipmentId).OrderBy(i => i.DisplayOrder).ToListAsync();
        Assert.Equal(2, images.Count);
        Assert.True(images[0].IsPrimary);
        Assert.False(images[1].IsPrimary);

        // 4. Set second image as primary
        var setPrimaryResult = await equipmentService.SetPrimaryImageAsync(images[1].Id, equipmentId, approvedBiz.Id);
        Assert.True(setPrimaryResult.Succeeded);

        var updatedImages = await db.EquipmentImages.Where(i => i.EquipmentId == equipmentId).OrderBy(i => i.DisplayOrder).ToListAsync();
        Assert.False(updatedImages[0].IsPrimary);
        Assert.True(updatedImages[1].IsPrimary);

        // 5. Delete an image
        var deleteResult = await equipmentService.DeleteEquipmentImageAsync(images[0].Id, equipmentId, approvedBiz.Id);
        Assert.True(deleteResult.Succeeded);

        var remainingImages = await db.EquipmentImages.Where(i => i.EquipmentId == equipmentId).ToListAsync();
        Assert.Single(remainingImages);
    }

    [Fact]
    public async Task MaintenanceRecords_AddResolveAndDelete_EnforcesInventoryLimits()
    {
        using var scope = _factory.Services.CreateScope();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var category = await db.Categories.FirstAsync(c => c.IsActive);

        var createResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = $"Maintenance Test Gear {Guid.NewGuid():N}",
            Description = "Testing downtime tracking.",
            CategoryId = category.Id,
            DailyRentalRate = 40.00m,
            SecurityDeposit = 100.00m,
            TotalQuantity = 3,
            IsActive = true
        });
        var equipmentId = createResult.EquipmentId!.Value;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // 1. Reject blocked quantity exceeding total capacity
        var invalidQty = await equipmentService.AddMaintenanceRecordAsync(equipmentId, approvedBiz.Id, new AddMaintenanceRecordDto
        {
            BlockedQuantity = 5, // Exceeds total quantity of 3!
            StartDate = today,
            EndDate = today.AddDays(5),
            Reason = "Too many units"
        });
        Assert.False(invalidQty.Succeeded);
        Assert.Contains("cannot exceed total equipment inventory", invalidQty.ErrorMessage);

        // 2. Reject invalid dates (start > end)
        var invalidDates = await equipmentService.AddMaintenanceRecordAsync(equipmentId, approvedBiz.Id, new AddMaintenanceRecordDto
        {
            BlockedQuantity = 1,
            StartDate = today.AddDays(5),
            EndDate = today,
            Reason = "Backwards dates"
        });
        Assert.False(invalidDates.Succeeded);
        Assert.Contains("start date cannot be after end date", invalidDates.ErrorMessage);

        // 3. Add valid maintenance record for 2 units
        var addRecord = await equipmentService.AddMaintenanceRecordAsync(equipmentId, approvedBiz.Id, new AddMaintenanceRecordDto
        {
            BlockedQuantity = 2,
            StartDate = today,
            EndDate = today.AddDays(7),
            Reason = "Firmware update and recalibration",
            EstimatedCost = 75.00m
        });
        Assert.True(addRecord.Succeeded);

        // Available quantity for these dates should now be 3 - 2 = 1!
        var availableDuringMaintenance = await equipmentService.GetAvailableQuantityAsync(equipmentId, today, today.AddDays(7));
        Assert.Equal(1, availableDuringMaintenance);

        // 4. Resolve maintenance record
        var records = await equipmentService.GetMaintenanceRecordsAsync(equipmentId, approvedBiz.Id);
        var record = records.First();
        Assert.False(record.IsResolved);

        var resolveResult = await equipmentService.ResolveMaintenanceRecordAsync(record.Id, equipmentId, approvedBiz.Id);
        Assert.True(resolveResult.Succeeded);

        // Available quantity returns to full 3
        var availableAfterResolved = await equipmentService.GetAvailableQuantityAsync(equipmentId, today, today.AddDays(7));
        Assert.Equal(3, availableAfterResolved);

        // 5. Delete maintenance record
        var deleteResult = await equipmentService.DeleteMaintenanceRecordAsync(record.Id, equipmentId, approvedBiz.Id);
        Assert.True(deleteResult.Succeeded);

        var emptyRecords = await equipmentService.GetMaintenanceRecordsAsync(equipmentId, approvedBiz.Id);
        Assert.Empty(emptyRecords);
    }

    [Fact]
    public async Task WebRoutes_BusinessEquipment_RequiresBusinessRole_AndProtectsEndpoints()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Anonymous access to /Business/Equipment redirects to Login
        var equipmentResponse = await client.GetAsync("/Business/Equipment");
        Assert.Equal(HttpStatusCode.Redirect, equipmentResponse.StatusCode);
        Assert.Contains("/Account/Login", equipmentResponse.Headers.Location?.OriginalString);

        // 2. Anonymous access to /Business/Equipment/Create redirects to Login
        var createResponse = await client.GetAsync("/Business/Equipment/Create");
        Assert.Equal(HttpStatusCode.Redirect, createResponse.StatusCode);
        Assert.Contains("/Account/Login", createResponse.Headers.Location?.OriginalString);
    }
}
