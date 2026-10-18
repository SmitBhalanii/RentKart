using RentKart.Core.Enums;

namespace RentKart.Web.Models.Admin;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalCustomers { get; set; }
    public int TotalBusinesses { get; set; }
    public int PendingBusinessesCount { get; set; }
    public int ApprovedBusinessesCount { get; set; }
    public int SuspendedBusinessesCount { get; set; }

    public List<BusinessListItemViewModel> PendingBusinesses { get; set; } = new();
    public List<BusinessListItemViewModel> RecentBusinesses { get; set; } = new();
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
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string OperatingHours { get; set; } = string.Empty;
    public BusinessStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public DateTime? SuspendedAtUtc { get; set; }
}
