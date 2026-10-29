using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class RentalDocument
{
    public int Id { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public RentalDocumentType DocumentType { get; set; }
    public int? BookingId { get; set; }
    public int? BookingGroupId { get; set; }
    public int? BusinessId { get; set; }
    public string CustomerUserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "text/html";
    public string Content { get; set; } = string.Empty;
    public decimal RentalSubtotal { get; set; }
    public decimal SecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public bool IsGenerated { get; set; } = true;
    public string? FailureReason { get; set; }
    public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Booking? Booking { get; set; }
    public virtual BookingGroup? BookingGroup { get; set; }
    public virtual BusinessProfile? Business { get; set; }
    public virtual ApplicationUser CustomerUser { get; set; } = null!;
}
