using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class BookingGroup
{
    public int Id { get; set; }
    public int ParentBookingId { get; set; }
    public int BusinessId { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public BookingGroupStatus Status { get; set; } = BookingGroupStatus.PendingApproval;
    public decimal SubtotalRentalFee { get; set; }
    public decimal SubtotalSecurityDeposit { get; set; }
    public string PickupVerificationCode { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public DateTime? HandedOverAtUtc { get; set; }
    public string? ReturnInspectionNotes { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual Booking ParentBooking { get; set; } = null!;
    public virtual BusinessProfile Business { get; set; } = null!;
    public virtual ICollection<BookingItem> BookingItems { get; set; } = new List<BookingItem>();
    public virtual ICollection<RentalDocument> RentalDocuments { get; set; } = new List<RentalDocument>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
