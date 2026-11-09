using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ApplicationDbContext dbContext, ILogger<ReviewService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ReviewEligibilityResult> CheckEligibilityAsync(int bookingItemId, string customerUserId)
    {
        var item = await _dbContext.BookingItems
            .Include(bi => bi.BookingGroup)
                .ThenInclude(bg => bg.ParentBooking)
            .Include(bi => bi.BookingGroup)
                .ThenInclude(bg => bg.Business)
            .Include(bi => bi.Equipment)
                .ThenInclude(e => e.Category)
            .Include(bi => bi.Equipment)
                .ThenInclude(e => e.Images)
            .Include(bi => bi.Review)
            .FirstOrDefaultAsync(bi => bi.Id == bookingItemId);

        if (item == null)
        {
            return new ReviewEligibilityResult
            {
                CanReview = false,
                Reason = "Booking item not found."
            };
        }

        if (item.BookingGroup.ParentBooking.CustomerId != customerUserId)
        {
            return new ReviewEligibilityResult
            {
                CanReview = false,
                Reason = "You can only review rentals from your own bookings."
            };
        }

        if (item.BookingGroup.Status != BookingGroupStatus.Completed)
        {
            return new ReviewEligibilityResult
            {
                CanReview = false,
                Reason = "Reviews can only be submitted after the rental is completed and returned."
            };
        }

        if (item.Review != null)
        {
            return new ReviewEligibilityResult
            {
                CanReview = false,
                Reason = "You have already submitted a review for this rental item."
            };
        }

        var primaryImage = item.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault(img => img.IsPrimary)?.ImageUrl
                        ?? item.Equipment.Images.OrderBy(img => img.DisplayOrder).FirstOrDefault()?.ImageUrl;

        return new ReviewEligibilityResult
        {
            CanReview = true,
            BookingItemId = item.Id,
            EquipmentId = item.EquipmentId,
            EquipmentTitle = item.Equipment.Title,
            PrimaryImageUrl = primaryImage,
            CategoryName = item.Equipment.Category.Name,
            ShopName = item.BookingGroup.Business.ShopName,
            DailyRentalRate = item.DailyRentalRate,
            StartDate = item.StartDate,
            EndDate = item.EndDate,
            DaysCount = item.DaysCount,
            Quantity = item.Quantity
        };
    }

    public async Task<ReviewOperationResult> SubmitReviewAsync(SubmitReviewRequestDto request, string customerUserId)
    {
        var eligibility = await CheckEligibilityAsync(request.BookingItemId, customerUserId);
        if (!eligibility.CanReview)
        {
            return ReviewOperationResult.Fail(eligibility.Reason ?? "Not eligible to review this item.");
        }

        if (request.Rating < 1 || request.Rating > 5)
        {
            return ReviewOperationResult.Fail("Rating must be between 1 and 5 stars.");
        }

        if (string.IsNullOrWhiteSpace(request.Comment) || request.Comment.Trim().Length < 5)
        {
            return ReviewOperationResult.Fail("Review comment must be at least 5 characters long.");
        }

        if (request.Comment.Trim().Length > 1000)
        {
            return ReviewOperationResult.Fail("Review comment cannot exceed 1,000 characters.");
        }

        var review = new Review
        {
            BookingItemId = request.BookingItemId,
            CustomerId = customerUserId,
            EquipmentId = eligibility.EquipmentId,
            Rating = request.Rating,
            Comment = request.Comment.Trim(),
            IsHidden = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _dbContext.Reviews.Add(review);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Review {ReviewId} created for Equipment {EquipmentId} by Customer {CustomerId} with Rating {Rating}",
            review.Id, eligibility.EquipmentId, customerUserId, request.Rating);

        return ReviewOperationResult.Ok(review.Id);
    }

    public async Task<EquipmentReviewsSummaryDto> GetEquipmentReviewsSummaryAsync(int equipmentId)
    {
        var reviews = await _dbContext.Reviews
            .Where(r => r.EquipmentId == equipmentId && !r.IsHidden)
            .Select(r => r.Rating)
            .ToListAsync();

        var summary = new EquipmentReviewsSummaryDto
        {
            TotalReviews = reviews.Count,
            AverageRating = reviews.Count > 0 ? Math.Round(reviews.Average(), 1) : 0.0,
            RatingCounts = new Dictionary<int, int>
            {
                { 5, reviews.Count(r => r == 5) },
                { 4, reviews.Count(r => r == 4) },
                { 3, reviews.Count(r => r == 3) },
                { 2, reviews.Count(r => r == 2) },
                { 1, reviews.Count(r => r == 1) }
            }
        };

        return summary;
    }

    public async Task<List<ReviewDto>> GetEquipmentReviewsAsync(int equipmentId, int take = 20)
    {
        return await _dbContext.Reviews
            .Include(r => r.Customer)
            .Where(r => r.EquipmentId == equipmentId && !r.IsHidden)
            .OrderByDescending(r => r.CreatedAtUtc)
            .Take(take)
            .Select(r => new ReviewDto
            {
                Id = r.Id,
                BookingItemId = r.BookingItemId,
                EquipmentId = r.EquipmentId,
                Rating = r.Rating,
                Comment = r.Comment,
                CustomerName = r.Customer.FullName,
                CreatedAtUtc = r.CreatedAtUtc,
                IsHidden = r.IsHidden
            })
            .ToListAsync();
    }

    public async Task<List<AdminReviewListItemDto>> GetReviewsForAdminAsync(bool? isHiddenFilter = null, int? ratingFilter = null)
    {
        var query = _dbContext.Reviews
            .Include(r => r.Customer)
            .Include(r => r.Equipment)
                .ThenInclude(e => e.Business)
            .AsQueryable();

        if (isHiddenFilter.HasValue)
        {
            query = query.Where(r => r.IsHidden == isHiddenFilter.Value);
        }

        if (ratingFilter.HasValue)
        {
            query = query.Where(r => r.Rating == ratingFilter.Value);
        }

        return await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Select(r => new AdminReviewListItemDto
            {
                Id = r.Id,
                BookingItemId = r.BookingItemId,
                EquipmentId = r.EquipmentId,
                EquipmentTitle = r.Equipment.Title,
                ShopName = r.Equipment.Business.ShopName,
                CustomerId = r.CustomerId,
                CustomerName = r.Customer.FullName,
                CustomerEmail = r.Customer.Email ?? string.Empty,
                Rating = r.Rating,
                Comment = r.Comment,
                IsHidden = r.IsHidden,
                ModerationReason = r.ModerationReason,
                CreatedAtUtc = r.CreatedAtUtc,
                ModeratedAtUtc = r.ModeratedAtUtc
            })
            .ToListAsync();
    }

    public async Task<bool> ModerateReviewAsync(int reviewId, bool isHidden, string? reason, string adminUserId, string? ipAddress = null)
    {
        var review = await _dbContext.Reviews.FindAsync(reviewId);
        if (review == null)
        {
            return false;
        }

        var oldHidden = review.IsHidden;
        review.IsHidden = isHidden;
        review.ModerationReason = reason;
        review.ModeratedAtUtc = DateTime.UtcNow;

        var validUserId = await _dbContext.Users.AnyAsync(u => u.Id == adminUserId) ? adminUserId : null;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = validUserId,
            Action = isHidden ? "HideReview" : "UnhideReview",
            EntityType = nameof(Review),
            EntityId = review.Id.ToString(),
            Details = $"{{\"OldHidden\": {oldHidden.ToString().ToLowerInvariant()}, \"NewHidden\": {isHidden.ToString().ToLowerInvariant()}, \"Reason\": \"{reason ?? string.Empty}\", \"AdminUser\": \"{adminUserId}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();
        _logger.LogInformation("Review {ReviewId} moderated (IsHidden: {IsHidden}) by Admin {AdminUserId}", reviewId, isHidden, adminUserId);
        return true;
    }
}
