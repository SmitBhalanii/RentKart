using RentKart.Core.Enums;

namespace RentKart.Core.Entities;

public class Booking
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    // Navigation properties
    public virtual ApplicationUser Customer { get; set; } = null!;
    public virtual ICollection<BookingGroup> BookingGroups { get; set; } = new List<BookingGroup>();
    public virtual ICollection<RentalDocument> RentalDocuments { get; set; } = new List<RentalDocument>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
