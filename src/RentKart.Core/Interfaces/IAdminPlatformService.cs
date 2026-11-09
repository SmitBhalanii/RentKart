using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public class AdminDashboardMetricsDto
{
    public int TotalUsersCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public int TotalBusinessesCount { get; set; }
    public int PendingBusinessesCount { get; set; }
    public int ApprovedBusinessesCount { get; set; }
    public int SuspendedBusinessesCount { get; set; }
    public int RejectedBusinessesCount { get; set; }

    public int TotalEquipmentCount { get; set; }
    public int ActiveEquipmentCount { get; set; }
    public int InactiveEquipmentCount { get; set; }
    public int MaintenanceEquipmentCount { get; set; }

    public int TotalBookingsCount { get; set; }
    public int PendingBookingsCount { get; set; }
    public int ConfirmedBookingsCount { get; set; }
    public int ActiveBookingsCount { get; set; }
    public int CompletedBookingsCount { get; set; }
    public int CancelledBookingsCount { get; set; }

    public decimal TotalRentalFeeVolume { get; set; }
    public decimal TotalSecurityDepositVolume { get; set; }
    public decimal TotalPlatformTurnover { get; set; }

    public int TotalReviewsCount { get; set; }
    public int VisibleReviewsCount { get; set; }
    public int HiddenReviewsCount { get; set; }
    public double AverageRating { get; set; }

    public int ActiveDamageReportsCount { get; set; }
}

public class AdminCustomerListItemDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsLockedOut { get; set; }
    public int TotalBookingsCount { get; set; }
    public int TotalReviewsCount { get; set; }
}

public class AdminCustomerBookingSummaryDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public BookingStatus Status { get; set; }
    public decimal GrandTotal { get; set; }
    public int GroupsCount { get; set; }
}

public class AdminCustomerReviewSummaryDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Comment { get; set; } = string.Empty;
    public bool IsHidden { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminCustomerDetailDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsLockedOut { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public List<AdminCustomerBookingSummaryDto> Bookings { get; set; } = new();
    public List<AdminCustomerReviewSummaryDto> Reviews { get; set; } = new();
}

public class AdminCategoryDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int? ParentCategoryId { get; set; }
    public string? ParentCategoryName { get; set; }
    public int EquipmentCount { get; set; }
}

public class AdminCreateCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }
    public int DisplayOrder { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    public int? ParentCategoryId { get; set; }
}

public class AdminEditCategoryDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconClass { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int? ParentCategoryId { get; set; }
}

public class AdminEquipmentListItemDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public decimal DailyRentalRate { get; set; }
    public decimal SecurityDeposit { get; set; }
    public int TotalQuantity { get; set; }
    public EquipmentCondition Condition { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? PrimaryImageUrl { get; set; }
}

public class AdminBookingListItemDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public int GroupsCount { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AdminBookingItemDto
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysCount { get; set; }
    public decimal DailyRentalRate { get; set; }
    public decimal TotalItemPrice { get; set; }
    public int? ReviewId { get; set; }
    public int? ReviewRating { get; set; }
    public bool? ReviewIsHidden { get; set; }
}

public class AdminBookingGroupDetailDto
{
    public int Id { get; set; }
    public string GroupNumber { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public BookingGroupStatus Status { get; set; }
    public decimal SubtotalRentalFee { get; set; }
    public decimal SubtotalSecurityDeposit { get; set; }
    public string PickupVerificationCode { get; set; } = string.Empty;
    public string? HandoverNotes { get; set; }
    public DateTime? HandedOverAtUtc { get; set; }
    public string? ReturnInspectionNotes { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? RejectionReason { get; set; }
    public List<AdminBookingItemDto> Items { get; set; } = new();
}

public class AdminBookingDetailDto
{
    public int Id { get; set; }
    public string BookingNumber { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public BookingStatus Status { get; set; }
    public decimal TotalRentalFee { get; set; }
    public decimal TotalSecurityDeposit { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<AdminBookingGroupDetailDto> Groups { get; set; } = new();
}

public class AdminDamageReportDto
{
    public int MaintenanceRecordId { get; set; }
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int BlockedQuantity { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public decimal? EstimatedCost { get; set; }
    public bool IsResolved { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}

public class MonthlyBookingTrendDto
{
    public string MonthLabel { get; set; } = string.Empty;
    public int BookingsCount { get; set; }
    public decimal TotalRentalFeeVolume { get; set; }
}

public class TopCategoryMetricDto
{
    public string CategoryName { get; set; } = string.Empty;
    public int TotalRentalItemsCount { get; set; }
    public decimal RentalFeeVolume { get; set; }
}

public class TopEquipmentMetricDto
{
    public int EquipmentId { get; set; }
    public string EquipmentTitle { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public int TotalBookingsCount { get; set; }
    public decimal RentalRevenue { get; set; }
}

public class TopStoreMetricDto
{
    public int BusinessId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public int CompletedGroupsCount { get; set; }
    public decimal TotalRentalRevenue { get; set; }
}

public class AdminPlatformAnalyticsDto
{
    public int TotalCompletedRentals { get; set; }
    public int TotalDamageIncidents { get; set; }
    public double DamageIncidentRatePercent { get; set; }
    public List<MonthlyBookingTrendDto> MonthlyTrends { get; set; } = new();
    public List<TopCategoryMetricDto> TopCategories { get; set; } = new();
    public List<TopEquipmentMetricDto> TopEquipment { get; set; } = new();
    public List<TopStoreMetricDto> TopStores { get; set; } = new();
}

public class AdminAuditLogDto
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string? Details { get; set; }
    public DateTime TimestampUtc { get; set; }
}

public interface IAdminPlatformService
{
    Task<AdminDashboardMetricsDto> GetDashboardMetricsAsync();
    Task<(List<AdminCustomerListItemDto> Items, int TotalCount)> GetCustomersAsync(string? search = null, int page = 1, int pageSize = 20);
    Task<AdminCustomerDetailDto?> GetCustomerDetailsAsync(string customerId);
    Task<bool> ToggleCustomerLockoutAsync(string customerId, string adminUserId, string? reason = null, string? ipAddress = null);
    
    Task<List<AdminCategoryDto>> GetCategoriesAsync();
    Task<Category?> GetCategoryByIdAsync(int id);
    Task<(bool Success, string? ErrorMessage, int? CategoryId)> CreateCategoryAsync(AdminCreateCategoryDto dto, string adminUserId, string? ipAddress = null);
    Task<(bool Success, string? ErrorMessage)> UpdateCategoryAsync(int id, AdminEditCategoryDto dto, string adminUserId, string? ipAddress = null);
    Task<(bool Success, string? ErrorMessage)> DeleteCategoryAsync(int id, string adminUserId, string? ipAddress = null);

    Task<(List<AdminEquipmentListItemDto> Items, int TotalCount)> GetEquipmentListingsAsync(string? search = null, int? categoryId = null, bool? isActiveFilter = null, int page = 1, int pageSize = 20);
    Task<(bool Success, string? ErrorMessage)> ToggleEquipmentStatusAsync(int equipmentId, bool isActive, string? reason, string adminUserId, string? ipAddress = null);

    Task<(List<AdminBookingListItemDto> Items, int TotalCount)> GetBookingsAsync(BookingStatus? statusFilter = null, string? search = null, int page = 1, int pageSize = 20);
    Task<AdminBookingDetailDto?> GetBookingDetailsAsync(int bookingId);

    Task<List<AdminDamageReportDto>> GetDamageReportsAsync(bool? isResolvedFilter = null);
    Task<bool> ResolveDamageReportAsync(int maintenanceRecordId, string adminUserId, string? resolutionNotes = null, string? ipAddress = null);

    Task<AdminPlatformAnalyticsDto> GetPlatformAnalyticsAsync();
    Task<(List<AdminAuditLogDto> Items, int TotalCount)> GetAuditLogsAsync(string? actionFilter = null, string? entityTypeFilter = null, int page = 1, int pageSize = 30);
}
