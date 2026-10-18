using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;

namespace RentKart.Infrastructure.Services;

public class BusinessManagementService : IBusinessManagementService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BusinessManagementService> _logger;

    public BusinessManagementService(ApplicationDbContext context, ILogger<BusinessManagementService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<BusinessProfile?> GetBusinessByUserIdAsync(string userId)
    {
        return await _context.BusinessProfiles
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.UserId == userId);
    }

    public async Task<BusinessProfile?> GetBusinessByIdAsync(int businessId)
    {
        return await _context.BusinessProfiles
            .Include(b => b.User)
            .FirstOrDefaultAsync(b => b.Id == businessId);
    }

    public async Task<bool> CanBusinessOperateAsync(int businessId)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        return business != null && business.Status == BusinessStatus.Approved;
    }

    public async Task<bool> ApproveBusinessAsync(int businessId, string adminUserId, string? ipAddress = null)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null) return false;

        var oldStatus = business.Status;
        business.Status = BusinessStatus.Approved;
        business.ApprovedAtUtc = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "ApproveBusiness",
            EntityType = nameof(BusinessProfile),
            EntityId = business.Id.ToString(),
            Details = $"{{\"OldStatus\": \"{oldStatus}\", \"NewStatus\": \"{business.Status}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business {BusinessId} approved by Admin {AdminUserId}.", businessId, adminUserId);
        return true;
    }

    public async Task<bool> RejectBusinessAsync(int businessId, string adminUserId, string? reason = null, string? ipAddress = null)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null) return false;

        var oldStatus = business.Status;
        business.Status = BusinessStatus.Rejected;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "RejectBusiness",
            EntityType = nameof(BusinessProfile),
            EntityId = business.Id.ToString(),
            Details = $"{{\"OldStatus\": \"{oldStatus}\", \"NewStatus\": \"{business.Status}\", \"Reason\": \"{reason ?? "None"}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business {BusinessId} rejected by Admin {AdminUserId}.", businessId, adminUserId);
        return true;
    }

    public async Task<bool> SuspendBusinessAsync(int businessId, string adminUserId, string? reason = null, string? ipAddress = null)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null) return false;

        var oldStatus = business.Status;
        business.Status = BusinessStatus.Suspended;
        business.SuspendedAtUtc = DateTime.UtcNow;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "SuspendBusiness",
            EntityType = nameof(BusinessProfile),
            EntityId = business.Id.ToString(),
            Details = $"{{\"OldStatus\": \"{oldStatus}\", \"NewStatus\": \"{business.Status}\", \"Reason\": \"{reason ?? "None"}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business {BusinessId} suspended by Admin {AdminUserId}.", businessId, adminUserId);
        return true;
    }

    public async Task<bool> ActivateBusinessAsync(int businessId, string adminUserId, string? ipAddress = null)
    {
        var business = await _context.BusinessProfiles.FindAsync(businessId);
        if (business == null) return false;

        var oldStatus = business.Status;
        business.Status = BusinessStatus.Approved;
        business.ApprovedAtUtc = DateTime.UtcNow;
        business.SuspendedAtUtc = null;

        _context.AuditLogs.Add(new AuditLog
        {
            UserId = adminUserId,
            Action = "ActivateBusiness",
            EntityType = nameof(BusinessProfile),
            EntityId = business.Id.ToString(),
            Details = $"{{\"OldStatus\": \"{oldStatus}\", \"NewStatus\": \"{business.Status}\", \"Ip\": \"{ipAddress ?? "unknown"}\"}}",
            TimestampUtc = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business {BusinessId} reactivated by Admin {AdminUserId}.", businessId, adminUserId);
        return true;
    }
}
