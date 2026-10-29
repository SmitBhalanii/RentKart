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

public class NotificationAndDocumentTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public NotificationAndDocumentTests(WebApplicationFactory<Program> factory)
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
    public async Task BookingCreation_GeneratesNotificationsForCustomerAndAllParticipatingStores()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // 1. Arrange customer and items from two distinct stores
        var customer = await GetOrCreateCustomerAsync(db);
        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var item1 = await CreateDedicatedTestEquipmentAsync(db, businesses[0].Id);
        var item2 = await CreateDedicatedTestEquipmentAsync(db, businesses[1].Id);
        item1.Business = businesses[0];
        item2.Business = businesses[1];

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(310));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(312));

        int custUnreadBefore = await notificationService.GetUnreadCountAsync(customer.Id);
        int store1UnreadBefore = await notificationService.GetUnreadCountAsync(item1.Business.UserId);
        int store2UnreadBefore = await notificationService.GetUnreadCountAsync(item2.Business.UserId);

        // 2. Act: Create multi-item multi-store booking
        var result = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            CustomerNotes = "Deliver to sound stage",
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(result.Success, result.ErrorMessage);
        int bookingId = result.BookingId!.Value;

        // 3. Assert: Customer received notification
        int custUnreadAfter = await notificationService.GetUnreadCountAsync(customer.Id);
        Assert.True(custUnreadAfter > custUnreadBefore, "Customer unread count should have increased.");

        var (custNotifications, _, _) = await notificationService.GetUserNotificationsAsync(customer.Id, unreadOnly: true);
        var custBookingNotice = custNotifications.FirstOrDefault(n => n.BookingId == bookingId);
        Assert.NotNull(custBookingNotice);
        Assert.Contains("Reservation Placed", custBookingNotice.Title);
        Assert.Equal(NotificationType.BookingPlaced, custBookingNotice.Type);
        Assert.False(custBookingNotice.IsRead);

        // 4. Assert: Store 1 owner received notification
        int store1UnreadAfter = await notificationService.GetUnreadCountAsync(item1.Business.UserId);
        Assert.True(store1UnreadAfter > store1UnreadBefore, "Store 1 unread count should have increased.");
        var (store1Notifications, _, _) = await notificationService.GetUserNotificationsAsync(item1.Business.UserId, unreadOnly: true);
        var store1Notice = store1Notifications.FirstOrDefault(n => n.BookingId == bookingId);
        Assert.NotNull(store1Notice);
        Assert.Contains("New Reservation Voucher", store1Notice.Title);

        // 5. Assert: Store 2 owner received notification
        int store2UnreadAfter = await notificationService.GetUnreadCountAsync(item2.Business.UserId);
        Assert.True(store2UnreadAfter > store2UnreadBefore, "Store 2 unread count should have increased.");
        var (store2Notifications, _, _) = await notificationService.GetUserNotificationsAsync(item2.Business.UserId, unreadOnly: true);
        var store2Notice = store2Notifications.FirstOrDefault(n => n.BookingId == bookingId);
        Assert.NotNull(store2Notice);
        Assert.Contains("New Reservation Voucher", store2Notice.Title);
    }

    [Fact]
    public async Task NotificationUnreadStatus_AndMarkAsRead_DecreasesUnreadCount()
    {
        using var scope = _factory.Services.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);

        // 1. Create a notification
        var created = await notificationService.CreateNotificationAsync(
            customer.Id,
            "System Maintenance Alert",
            "Platform scheduled maintenance this weekend.",
            NotificationType.SystemNotice);

        Assert.NotNull(created);
        Assert.False(created.IsRead);
        Assert.Null(created.ReadAtUtc);

        int unreadBefore = await notificationService.GetUnreadCountAsync(customer.Id);
        Assert.True(unreadBefore >= 1);

        // 2. Mark specific notification as read
        bool markSuccess = await notificationService.MarkAsReadAsync(created.Id, customer.Id);
        Assert.True(markSuccess);

        // 3. Verify status in database
        var refreshed = await db.Notifications.FindAsync(created.Id);
        Assert.NotNull(refreshed);
        Assert.True(refreshed.IsRead);
        Assert.NotNull(refreshed.ReadAtUtc);

        int unreadAfter = await notificationService.GetUnreadCountAsync(customer.Id);
        Assert.Equal(unreadBefore - 1, unreadAfter);
    }

    [Fact]
    public async Task DuplicateNotificationSuppression_SameEventKey_CreatesOnlyOneNotification()
    {
        using var scope = _factory.Services.CreateScope();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        string eventKey = $"IdempotencyTest:{Guid.NewGuid():N}";

        // 1. First trigger
        var first = await notificationService.CreateNotificationAsync(
            customer.Id,
            "Booking Confirmed",
            "Your booking was confirmed.",
            NotificationType.BookingConfirmed,
            targetUrl: "/Booking/Details/999",
            eventKey: eventKey);

        Assert.NotNull(first);

        // 2. Second trigger with identical eventKey
        var second = await notificationService.CreateNotificationAsync(
            customer.Id,
            "Booking Confirmed (Duplicate)",
            "Your booking was confirmed (Duplicate).",
            NotificationType.BookingConfirmed,
            targetUrl: "/Booking/Details/999",
            eventKey: eventKey);

        // Duplicate event suppression returns null and prevents duplicate creation
        Assert.Null(second);

        // 3. Verify exactly one record exists in DB for this eventKey
        int count = await db.Notifications.CountAsync(n => n.EventKey == eventKey && n.UserId == customer.Id);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task BookingCreation_GeneratesRentalAgreementsAndInvoicesWithAuthoritativeSnapshots()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var documentService = scope.ServiceProvider.GetRequiredService<IRentalDocumentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await CreateDedicatedTestEquipmentAsync(db);
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(150));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(152)); // 3 days

        // 1. Act: Create booking
        var result = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 2, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(result.Success, result.ErrorMessage);
        int bookingId = result.BookingId!.Value;

        var parentBooking = await db.Bookings
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .FirstAsync(b => b.Id == bookingId);

        var group = parentBooking.BookingGroups.First();

        // 2. Assert: Parent Booking Consolidated Invoice was created
        var parentDoc = await db.RentalDocuments
            .FirstOrDefaultAsync(d => d.BookingId == bookingId && d.DocumentType == RentalDocumentType.ParentBookingInvoice);

        Assert.NotNull(parentDoc);
        Assert.True(parentDoc.IsGenerated);
        Assert.Equal(parentBooking.TotalRentalFee, parentDoc.RentalSubtotal);
        Assert.Equal(parentBooking.TotalSecurityDeposit, parentDoc.SecurityDeposit);
        Assert.Equal(parentBooking.GrandTotal, parentDoc.GrandTotal);
        Assert.Contains(parentBooking.BookingNumber, parentDoc.Content);
        Assert.Contains("Consolidated Rental Invoice", parentDoc.Content);

        // 3. Assert: Store Invoice was created
        var storeInvoice = await db.RentalDocuments
            .FirstOrDefaultAsync(d => d.BookingGroupId == group.Id && d.DocumentType == RentalDocumentType.StoreInvoice);

        Assert.NotNull(storeInvoice);
        Assert.True(storeInvoice.IsGenerated);
        Assert.Equal(group.SubtotalRentalFee, storeInvoice.RentalSubtotal);
        Assert.Equal(group.SubtotalSecurityDeposit, storeInvoice.SecurityDeposit);
        Assert.Contains(group.GroupNumber, storeInvoice.Content);

        // 4. Assert: Rental Agreement was created with liability terms & snapshots
        var agreement = await db.RentalDocuments
            .FirstOrDefaultAsync(d => d.BookingGroupId == group.Id && d.DocumentType == RentalDocumentType.RentalAgreement);

        Assert.NotNull(agreement);
        Assert.True(agreement.IsGenerated);
        Assert.Contains(group.GroupNumber, agreement.Content);
        Assert.Contains("Equipment Rental Agreement", agreement.Content);
        Assert.Contains("Terms & Conditions", agreement.Content);
        Assert.Contains(item.Title, agreement.Content);
        Assert.Contains("Security Deposit & Damage Liability", agreement.Content);
    }

    [Fact]
    public async Task StoreStatusTransition_GeneratesLifecycleNotifications()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await CreateDedicatedTestEquipmentAsync(db);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(160));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(162));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);

        // 1. Accept booking group -> Customer receives BookingConfirmed notification
        var acceptResult = await bookingService.AcceptBookingGroupAsync(group.Id, group.BusinessId);
        Assert.True(acceptResult.Success);

        var (custNotices1, _, _) = await notificationService.GetUserNotificationsAsync(customer.Id);
        var confirmNotice = custNotices1.FirstOrDefault(n => n.BookingGroupId == group.Id && n.Type == NotificationType.BookingConfirmed);
        Assert.NotNull(confirmNotice);
        Assert.Contains("Confirmed", confirmNotice.Title);

        // 2. Handover -> Customer receives EquipmentHandedOver notification
        var handoverResult = await bookingService.HandoverBookingGroupAsync(group.Id, group.BusinessId, group.PickupVerificationCode, "Inspection passed");
        Assert.True(handoverResult.Success);

        var (custNotices2, _, _) = await notificationService.GetUserNotificationsAsync(customer.Id);
        var handoverNotice = custNotices2.FirstOrDefault(n => n.BookingGroupId == group.Id && n.Type == NotificationType.EquipmentHandedOver);
        Assert.NotNull(handoverNotice);
        Assert.Contains("Equipment Picked Up", handoverNotice.Title);
        Assert.Contains("collected", handoverNotice.Message, StringComparison.OrdinalIgnoreCase);

        // 3. Return & Inspection -> Customer receives EquipmentReturned notification
        var returnResult = await bookingService.CompleteReturnBookingGroupAsync(group.Id, group.BusinessId, "Returned in perfect condition");
        Assert.True(returnResult.Success);

        var (custNotices3, _, _) = await notificationService.GetUserNotificationsAsync(customer.Id);
        var returnNotice = custNotices3.FirstOrDefault(n => n.BookingGroupId == group.Id && n.Type == NotificationType.EquipmentReturned);
        Assert.NotNull(returnNotice);
        Assert.Contains("Rental Returned", returnNotice.Title);
    }

    [Fact]
    public async Task StrictTenantAccessIsolation_DocumentSecurity()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var documentService = scope.ServiceProvider.GetRequiredService<IRentalDocumentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var businesses = await db.BusinessProfiles.Where(b => b.Status == BusinessStatus.Approved).Take(2).ToListAsync();
        var item1 = await CreateDedicatedTestEquipmentAsync(db, businesses[0].Id);
        var item2 = await CreateDedicatedTestEquipmentAsync(db, businesses[1].Id);
        item1.Business = businesses[0];
        item2.Business = businesses[1];

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(200));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(202));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item1.Id, Quantity = 1, StartDate = startDate, EndDate = endDate },
                new() { EquipmentId = item2.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        int bookingId = createResult.BookingId!.Value;

        var parentDoc = await db.RentalDocuments
            .FirstAsync(d => d.BookingId == bookingId && d.DocumentType == RentalDocumentType.ParentBookingInvoice);

        var store1Doc = await db.RentalDocuments
            .FirstAsync(d => d.BookingId == bookingId && d.BusinessId == item1.BusinessId && d.DocumentType == RentalDocumentType.RentalAgreement);

        var store2Doc = await db.RentalDocuments
            .FirstAsync(d => d.BookingId == bookingId && d.BusinessId == item2.BusinessId && d.DocumentType == RentalDocumentType.RentalAgreement);

        // Security Test A: Customer can access their own parent and store documents
        var custAccessParent = await documentService.GetDocumentDetailsAsync(parentDoc.Id, customer.Id, isAdmin: false);
        Assert.NotNull(custAccessParent);

        var custAccessStore1 = await documentService.GetDocumentDetailsAsync(store1Doc.Id, customer.Id, isAdmin: false);
        Assert.NotNull(custAccessStore1);

        // Security Test B: Store 1 owner CANNOT access Store 2 agreement
        var store1AccessStore2 = await documentService.GetDocumentDetailsAsync(store2Doc.Id, item1.Business.UserId, isAdmin: false);
        Assert.Null(store1AccessStore2);

        // Security Test C: Store 1 owner CANNOT access Parent Consolidated Invoice (protects other stores' pricing & margins)
        var store1AccessParent = await documentService.GetDocumentDetailsAsync(parentDoc.Id, item1.Business.UserId, isAdmin: false);
        Assert.Null(store1AccessParent);

        // Security Test D: Store 1 owner CAN access their own store agreement
        var store1AccessStore1 = await documentService.GetDocumentDetailsAsync(store1Doc.Id, item1.Business.UserId, isAdmin: false);
        Assert.NotNull(store1AccessStore1);

        // Security Test E: Another customer CANNOT access this customer's documents
        var otherCustomer = new ApplicationUser
        {
            UserName = $"other_cust_{Guid.NewGuid():N}@rentkart.local",
            Email = $"other_cust_{Guid.NewGuid():N}@rentkart.local",
            FullName = "Other Customer",
            UserType = UserRole.Customer,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.Users.Add(otherCustomer);
        await db.SaveChangesAsync();

        var otherCustAccessParent = await documentService.GetDocumentDetailsAsync(parentDoc.Id, otherCustomer.Id, isAdmin: false);
        Assert.Null(otherCustAccessParent);

        var otherCustAccessStore1 = await documentService.GetDocumentDetailsAsync(store1Doc.Id, otherCustomer.Id, isAdmin: false);
        Assert.Null(otherCustAccessStore1);

        // Security Test F: Administrator can access any document
        var adminAccessParent = await documentService.GetDocumentDetailsAsync(parentDoc.Id, "admin-id", isAdmin: true);
        Assert.NotNull(adminAccessParent);

        var adminAccessStore1 = await documentService.GetDocumentDetailsAsync(store1Doc.Id, "admin-id", isAdmin: true);
        Assert.NotNull(adminAccessStore1);
    }

    [Fact]
    public async Task DocumentGenerationFailureHandling_AndRetryRegeneration()
    {
        using var scope = _factory.Services.CreateScope();
        var bookingService = scope.ServiceProvider.GetRequiredService<IBookingService>();
        var documentService = scope.ServiceProvider.GetRequiredService<IRentalDocumentService>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var customer = await GetOrCreateCustomerAsync(db);
        var item = await CreateDedicatedTestEquipmentAsync(db);

        var startDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(220));
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(222));

        var createResult = await bookingService.CreateBookingAsync(customer.Id, new CreateBookingRequestDto
        {
            Items = new List<CreateBookingItemRequestDto>
            {
                new() { EquipmentId = item.Id, Quantity = 1, StartDate = startDate, EndDate = endDate }
            }
        });

        Assert.True(createResult.Success);
        var group = await db.BookingGroups.FirstAsync(bg => bg.ParentBookingId == createResult.BookingId!.Value);

        // Retrieve existing agreement
        var agreement = await db.RentalDocuments
            .FirstAsync(d => d.BookingGroupId == group.Id && d.DocumentType == RentalDocumentType.RentalAgreement);

        // 1. Simulate a failed state on this document
        agreement.IsGenerated = false;
        agreement.Content = string.Empty;
        agreement.FailureReason = "Simulated rendering timeout / IO exception";
        await db.SaveChangesAsync();

        // 2. Attempt download -> should gracefully fail with the recorded failure reason
        var downloadResult = await documentService.GetDocumentForDownloadAsync(agreement.Id, customer.Id, isAdmin: false);
        Assert.False(downloadResult.Success);
        Assert.Contains("Simulated rendering timeout", downloadResult.ErrorMessage);

        // 3. Retry regeneration -> should succeed and restore document content
        var retryResult = await documentService.RetryDocumentGenerationAsync(agreement.Id, customer.Id, isAdmin: false);
        Assert.True(retryResult.Success, retryResult.ErrorMessage);

        // 4. Verify refreshed state in DB
        var refreshedAgreement = await db.RentalDocuments.FindAsync(agreement.Id);
        Assert.NotNull(refreshedAgreement);
        Assert.True(refreshedAgreement.IsGenerated);
        Assert.Null(refreshedAgreement.FailureReason);
        Assert.False(string.IsNullOrWhiteSpace(refreshedAgreement.Content));

        // 5. Download now succeeds
        var retryDownload = await documentService.GetDocumentForDownloadAsync(agreement.Id, customer.Id, isAdmin: false);
        Assert.True(retryDownload.Success);
        Assert.True(retryDownload.FileBytes.Length > 0);
        Assert.Equal(agreement.FileName, retryDownload.FileName);
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

        var category = await db.Categories.FirstAsync();
        var item = new Equipment
        {
            BusinessId = biz.Id,
            CategoryId = category.Id,
            Title = $"Dedicated Test Item {Guid.NewGuid():N}",
            Description = "Phase 9 test equipment listing",
            DailyRentalRate = 65.00m,
            SecurityDeposit = 120.00m,
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
