using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Core.Interfaces;

public interface IBusinessManagementService
{
    Task<BusinessProfile?> GetBusinessByUserIdAsync(string userId);
    Task<BusinessProfile?> GetBusinessByIdAsync(int businessId);
    Task<bool> CanBusinessOperateAsync(int businessId);
    Task<bool> ApproveBusinessAsync(int businessId, string adminUserId, string? ipAddress = null);
    Task<bool> RejectBusinessAsync(int businessId, string adminUserId, string? reason = null, string? ipAddress = null);
    Task<bool> SuspendBusinessAsync(int businessId, string adminUserId, string? reason = null, string? ipAddress = null);
    Task<bool> ActivateBusinessAsync(int businessId, string adminUserId, string? ipAddress = null);
}
