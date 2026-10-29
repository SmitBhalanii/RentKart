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

public class HandoverAndReturnLifecycleTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public HandoverAndReturnLifecycleTests(WebApplicationFactory<Program> factory)
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
    public async Task Handover_WithChecklistAndConditionNotes_TransitionsToHandedOverAndActivatesParent()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await db.Equipments.Include(e => e.Business).FirstAsync(e => e.IsActive && e.Business.Status == BusinessStatus.Approved);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(40));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(42));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success, createResult.ErrorMessage);

        var group = await db.BookingGroups.Include(bg => bg.BookingItems).FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);

        // 1. Accept booking group
        var acceptResult = await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);
        Assert.True(acceptResult.Success);
        Assert.Equal(BookingGroupStatus.Confirmed, acceptResult.NewStatus);

        // 2. Perform detailed handover
        var bookingItem = group.BookingItems.First();
        var handoverRequest = new HandoverBookingGroupRequestDto
        {
            VerificationCode = group.PickupVerificationCode,
            GeneralHandoverNotes = "Customer showed driving license. Battery 100%.",
            Items = new List<HandoverItemCheckDto>
            {
                new()
                {
                    BookingItemId = bookingItem.Id,
                    QuantityHandedOver = 1,
                    AccessoriesConfirmed = true,
                    ConditionNotes = "Flawless cosmetic condition, test recording successful"
                }
            }
        };

        var handoverResult = await bookingService.HandoverBookingGroupDetailedAsync(group.Id, group.BusinessId, handoverRequest);

        // Assert
        Assert.True(handoverResult.Success, handoverResult.ErrorMessage);
        Assert.Equal(BookingGroupStatus.HandedOver, handoverResult.NewStatus);

        // Verify direct database persistence
        var updatedGroup = await db.BookingGroups
            .Include(bg => bg.ParentBooking)
            .FirstAsync(bg => bg.Id == group.Id);

        Assert.Equal(BookingGroupStatus.HandedOver, updatedGroup.Status);
        Assert.NotNull(updatedGroup.HandedOverAtUtc);
        Assert.Contains("Flawless cosmetic condition", updatedGroup.HandoverNotes);
        Assert.Contains("Accessories: Verified", updatedGroup.HandoverNotes);

        // Parent booking must be Active
        Assert.Equal(BookingStatus.Active, updatedGroup.ParentBooking.Status);

        // Audit log check
        var audit = await db.AuditLogs
            .FirstOrDefaultAsync(a => a.Action == "EquipmentHandover" && a.EntityId == group.Id.ToString());
        Assert.NotNull(audit);
    }

    [Fact]
    public async Task Handover_WrongVerificationCode_FailsGracefullyWithoutTransition()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await db.Equipments.Include(e => e.Business).FirstAsync(e => e.IsActive && e.Business.Status == BusinessStatus.Approved);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(45));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(47));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);

        // Act with invalid code
        var result = await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, "RK-999999-WRONG", "Attempted handover");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Invalid pickup verification code", result.ErrorMessage);

        var checkGroup = await db.BookingGroups.FindAsync(group.Id);
        Assert.Equal(BookingGroupStatus.Confirmed, checkGroup!.Status);
        Assert.Null(checkGroup.HandedOverAtUtc);
    }

    [Fact]
    public async Task Handover_UnauthorizedBusiness_CannotPerformHandover()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var storeA = businesses[0];
        var storeB = businesses[1];

        var itemA = await db.Equipments.FirstAsync(e => e.BusinessId == storeA.Id && e.IsActive);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(50));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(52));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = itemA.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, storeA.Id);

        // Act: Store B tries to perform handover for Store A's group
        var unauthorizedResult = await bookingService.HandoverBookingGroupAsync(group.Id, storeB.Id, group.PickupVerificationCode, "Malicious attempt");

        // Assert
        Assert.False(unauthorizedResult.Success);
        Assert.Contains("access denied", unauthorizedResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var checkGroup = await db.BookingGroups.FindAsync(group.Id);
        Assert.Equal(BookingGroupStatus.Confirmed, checkGroup!.Status);
    }

    [Fact]
    public async Task Handover_InvalidTransition_CannotHandoverWhenPending()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await db.Equipments.Include(e => e.Business).FirstAsync(e => e.IsActive && e.Business.Status == BusinessStatus.Approved);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(55));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(57));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        // Do NOT accept (status is still PendingApproval)

        var result = await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, group.PickupVerificationCode, "Premature handover");

        Assert.False(result.Success);
        Assert.Contains("must be Confirmed", result.ErrorMessage);
    }

    [Fact]
    public async Task CompleteReturn_CleanReturn_ReleasesDepositAndRestoresInventoryWithoutMaintenance()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await db.Equipments.Include(e => e.Business).FirstAsync(e => e.IsActive && e.Business.Status == BusinessStatus.Approved);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(60));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(62));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.Include(bg => bg.BookingItems).FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);
        await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, group.PickupVerificationCode, "Handed over clean");

        // Act: Clean return inspection
        var bookingItem = group.BookingItems.First();
        var returnRequest = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Perfect condition returned on time.",
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = bookingItem.Id,
                    ReturnedQuantity = 1,
                    MissingQuantity = 0,
                    Condition = "Good",
                    HasDamage = false
                }
            }
        };

        var returnResult = await bookingService.CompleteReturnWithInspectionAsync(group.Id, group.BusinessId, returnRequest);

        // Assert
        Assert.True(returnResult.Success, returnResult.ErrorMessage);
        Assert.Equal(BookingGroupStatus.Completed, returnResult.NewStatus);
        Assert.Equal(group.SubtotalSecurityDeposit, returnResult.TotalDepositHeld);
        Assert.Equal(0, returnResult.TotalDamageFeeAssessed);
        Assert.Equal(group.SubtotalSecurityDeposit, returnResult.DepositRefundAmount);
        Assert.Equal(0, returnResult.ForfeitedDepositAmount);
        Assert.Equal(0, returnResult.MaintenanceRecordsCreatedCount);

        var updatedGroup = await db.BookingGroups.Include(bg => bg.ParentBooking).FirstAsync(bg => bg.Id == group.Id);
        Assert.Equal(BookingGroupStatus.Completed, updatedGroup.Status);
        Assert.NotNull(updatedGroup.ReturnedAtUtc);
        Assert.NotNull(updatedGroup.CompletedAtUtc);
        Assert.Equal(BookingStatus.Completed, updatedGroup.ParentBooking.Status);

        // Verify inventory availability is not restricted by any maintenance
        var avail = await bookingService.GetAuthoritativeAvailableQuantityAsync(item.Id, startDate, endDate);
        Assert.Equal(item.TotalQuantity, avail);
    }

    [Fact]
    public async Task CompleteReturn_DamagedItem_CreatesMaintenanceRecordBlocksQuantityAndDeductsDeposit()
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
            Title = $"Damage Test Cine Lens - {Guid.NewGuid():N}",
            Description = "Cinema Prime Lens",
            DailyRentalRate = 50.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 3,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(70));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(72));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.Include(bg => bg.BookingItems).FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);
        await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, group.PickupVerificationCode, "Handed over");

        var bookingItem = group.BookingItems.First();
        decimal repairCost = 150m;

        var returnRequest = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Severe drop damage observed on lens mount.",
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = bookingItem.Id,
                    ReturnedQuantity = 1,
                    MissingQuantity = 0,
                    Condition = "Damaged",
                    HasDamage = true,
                    DamageDescription = "Cracked lens mount and misaligned optics",
                    EstimatedRepairCost = repairCost,
                    PhotoEvidenceUrl = "https://rentkart.local/evidence/dmg-001.jpg",
                    CreateMaintenanceBlock = true,
                    EstimatedMaintenanceDays = 14
                }
            }
        };

        // Act
        var returnResult = await bookingService.CompleteReturnWithInspectionAsync(group.Id, group.BusinessId, returnRequest);

        // Assert
        Assert.True(returnResult.Success, returnResult.ErrorMessage);
        Assert.Equal(1, returnResult.MaintenanceRecordsCreatedCount);
        Assert.NotEmpty(returnResult.CreatedMaintenanceRecordIds);

        // Deposit calculations
        decimal expectedForfeited = Math.Min(group.SubtotalSecurityDeposit, repairCost);
        decimal expectedRefund = Math.Max(0, group.SubtotalSecurityDeposit - expectedForfeited);
        Assert.Equal(expectedForfeited, returnResult.ForfeitedDepositAmount);
        Assert.Equal(expectedRefund, returnResult.DepositRefundAmount);

        // Verify MaintenanceRecord exists in DB
        var createdRecordId = returnResult.CreatedMaintenanceRecordIds.First();
        var maintenanceRecord = await db.MaintenanceRecords.FindAsync(createdRecordId);
        Assert.NotNull(maintenanceRecord);
        Assert.Equal(item.Id, maintenanceRecord.EquipmentId);
        Assert.Equal(1, maintenanceRecord.BlockedQuantity);
        Assert.False(maintenanceRecord.IsResolved);
        Assert.Equal(repairCost, maintenanceRecord.EstimatedCost);
        Assert.Contains("Cracked lens mount", maintenanceRecord.Reason);

        // Verify maintenance consequences: the damaged unit is blocked from available inventory during maintenance downtime!
        var maintenanceStart = maintenanceRecord.StartDate;
        var maintenanceEnd = maintenanceRecord.EndDate;
        var availableDuringMaintenance = await bookingService.GetAuthoritativeAvailableQuantityAsync(item.Id, maintenanceStart, maintenanceEnd);

        // Available quantity must be reduced by exactly BlockedQuantity (1)
        Assert.Equal(item.TotalQuantity - 1, availableDuringMaintenance);
    }

    [Fact]
    public async Task CompleteReturn_MissingItems_HandlesMissingUnitsAndForfeitsDeposit()
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
            Title = $"Missing Test Projector - {Guid.NewGuid():N}",
            Description = "Test Projector for Missing Item Tests",
            DailyRentalRate = 60.00m,
            SecurityDeposit = 250.00m,
            TotalQuantity = 4,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(80));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(82));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 2, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.Include(bg => bg.BookingItems).FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);
        await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, group.PickupVerificationCode, "Handed over 2 units");

        var bookingItem = group.BookingItems.First();

        var returnRequest = new CompleteReturnBookingGroupRequestDto
        {
            GeneralInspectionNotes = "Customer reported 1 unit was stolen at event.",
            AssessedDamageFee = group.SubtotalSecurityDeposit, // Total forfeiture for lost equipment
            Items = new List<ReturnItemInspectionDto>
            {
                new()
                {
                    BookingItemId = bookingItem.Id,
                    ReturnedQuantity = 1,
                    MissingQuantity = 1,
                    Condition = "MissingParts",
                    HasDamage = true,
                    DamageDescription = "1 unit lost / stolen",
                    EstimatedRepairCost = group.SubtotalSecurityDeposit,
                    CreateMaintenanceBlock = true,
                    EstimatedMaintenanceDays = 30
                }
            }
        };

        // Act
        var result = await bookingService.CompleteReturnWithInspectionAsync(group.Id, group.BusinessId, returnRequest);

        // Assert
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(group.SubtotalSecurityDeposit, result.ForfeitedDepositAmount);
        Assert.Equal(0, result.DepositRefundAmount);
        Assert.Equal(1, result.MaintenanceRecordsCreatedCount);

        var updatedGroup = await db.BookingGroups.FindAsync(group.Id);
        Assert.Equal(BookingGroupStatus.Completed, updatedGroup!.Status);
        Assert.Contains("Missing: 1", updatedGroup.ReturnInspectionNotes);
    }

    [Fact]
    public async Task CompleteReturn_UnauthorizedBusiness_Forbidden()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var storeA = businesses[0];
        var storeB = businesses[1];

        var itemA = await db.Equipments.FirstAsync(e => e.BusinessId == storeA.Id && e.IsActive);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(92));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = itemA.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, storeA.Id);
        await bookingService.HandoverBookingGroupAsync(group.Id, storeA.Id, group.PickupVerificationCode, "Handed over by Store A");

        // Act: Store B tries to return & inspect Store A's equipment
        var result = await bookingService.CompleteReturnWithInspectionAsync(group.Id, storeB.Id, new CompleteReturnBookingGroupRequestDto());

        // Assert
        Assert.False(result.Success);
        Assert.Contains("access denied", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        var checkGroup = await db.BookingGroups.FindAsync(group.Id);
        Assert.Equal(BookingGroupStatus.HandedOver, checkGroup!.Status);
    }

    [Fact]
    public async Task MultiStoreParentAggregation_StoreACompletionDoesNotCompleteStoreB_AndCompletesWhenAllFinish()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var activeItems = await db.Equipments
            .Include(e => e.Business)
            .Where(e => e.IsActive && e.Business.Status == BusinessStatus.Approved)
            .ToListAsync();

        var item1 = activeItems.First();
        var item2 = activeItems.First(e => e.BusinessId != item1.BusinessId);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(100));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(102));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var parentBookingId = createResult.BookingId!.Value;

        var groups = await db.BookingGroups.Where(bg => bg.ParentBookingId == parentBookingId).ToListAsync();
        var groupA = groups.First(g => g.BusinessId == item1.BusinessId);
        var groupB = groups.First(g => g.BusinessId == item2.BusinessId);

        // Both stores accept
        await bookingService.AcceptBookingGroupAsync(groupA.Id, groupA.BusinessId);
        await bookingService.AcceptBookingGroupAsync(groupB.Id, groupB.BusinessId);

        // Both stores handover
        await bookingService.HandoverBookingGroupAsync(groupA.Id, groupA.BusinessId, groupA.PickupVerificationCode, "Handed over by Store A");
        await bookingService.HandoverBookingGroupAsync(groupB.Id, groupB.BusinessId, groupB.PickupVerificationCode, "Handed over by Store B");

        // Verify parent is Active
        var parent = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Active, parent!.Status);

        // Store A completes return
        var returnAResult = await bookingService.CompleteReturnBookingGroupAsync(groupA.Id, groupA.BusinessId, "Clean return Store A");
        Assert.True(returnAResult.Success);

        // Critical requirement check: Business A's return must NEVER complete Business B's subgroup!
        var verifyGroupB = await db.BookingGroups.FindAsync(groupB.Id);
        Assert.Equal(BookingGroupStatus.HandedOver, verifyGroupB!.Status);

        // Parent booking must REMAIN Active (not Completed yet!)
        var verifyParentMid = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Active, verifyParentMid!.Status);

        // Now Store B completes return
        var returnBResult = await bookingService.CompleteReturnBookingGroupAsync(groupB.Id, groupB.BusinessId, "Clean return Store B");
        Assert.True(returnBResult.Success);

        // Parent booking becomes Completed only when EVERY applicable accepted subgroup has reached Completed!
        var verifyParentFinal = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Completed, verifyParentFinal!.Status);
    }

    [Fact]
    public async Task MultiStoreParentAggregation_WithCancelledOrRejectedGroup_PermitsCompletion()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var activeItems = await db.Equipments
            .Include(e => e.Business)
            .Where(e => e.IsActive && e.Business.Status == BusinessStatus.Approved)
            .ToListAsync();

        var item1 = activeItems.First();
        var item2 = activeItems.First(e => e.BusinessId != item1.BusinessId);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(110));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(112));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var parentBookingId = createResult.BookingId!.Value;

        var groups = await db.BookingGroups.Where(bg => bg.ParentBookingId == parentBookingId).ToListAsync();
        var groupA = groups.First(g => g.BusinessId == item1.BusinessId);
        var groupB = groups.First(g => g.BusinessId == item2.BusinessId);

        // Store A accepts; Store B rejects
        await bookingService.AcceptBookingGroupAsync(groupA.Id, groupA.BusinessId);
        await bookingService.RejectBookingGroupAsync(groupB.Id, groupB.BusinessId, "Equipment undergoing unscheduled maintenance");

        // Store A hands over
        await bookingService.HandoverBookingGroupAsync(groupA.Id, groupA.BusinessId, groupA.PickupVerificationCode, "Handed over by Store A");

        // Store A completes return
        await bookingService.CompleteReturnBookingGroupAsync(groupA.Id, groupA.BusinessId, "Clean return Store A");

        // Parent booking becomes Completed because every applicable accepted subgroup reached defined terminal state!
        var parent = await db.Bookings.FindAsync(parentBookingId);
        Assert.Equal(BookingStatus.Completed, parent!.Status);
    }

    [Fact]
    public async Task Handover_PartialPickupAndExcessQuantityValidation()
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
            Title = $"Partial Pickup Drone - {Guid.NewGuid():N}",
            Description = "Commercial Quadcopter",
            DailyRentalRate = 75.00m,
            SecurityDeposit = 200.00m,
            TotalQuantity = 5,
            IsActive = true,
            Status = EquipmentStatus.Active,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Equipments.Add(item);
        await db.SaveChangesAsync();

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(120));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(122));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 3, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var group = await db.BookingGroups.Include(bg => bg.BookingItems).FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);
        await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);

        var bookingItemId = group.BookingItems.First().Id;

        // 1. Attempt invalid handover with excess quantity (> requested 3)
        var invalidExcessRequest = new HandoverBookingGroupRequestDto
        {
            VerificationCode = group.PickupVerificationCode,
            Items = new List<HandoverItemCheckDto>
            {
                new() { BookingItemId = bookingItemId, QuantityHandedOver = 4, AccessoriesConfirmed = true }
            }
        };
        var excessResult = await bookingService.HandoverBookingGroupDetailedAsync(group.Id, group.BusinessId, invalidExcessRequest);
        Assert.False(excessResult.Success);
        Assert.Contains("Invalid handover quantity", excessResult.ErrorMessage);

        // 2. Perform valid partial pickup (2 out of 3 handed over)
        var validPartialRequest = new HandoverBookingGroupRequestDto
        {
            VerificationCode = group.PickupVerificationCode,
            GeneralHandoverNotes = "Customer opted to take only 2 units; remaining unit left in store.",
            Items = new List<HandoverItemCheckDto>
            {
                new() { BookingItemId = bookingItemId, QuantityHandedOver = 2, AccessoriesConfirmed = true, ConditionNotes = "2 units verified with chargers" }
            }
        };
        var partialResult = await bookingService.HandoverBookingGroupDetailedAsync(group.Id, group.BusinessId, validPartialRequest);
        Assert.True(partialResult.Success);
        Assert.Equal(BookingGroupStatus.HandedOver, partialResult.NewStatus);

        var updatedGroup = await db.BookingGroups.FindAsync(group.Id);
        Assert.Contains("Qty: 2/3", updatedGroup!.HandoverNotes);
        Assert.Contains("2 units verified with chargers", updatedGroup.HandoverNotes);
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
}
