namespace RentKart.Core.Entities;

public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

    // Navigation property
    public virtual ApplicationUser? User { get; set; }
}
