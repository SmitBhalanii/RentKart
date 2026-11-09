using System.ComponentModel.DataAnnotations;

namespace RentKart.Web.Models.Reviews;

public class ReviewCreateViewModel
{
    [Required]
    public int BookingItemId { get; set; }

    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public string? PrimaryImageUrl { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysCount { get; set; }
    public int Quantity { get; set; }

    [Required(ErrorMessage = "Please select a star rating.")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5 stars.")]
    public int Rating { get; set; } = 5;

    [Required(ErrorMessage = "Please share your experience with this equipment.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Review must be between 5 and 1,000 characters.")]
    public string Comment { get; set; } = string.Empty;
}
