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

public class BookingLifecycleTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public BookingLifecycleTests(WebApplicationFactory<Program> factory)
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
    public async Task CreateBooking_MultiItemMultiBusiness_CreatesParentAndIndependentChildGroups()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Get customer
        var customer = await GetOrCreateCustomerAsync(db);

        // 2. Get two items from two different approved businesses
        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var item1 = await CreateDedicatedTestEquipmentAsync(db, businesses[0].Id);
        var item2 = await CreateDedicatedTestEquipmentAsync(db, businesses[1].Id);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(22)); // 3 days

        var request = new CreateBookingRequestDto
        {
            CustomerNotes = "Handle with care, commercial project",
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        };

        // Act
        var result = await bookingService.CreateBookingAsync(customer.Id, request);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.BookingId);
        Assert.StartsWith("BK-", result.BookingNumber);

        // Verify direct database persistence
        var parentBooking = await db.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .FirstOrDefaultAsync(b => b.Id == result.BookingId.Value);

        Assert.NotNull(parentBooking);
        Assert.Equal(customer.Id, parentBooking.CustomerId);
        Assert.Equal(BookingStatus.Pending, parentBooking.Status);

        // Must have exactly two independent booking groups, one per business
        Assert.Equal(2, parentBooking.BookingGroups.Count);
        Assert.Contains(parentBooking.BookingGroups, g => g.BusinessId == item1.BusinessId);
        Assert.Contains(parentBooking.BookingGroups, g => g.BusinessId == item2.BusinessId);

        // Verify independent status and verification codes
        foreach (var group in parentBooking.BookingGroups)
        {
            Assert.Equal(BookingGroupStatus.PendingApproval, group.Status);
            Assert.StartsWith("GRP-", group.GroupNumber);
            Assert.StartsWith("RK-", group.PickupVerificationCode);
            Assert.NotEmpty(group.BookingItems);
            Assert.True(group.SubtotalRentalFee > 0);
            Assert.True(group.SubtotalSecurityDeposit >= 0);
        }

        // Verify parent aggregates
        var expectedRentalFee = parentBooking.BookingGroups.Sum(g => g.SubtotalRentalFee);
        var expectedDeposit = parentBooking.BookingGroups.Sum(g => g.SubtotalSecurityDeposit);
        Assert.Equal(expectedRentalFee, parentBooking.TotalRentalFee);
        Assert.Equal(expectedDeposit, parentBooking.TotalSecurityDeposit);
        Assert.Equal(expectedRentalFee + expectedDeposit, parentBooking.GrandTotal);

        // Verify snapshots on items
        var savedItem1 = parentBooking.BookingGroups.SelectMany(g => g.BookingItems).First(i => i.EquipmentId == item1.Id);
        Assert.Equal(item1.DailyRentalRate, savedItem1.DailyRentalRate);
        Assert.Equal(item1.SecurityDeposit * 1, savedItem1.ItemSecurityDeposit);
        Assert.Equal(3, savedItem1.DaysCount);
        Assert.Equal(item1.DailyRentalRate * 1 * 3, savedItem1.TotalItemPrice);
    }

    [Fact]
    public async Task MixedApprovalOutcomes_BusinessAAccepts_BusinessBRejects_UpdatesAggregateStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);

        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var item1 = await CreateDedicatedTestEquipmentAsync(db, businesses[0].Id);
        var item2 = await CreateDedicatedTestEquipmentAsync(db, businesses[1].Id);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(32));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var booking = await db.Bookings.Include(b => b.BookingGroups).FirstAsync(b => b.Id == createResult.BookingId!.Value);

        var groupA = booking.BookingGroups.First(g => g.BusinessId == item1.BusinessId);
        var groupB = booking.BookingGroups.First(g => g.BusinessId == item2.BusinessId);

        // 1. Business A accepts their group
        var acceptResult = await bookingService.AcceptBookingGroupAsync(groupA.Id, item1.BusinessId);
        Assert.True(acceptResult.Success, acceptResult.ErrorMessage);
        Assert.Equal(BookingGroupStatus.Confirmed, acceptResult.NewStatus);

        // Parent should now be PartiallyConfirmed (since group B is still PendingApproval)
        var updatedBooking = await db.Bookings.AsNoTracking().FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.PartiallyConfirmed, updatedBooking.Status);

        // 2. Business B declines/rejects their group
        var rejectResult = await bookingService.RejectBookingGroupAsync(groupB.Id, item2.BusinessId, "Item undergoing unexpected service");
        Assert.True(rejectResult.Success, rejectResult.ErrorMessage);
        Assert.Equal(BookingGroupStatus.Rejected, rejectResult.NewStatus);

        // Parent remains PartiallyConfirmed (since group A is Confirmed and group B is Rejected)
        var finalBooking = await db.Bookings
            .Include(b => b.BookingGroups)
            .AsNoTracking()
            .FirstAsync(b => b.Id == booking.Id);

        Assert.Equal(BookingStatus.PartiallyConfirmed, finalBooking.Status);

        var reloadedGroupB = finalBooking.BookingGroups.First(g => g.Id == groupB.Id);
        Assert.Equal(BookingGroupStatus.Rejected, reloadedGroupB.Status);
        Assert.Contains("unexpected service", reloadedGroupB.RejectionReason);

        // Inventory for Business B's rejected equipment is now free
        var availB = await bookingService.GetAuthoritativeAvailableQuantityAsync(item2.Id, startDate, endDate);
        Assert.True(availB >= 1, "Rejected booking must immediately release held inventory.");
    }

    [Fact]
    public async Task InventoryExhaustion_RejectsWhenRequestedExceedsAvailable()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        // Create an equipment with exactly 1 unit
        var scarceItem = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Rare Drone - {Guid.NewGuid():N}",
            Description = "Single unit drone",
            DailyRentalRate = 120.00m,
            SecurityDeposit = 300.00m,
            TotalQuantity = 1,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(scarceItem);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(42));

        // 1. Customer 1 reserves the single unit
        var res1 = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = scarceItem.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(res1.Success);

        // 2. Customer 2 tries to reserve the same unit on overlapping dates
        var res2 = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = scarceItem.Id, Quantity = 1, StartDate = startDate.AddDays(1), EndDate = endDate.AddDays(1) }
            }
        });

        // Must be cleanly rejected with inventory exhaustion error
        Assert.False(res2.Success);
        Assert.Contains("Insufficient units available", res2.ErrorMessage);
    }

    [Fact]
    public async Task MaintenanceConflict_OverlappingMaintenanceDeductsAvailableQuantity()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        // Create an equipment with 2 units
        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Generator - {Guid.NewGuid():N}",
            Description = "Portable Generator",
            DailyRentalRate = 60.00m,
            SecurityDeposit = 150.00m,
            TotalQuantity = 2,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(55));

        // Add maintenance blocking 1 unit over [startDate, endDate]
        var maintenance = new MaintenanceRecord
        {
            EquipmentId = item.Id,
            BlockedQuantity = 1,
            StartDate = startDate,
            EndDate = endDate,
            Reason = "Scheduled oil filter overhaul",
            IsResolved = false,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.MaintenanceRecords.Add(maintenance);
        await db.SaveChangesAsync();

        // Total 2, blocked 1 -> available 1.
        // Trying to book 2 units must fail
        var failRes = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 2, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.False(failRes.Success);
        Assert.Contains("Insufficient units available", failRes.ErrorMessage);

        // Booking 1 unit must succeed
        var okRes = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(okRes.Success);
    }

    [Fact]
    public async Task Cancellation_ReleasesInventoryForSubsequentReservations()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Spotting Scope - {Guid.NewGuid():N}",
            Description = "Precision Scope",
            DailyRentalRate = 45.00m,
            SecurityDeposit = 100.00m,
            TotalQuantity = 1,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(62));

        // 1. Customer reserves
        var res1 = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(res1.Success);

        // Check availability is now 0
        var availBeforeCancel = await bookingService.GetAuthoritativeAvailableQuantityAsync(item.Id, startDate, endDate);
        Assert.Equal(0, availBeforeCancel);

        // 2. Customer cancels group
        var booking = await db.Bookings.Include(b => b.BookingGroups).FirstAsync(b => b.Id == res1.BookingId!.Value);
        var group = booking.BookingGroups.First();

        var cancelRes = await bookingService.CancelBookingGroupByCustomerAsync(group.Id, customer.Id, "Plans changed");
        Assert.True(cancelRes.Success);
        Assert.Equal(BookingGroupStatus.CancelledByCustomer, cancelRes.NewStatus);

        // Availability must be restored to 1
        var availAfterCancel = await bookingService.GetAuthoritativeAvailableQuantityAsync(item.Id, startDate, endDate);
        Assert.Equal(1, availAfterCancel);

        // Subsequent booking for same dates succeeds
        var res2 = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(res2.Success);
    }

    [Fact]
    public async Task HandoverAndCompletionLifecycle_FullStateProgression()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Concrete Mixer - {Guid.NewGuid():N}",
            Description = "Commercial mixer",
            DailyRentalRate = 55.00m,
            SecurityDeposit = 120.00m,
            TotalQuantity = 1,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(72));

        var createRes = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(createRes.Success);

        var booking = await db.Bookings.Include(b => b.BookingGroups).FirstAsync(b => b.Id == createRes.BookingId!.Value);
        var group = booking.BookingGroups.First();

        // 1. Business accepts
        var acceptRes = await bookingService.AcceptBookingGroupAsync(group.Id, biz.Id);
        Assert.True(acceptRes.Success);

        // 2. Handover attempt with WRONG code fails
        var failHandover = await bookingService.HandoverBookingGroupAsync(group.Id, biz.Id, "RK-WRONGCODE", "Clean");
        Assert.False(failHandover.Success);
        Assert.Contains("Invalid pickup verification code", failHandover.ErrorMessage);

        // 3. Handover with CORRECT code succeeds
        var okHandover = await bookingService.HandoverBookingGroupAsync(group.Id, biz.Id, group.PickupVerificationCode, "All accessories checked");
        Assert.True(okHandover.Success);
        Assert.Equal(BookingGroupStatus.HandedOver, okHandover.NewStatus);

        // Parent booking should now be Active
        var activeBooking = await db.Bookings.AsNoTracking().FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Active, activeBooking.Status);

        // 4. Return inspection and complete
        var completeRes = await bookingService.CompleteReturnBookingGroupAsync(group.Id, biz.Id, "Returned in perfect condition. Deposit fully approved.");
        Assert.True(completeRes.Success);
        Assert.Equal(BookingGroupStatus.Completed, completeRes.NewStatus);

        // Parent booking should now be Completed
        var completedBooking = await db.Bookings.AsNoTracking().FirstAsync(b => b.Id == booking.Id);
        Assert.Equal(BookingStatus.Completed, completedBooking.Status);

        // Units released back to available inventory
        var availFinal = await bookingService.GetAuthoritativeAvailableQuantityAsync(item.Id, startDate, endDate);
        Assert.Equal(1, availFinal);
    }

    [Fact]
    public async Task Security_TenantIsolation_BusinessCannotManageOtherBusinessBooking()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var bizA = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved && b.ShopName.Contains("Apex"));
        var bizB = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved && b.Id != bizA.Id);

        var itemA = await CreateDedicatedTestEquipmentAsync(db, bizA.Id);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(80));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(82));

        var createRes = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = itemA.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });
        Assert.True(createRes.Success);

        var booking = await db.Bookings.Include(b => b.BookingGroups).FirstAsync(b => b.Id == createRes.BookingId!.Value);
        var groupA = booking.BookingGroups.First();

        // Business B attempts to accept Business A's group
        var unauthorizedAccept = await bookingService.AcceptBookingGroupAsync(groupA.Id, bizB.Id);
        Assert.False(unauthorizedAccept.Success);
        Assert.Contains("access denied", unauthorizedAccept.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Business B attempts to reject Business A's group
        var unauthorizedReject = await bookingService.RejectBookingGroupAsync(groupA.Id, bizB.Id, "Unauthorized reject");
        Assert.False(unauthorizedReject.Success);
        Assert.Contains("access denied", unauthorizedReject.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DatabaseRollback_OnInventoryFailureInMultiItemCheckout()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        // Item 1 has 5 units
        var item1 = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Item 1 Plenty - {Guid.NewGuid():N}",
            Description = "5 units",
            DailyRentalRate = 20.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 5,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Item 2 has only 1 unit
        var item2 = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Item 2 Scarce - {Guid.NewGuid():N}",
            Description = "1 unit",
            DailyRentalRate = 20.00m,
            SecurityDeposit = 50.00m,
            TotalQuantity = 1,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.Equipments.AddRange(item1, item2);
        await db.SaveChangesAsync();

        var initialBookingsCount = await db.Bookings.CountAsync();
        var initialGroupsCount = await db.BookingGroups.CountAsync();
        var initialItemsCount = await db.BookingItems.CountAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(92));

        // Checkout requests 1 of item1 (valid) and 99 of item2 (FATAL inventory failure)
        var res = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 99, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.False(res.Success);

        // Entire transaction must rollback: zero new bookings, groups, or items
        var finalBookingsCount = await db.Bookings.CountAsync();
        var finalGroupsCount = await db.BookingGroups.CountAsync();
        var finalItemsCount = await db.BookingItems.CountAsync();

        Assert.Equal(initialBookingsCount, finalBookingsCount);
        Assert.Equal(initialGroupsCount, finalGroupsCount);
        Assert.Equal(initialItemsCount, finalItemsCount);
    }

    [Fact]
    public async Task Concurrency_ParallelReservationAttempts_LocksAndPreventsOverselling()
    {
        using var setupScope = _factory.Services.CreateScope();
        var db = setupScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var biz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);

        // Exactly 1 unit available
        var singleUnitItem = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Exclusive Camera - {Guid.NewGuid():N}",
            Description = "Only one in inventory",
            DailyRentalRate = 150.00m,
            SecurityDeposit = 500.00m,
            TotalQuantity = 1,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(singleUnitItem);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(102));

        // Launch two concurrent checkout operations on separate scopes/threads
        var task1 = Task.Run(async () =>
        {
            using var scope1 = _factory.Services.CreateScope();
            var service1 = scope1.ServiceProvider.GetRequiredService<IBookingService>();
            return await service1.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
            {
                Items = new List<CreateBookingItemRequestDto>
                {
                    new() { EquipmentId = singleUnitItem.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
                }
            });
        });

        var task2 = Task.Run(async () =>
        {
            using var scope2 = _factory.Services.CreateScope();
            var service2 = scope2.ServiceProvider.GetRequiredService<IBookingService>();
            return await service2.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
            {
                Items = new List<CreateBookingItemRequestDto>
                {
                    new() { EquipmentId = singleUnitItem.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
                }
            });
        });

        var results = await Task.WhenAll(task1, task2);

        // Concurrency verification:
        // Exactly ONE must succeed and ONE must fail
        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failCount);

        // Verify direct database state: only 1 booking item created for this equipment on these dates
        using var verifyScope = _factory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservedCount = await verifyDb.BookingItems
            .Include(bi => bi.BookingGroup)
            .Where(bi => bi.EquipmentId == singleUnitItem.Id &&
                         bi.StartDate <= endDate &&
                         bi.EndDate >= startDate &&
                         (bi.BookingGroup.Status == BookingGroupStatus.PendingApproval ||
                          bi.BookingGroup.Status == BookingGroupStatus.Confirmed ||
                          bi.BookingGroup.Status == BookingGroupStatus.HandedOver))
            .SumAsync(bi => bi.Quantity);

        Assert.Equal(1, reservedCount);
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

        var category = await db.Categories.FirstAsync(c => c.IsActive);
        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Dedicated Booking Test - {Guid.NewGuid():N}",
            Description = "Phase 7/8/9 booking test equipment listing",
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
}
