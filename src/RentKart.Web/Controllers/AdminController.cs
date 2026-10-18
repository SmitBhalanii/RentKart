using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Admin;

namespace RentKart.Web.Controllers;

[Authorize(Roles = IdentityDataSeeder.RoleAdmin)]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IBusinessManagementService _businessService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        IBusinessManagementService businessService,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger)
    {
        _context = context;
        _businessService = businessService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var totalUsers = await _context.Users.CountAsync();
        var totalCustomers = await _context.Users.CountAsync(u => u.UserType == UserRole.Customer);
        var totalBusinesses = await _context.BusinessProfiles.CountAsync();
        var pendingCount = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.PendingApproval);
        var approvedCount = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.Approved);
        var suspendedCount = await _context.BusinessProfiles.CountAsync(b => b.Status == BusinessStatus.Suspended);

        var pendingBusinesses = await _context.BusinessProfiles
            .Include(b => b.User)
            .Where(b => b.Status == BusinessStatus.PendingApproval)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BusinessListItemViewModel
            {
                Id = b.Id,
                UserId = b.UserId,
                OwnerName = b.User.FullName,
                OwnerEmail = b.User.Email ?? string.Empty,
                ShopName = b.ShopName,
                ContactPhone = b.ContactPhone,
                City = b.City,
                State = b.State,
                OperatingHours = b.OperatingHours,
                Status = b.Status,
                CreatedAtUtc = b.CreatedAtUtc
            })
            .ToListAsync();

        var recentBusinesses = await _context.BusinessProfiles
            .Include(b => b.User)
            .OrderByDescending(b => b.CreatedAtUtc)
            .Take(5)
            .Select(b => new BusinessListItemViewModel
            {
                Id = b.Id,
                UserId = b.UserId,
                OwnerName = b.User.FullName,
                OwnerEmail = b.User.Email ?? string.Empty,
                ShopName = b.ShopName,
                ContactPhone = b.ContactPhone,
                City = b.City,
                State = b.State,
                OperatingHours = b.OperatingHours,
                Status = b.Status,
                CreatedAtUtc = b.CreatedAtUtc
            })
            .ToListAsync();

        var model = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalCustomers = totalCustomers,
            TotalBusinesses = totalBusinesses,
            PendingBusinessesCount = pendingCount,
            ApprovedBusinessesCount = approvedCount,
            SuspendedBusinessesCount = suspendedCount,
            PendingBusinesses = pendingBusinesses,
            RecentBusinesses = recentBusinesses
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Businesses(BusinessStatus? status)
    {
        var query = _context.BusinessProfiles
            .Include(b => b.User)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(b => b.Status == status.Value);
        }

        var list = await query
            .OrderByDescending(b => b.CreatedAtUtc)
            .Select(b => new BusinessListItemViewModel
            {
                Id = b.Id,
                UserId = b.UserId,
                OwnerName = b.User.FullName,
                OwnerEmail = b.User.Email ?? string.Empty,
                ShopName = b.ShopName,
                ContactPhone = b.ContactPhone,
                City = b.City,
                State = b.State,
                OperatingHours = b.OperatingHours,
                Status = b.Status,
                CreatedAtUtc = b.CreatedAtUtc,
                ApprovedAtUtc = b.ApprovedAtUtc,
                SuspendedAtUtc = b.SuspendedAtUtc
            })
            .ToListAsync();

        var model = new BusinessManagementViewModel
        {
            StatusFilter = status,
            Businesses = list
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveBusiness(int id, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _businessService.ApproveBusinessAsync(id, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = $"Business ID #{id} has been approved successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Failed to approve business #{id}.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Businesses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectBusiness(int id, string? reason, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _businessService.RejectBusinessAsync(id, adminId, reason, ip);
        if (success)
        {
            TempData["InfoMessage"] = $"Business ID #{id} has been marked as Rejected.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Failed to reject business #{id}.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Businesses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendBusiness(int id, string? reason, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _businessService.SuspendBusinessAsync(id, adminId, reason, ip);
        if (success)
        {
            TempData["WarningMessage"] = $"Business ID #{id} has been suspended.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Failed to suspend business #{id}.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Businesses));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ActivateBusiness(int id, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _businessService.ActivateBusinessAsync(id, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = $"Business ID #{id} has been reactivated and approved.";
        }
        else
        {
            TempData["ErrorMessage"] = $"Failed to reactivate business #{id}.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Businesses));
    }
}
