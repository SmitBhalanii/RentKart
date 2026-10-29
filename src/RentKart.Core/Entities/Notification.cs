using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class Notification
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TargetUrl { get; set; }
    public NotificationType Type { get; set; } = NotificationType.SystemNotice;
    public string? EventKey { get; set; }
    public int? BookingId { get; set; }
    public int? BookingGroupId { get; set; }
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual ApplicationUser User { get; set; } = null!;
    public virtual Booking? Booking { get; set; }
    public virtual BookingGroup? BookingGroup { get; set; }
}
