using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(ApplicationDbContext dbContext, ILogger<NotificationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Notification?> CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        string? targetUrl = null,
        string? eventKey = null,
        int? bookingId = null,
        int? bookingGroupId = null)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        // Duplicate event suppression
        if (!string.IsNullOrWhiteSpace(eventKey))
        {
            var exists = await _dbContext.Notifications
                .AnyAsync(n => n.UserId == userId && n.EventKey == eventKey.Trim());

            if (exists)
            {
                _logger.LogInformation("Suppressed duplicate notification for User {UserId} with EventKey {EventKey}.", userId, eventKey);
                return null;
            }
        }

        var notification = new Notification
        {
            UserId = userId,
            Title = title.Trim(),
            Message = message.Trim(),
            TargetUrl = targetUrl?.Trim(),
            Type = type,
            EventKey = eventKey?.Trim(),
            BookingId = bookingId,
            BookingGroupId = bookingGroupId,
            IsRead = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Notifications.Add(notification);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Created notification {NotificationId} for User {UserId}: {Title}", notification.Id, userId, title);
        return notification;
    }

    public async Task<(List<NotificationDto> Items, int TotalCount, int UnreadCount)> GetUserNotificationsAsync(
        string userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return (new List<NotificationDto>(), 0, 0);
        }

        var baseQuery = _dbContext.Notifications.Where(n => n.UserId == userId);

        int totalCount = await baseQuery.CountAsync();
        int unreadCount = await baseQuery.CountAsync(n => !n.IsRead);

        var query = baseQuery;
        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((Math.Max(1, page) - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                TargetUrl = n.TargetUrl,
                Type = n.Type,
                IsRead = n.IsRead,
                ReadAtUtc = n.ReadAtUtc,
                CreatedAtUtc = n.CreatedAtUtc,
                BookingId = n.BookingId,
                BookingGroupId = n.BookingGroupId
            })
            .ToListAsync();

        return (items, totalCount, unreadCount);
    }

    public async Task<int> GetUnreadCountAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;

        return await _dbContext.Notifications
            .CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();
        }

        return true;
    }

    public async Task<int> MarkAllAsReadAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return 0;

        var unread = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        if (unread.Count == 0) return 0;

        var now = DateTime.UtcNow;
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAtUtc = now;
        }

        await _dbContext.SaveChangesAsync();
        return unread.Count;
    }

    public async Task<bool> DeleteNotificationAsync(int notificationId, string userId)
    {
        var notification = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId);

        if (notification == null) return false;

        _dbContext.Notifications.Remove(notification);
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task NotifyBookingCreatedAsync(int bookingId)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Customer)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.Business)
            .Include(b => b.BookingGroups)
                .ThenInclude(bg => bg.BookingItems)
            .FirstOrDefaultAsync(b => b.Id == bookingId);

        if (booking == null) return;

        // 1. Notify Customer of reservation placement
        await CreateNotificationAsync(
            userId: booking.CustomerId,
            title: $"Reservation Placed: #{booking.BookingNumber}",
            message: $"Your reservation across {booking.BookingGroups.Count} participating store(s) was submitted. Total agreed quotation: ${booking.GrandTotal:F2}.",
            type: NotificationType.BookingPlaced,
            targetUrl: $"/Booking/Details/{booking.Id}",
            eventKey: $"Booking:{booking.Id}:Created",
            bookingId: booking.Id);

        // 2. Notify each participating store of incoming voucher request
        foreach (var group in booking.BookingGroups)
        {
            if (group.Business?.UserId != null)
            {
                await CreateNotificationAsync(
                    userId: group.Business.UserId,
                    title: $"New Reservation Voucher: #{group.GroupNumber}",
                    message: $"Customer {booking.Customer.FullName} requested {group.BookingItems.Count} item(s). Subtotal: ${group.SubtotalRentalFee:F2}.",
                    type: NotificationType.BookingPlaced,
                    targetUrl: $"/Business/Bookings/Group/{group.Id}",
                    eventKey: $"BookingGroup:{group.Id}:Created",
                    bookingId: booking.Id,
                    bookingGroupId: group.Id);
            }
        }
    }

    public async Task NotifyBookingGroupStatusChangedAsync(int bookingGroupId, BookingGroupStatus newStatus, string? note = null)
    {
        var group = await _dbContext.BookingGroups
            .Include(bg => bg.Business)
            .Include(bg => bg.ParentBooking)
                .ThenInclude(b => b.Customer)
            .FirstOrDefaultAsync(bg => bg.Id == bookingGroupId);

        if (group == null) return;

        string customerUserId = group.ParentBooking.CustomerId;
        string storeUserId = group.Business.UserId;
        string storeName = group.Business.ShopName;

        switch (newStatus)
        {
            case BookingGroupStatus.Confirmed:
                await CreateNotificationAsync(
                    userId: customerUserId,
                    title: $"Reservation Confirmed: {storeName}",
                    message: $"{storeName} confirmed reservation #{group.GroupNumber}. Voucher Pickup Code: {group.PickupVerificationCode}.",
                    type: NotificationType.BookingConfirmed,
                    targetUrl: $"/Booking/Details/{group.ParentBookingId}",
                    eventKey: $"BookingGroup:{group.Id}:Confirmed",
                    bookingId: group.ParentBookingId,
                    bookingGroupId: group.Id);
                break;

            case BookingGroupStatus.Rejected:
                string reason = !string.IsNullOrWhiteSpace(note) ? note : (group.RejectionReason ?? "Store declined.");
                await CreateNotificationAsync(
                    userId: customerUserId,
                    title: $"Reservation Declined: {storeName}",
                    message: $"{storeName} declined reservation #{group.GroupNumber}. Reason: {reason}",
                    type: NotificationType.BookingRejected,
                    targetUrl: $"/Booking/Details/{group.ParentBookingId}",
                    eventKey: $"BookingGroup:{group.Id}:Rejected",
                    bookingId: group.ParentBookingId,
                    bookingGroupId: group.Id);
                break;

            case BookingGroupStatus.HandedOver:
                await CreateNotificationAsync(
                    userId: customerUserId,
                    title: $"Equipment Picked Up: {storeName}",
                    message: $"Equipment for reservation #{group.GroupNumber} collected from {storeName}. Active rental has started.",
                    type: NotificationType.EquipmentHandedOver,
                    targetUrl: $"/Booking/Details/{group.ParentBookingId}",
                    eventKey: $"BookingGroup:{group.Id}:HandedOver",
                    bookingId: group.ParentBookingId,
                    bookingGroupId: group.Id);
                break;

            case BookingGroupStatus.Completed:
                await CreateNotificationAsync(
                    userId: customerUserId,
                    title: $"Rental Returned & Settled: {storeName}",
                    message: $"Return inspection and deposit settlement finalized for #{group.GroupNumber} at {storeName}.",
                    type: NotificationType.EquipmentReturned,
                    targetUrl: $"/Booking/Details/{group.ParentBookingId}",
                    eventKey: $"BookingGroup:{group.Id}:Completed",
                    bookingId: group.ParentBookingId,
                    bookingGroupId: group.Id);
                break;

            case BookingGroupStatus.CancelledByCustomer:
                await CreateNotificationAsync(
                    userId: storeUserId,
                    title: $"Reservation Cancelled by Customer: #{group.GroupNumber}",
                    message: $"Customer {group.ParentBooking.Customer.FullName} cancelled reservation #{group.GroupNumber}. Reserved inventory released.",
                    type: NotificationType.BookingCancelled,
                    targetUrl: $"/Business/Bookings/Group/{group.Id}",
                    eventKey: $"BookingGroup:{group.Id}:Cancelled",
                    bookingId: group.ParentBookingId,
                    bookingGroupId: group.Id);
                break;
        }
    }
}
