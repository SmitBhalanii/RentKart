using System.ComponentModel.DataAnnotations;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;

namespace RentKart.Web.Models.Admin;

public class AdminDashboardViewModel
{
    public AdminDashboardMetricsDto Metrics { get; set; } = new();
    public List<BusinessListItemViewModel> PendingBusinesses { get; set; } = new();
    public List<BusinessListItemViewModel> RecentBusinesses { get; set; } = new();

    // Legacy compatibility helpers
    public int TotalUsers => Metrics.TotalUsersCount;
    public int TotalCustomers => Metrics.TotalCustomersCount;
    public int TotalBusinesses => Metrics.TotalBusinessesCount;
    public int PendingBusinessesCount => Metrics.PendingBusinessesCount;
    public int ApprovedBusinessesCount => Metrics.ApprovedBusinessesCount;
    public int SuspendedBusinessesCount => Metrics.SuspendedBusinessesCount;
}

public class BusinessManagementViewModel
{
    public BusinessStatus? StatusFilter { get; set; }
    public List<BusinessListItemViewModel> Businesses { get; set; } = new();
}

public class BusinessListItemViewModel
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string OwnerName { get; set; } = string.Empty;
    public string OwnerEmail { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string AddressLine { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? Landmark { get; set; }
    public string? District { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "IN";
    public string FullAddress { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public BusinessStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
}

public class AdminCustomersViewModel
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<AdminCustomerListItemDto> Customers { get; set; } = new();
}

public class AdminCategoriesViewModel
{
    public List<AdminCategoryDto> Categories { get; set; } = new();
    public AdminCreateCategoryDto NewCategory { get; set; } = new();
}

public class AdminEquipmentViewModel
{
    public string? Search { get; set; }
    public int? CategoryId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<AdminEquipmentListItemDto> Equipment { get; set; } = new();
    public List<AdminCategoryDto> Categories { get; set; } = new();
}

public class AdminBookingsViewModel
{
    public BookingStatus? StatusFilter { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<AdminBookingListItemDto> Bookings { get; set; } = new();
}

public class AdminReviewsViewModel
{
    public bool? IsHiddenFilter { get; set; }
    public int? RatingFilter { get; set; }
    public List<AdminReviewListItemDto> Reviews { get; set; } = new();
}

public class AdminDamageReportsViewModel
{
    public bool? IsResolvedFilter { get; set; }
    public List<AdminDamageReportDto> DamageReports { get; set; } = new();
}

public class AdminAuditLogsViewModel
{
    public string? ActionFilter { get; set; }
    public string? EntityTypeFilter { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 30;
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
    public List<AdminAuditLogDto> Logs { get; set; } = new();
}
