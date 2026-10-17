namespace RentKart.Core.Entities;

public class BookingItem
{
    public int Id { get; set; }
    public int BookingGroupId { get; set; }
    public int EquipmentId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysCount { get; set; }
    public decimal DailyRentalRate { get; set; }
    public decimal ItemSecurityDeposit { get; set; }
    public decimal TotalItemPrice { get; set; }

    // Navigation properties
    public virtual BookingGroup BookingGroup { get; set; } = null!;
    public virtual Equipment Equipment { get; set; } = null!;
    public virtual Review? Review { get; set; }
}
