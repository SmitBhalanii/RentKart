namespace RentKart.Core.Interfaces;

public class ReviewEligibilityResult
{
    public bool CanReview { get; set; }
    public string? Reason { get; set; }
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
}

public class SubmitReviewRequestDto
{
    public int BookingItemId { get; set; }
    public int Rating { get; set; } // 1 to 5
    public string Comment { get; set; } = string.Empty;
}

public class ReviewOperationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int? ReviewId { get; set; }

    public static ReviewOperationResult Ok(int reviewId) => new() { Success = true, ReviewId = reviewId };
    public static ReviewOperationResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}

public class EquipmentReviewsSummaryDto
{
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public Dictionary<int, int> RatingCounts { get; set; } = new();
}

public class ReviewDto
{
    public int Id { get; set; }
    public int BookingItemId { get; set; }
    public int EquipmentId { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsHidden { get; set; }
}

public class AdminReviewListItemDto
{
    public int Id { get; set; }
    public int BookingItemId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public string? ModerationReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ModeratedAtUtc { get; set; }
}

public interface IReviewService
{
    Task<ReviewEligibilityResult> CheckEligibilityAsync(int bookingItemId, string customerUserId);
    Task<ReviewOperationResult> SubmitReviewAsync(SubmitReviewRequestDto request, string customerUserId);
    Task<EquipmentReviewsSummaryDto> GetEquipmentReviewsSummaryAsync(int equipmentId);
    Task<List<ReviewDto>> GetEquipmentReviewsAsync(int equipmentId, int take = 20);
    Task<List<AdminReviewListItemDto>> GetReviewsForAdminAsync(bool? isHiddenFilter = null, int? ratingFilter = null);
    Task<bool> ModerateReviewAsync(int reviewId, bool isHidden, string? reason, string adminUserId, string? ipAddress = null);
}

