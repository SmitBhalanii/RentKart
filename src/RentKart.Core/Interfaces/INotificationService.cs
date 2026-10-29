using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class NotificationDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int? BookingId { get; set; }
    public int? BookingGroupId { get; set; }
}

public interface INotificationService
{
    /// <summary>
    /// Creates a persistent notification for a user.
    /// If an eventKey is provided and a notification with the same eventKey already exists for the user,
    /// duplicate notification creation is prevented.
    /// </summary>
    Task<Notification?> CreateNotificationAsync(
        string userId,
        string title,
        string message,
        NotificationType type,
        string? targetUrl = null,
        string? eventKey = null,
        int? bookingId = null,
        int? bookingGroupId = null);

    /// <summary>
    /// Gets paginated notifications for a user, with unread count.
    /// </summary>
    Task<(List<NotificationDto> Items, int TotalCount, int UnreadCount)> GetUserNotificationsAsync(
        string userId,
        bool unreadOnly = false,
        int page = 1,
        int pageSize = 20);

    /// <summary>
    /// Gets the total count of unread notifications for a user (e.g. for navbar badge).
    /// </summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>
    /// Marks a specific notification as read by its recipient.
    /// </summary>
    Task<bool> MarkAsReadAsync(int notificationId, string userId);

    /// <summary>
    /// Marks all unread notifications as read for a user.
    /// </summary>
    Task<int> MarkAllAsReadAsync(string userId);

    /// <summary>
    /// Deletes a notification by its recipient.
    /// </summary>
    Task<bool> DeleteNotificationAsync(int notificationId, string userId);

    /// <summary>
    /// Convenience helper to dispatch notifications to the customer and participating stores when a booking is placed.
    /// </summary>
    Task NotifyBookingCreatedAsync(int bookingId);

    /// <summary>
    /// Convenience helper to dispatch notifications when a store subgroup status changes.
    /// </summary>
    Task NotifyBookingGroupStatusChangedAsync(int bookingGroupId, BookingGroupStatus newStatus, string? note = null);
}
