using System.Security.Claims;
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
    private readonly IAdminPlatformService _adminPlatformService;
    private readonly IReviewService _reviewService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        ApplicationDbContext context,
        IBusinessManagementService businessService,
        IAdminPlatformService adminPlatformService,
        IReviewService reviewService,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger)
    {
        _context = context;
        _businessService = businessService;
        _adminPlatformService = adminPlatformService;
        _reviewService = reviewService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var metrics = await _adminPlatformService.GetDashboardMetricsAsync();

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
            Metrics = metrics,
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

    // Customer Management
    [HttpGet]
    public async Task<IActionResult> Customers(string? search, int page = 1)
    {
        if (page < 1) page = 1;
        var (customers, totalCount) = await _adminPlatformService.GetCustomersAsync(search, page, pageSize: 20);

        var model = new AdminCustomersViewModel
        {
            Search = search,
            Page = page,
            TotalCount = totalCount,
            Customers = customers
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> CustomerDetails(string id)
    {
        var customer = await _adminPlatformService.GetCustomerDetailsAsync(id);
        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleCustomerLockout(string id, string? reason, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _adminPlatformService.ToggleCustomerLockoutAsync(id, adminId, reason, ip);
        if (success)
        {
            TempData["SuccessMessage"] = "Customer account access status was updated successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not update customer status.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Customers));
    }

    // Category Management
    [HttpGet]
    public async Task<IActionResult> Categories()
    {
        var list = await _adminPlatformService.GetCategoriesAsync();
        var model = new AdminCategoriesViewModel
        {
            Categories = list
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateCategory(AdminCreateCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Slug))
        {
            TempData["ErrorMessage"] = "Category name and slug are required.";
            return RedirectToAction(nameof(Categories));
        }

        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, error, _) = await _adminPlatformService.CreateCategoryAsync(dto, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = $"Category '{dto.Name}' was created successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = error ?? "Failed to create category.";
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCategory(int id, AdminEditCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Slug))
        {
            TempData["ErrorMessage"] = "Category name and slug are required.";
            return RedirectToAction(nameof(Categories));
        }

        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, error) = await _adminPlatformService.UpdateCategoryAsync(id, dto, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = $"Category '{dto.Name}' was updated successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = error ?? "Failed to update category.";
        }

        return RedirectToAction(nameof(Categories));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, error) = await _adminPlatformService.DeleteCategoryAsync(id, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = "Category was deleted successfully.";
        }
        else
        {
            TempData["ErrorMessage"] = error ?? "Failed to delete category.";
        }

        return RedirectToAction(nameof(Categories));
    }

    // Equipment Moderation
    [HttpGet]
    public async Task<IActionResult> Equipment(string? search, int? categoryId, bool? isActive, int page = 1)
    {
        if (page < 1) page = 1;
        var (items, totalCount) = await _adminPlatformService.GetEquipmentListingsAsync(search, categoryId, isActive, page, pageSize: 20);
        var categories = await _adminPlatformService.GetCategoriesAsync();

        var model = new AdminEquipmentViewModel
        {
            Search = search,
            CategoryId = categoryId,
            IsActive = isActive,
            Page = page,
            TotalCount = totalCount,
            Equipment = items,
            Categories = categories
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEquipmentStatus(int id, bool isActive, string? reason, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var (success, error) = await _adminPlatformService.ToggleEquipmentStatusAsync(id, isActive, reason, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = isActive 
                ? "Equipment listing has been activated and is now visible." 
                : "Equipment listing has been suspended from the public catalog.";
        }
        else
        {
            TempData["ErrorMessage"] = error ?? "Failed to update equipment status.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Equipment));
    }

    // Booking Monitoring
    [HttpGet]
    public async Task<IActionResult> Bookings(BookingStatus? status, string? search, int page = 1)
    {
        if (page < 1) page = 1;
        var (items, totalCount) = await _adminPlatformService.GetBookingsAsync(status, search, page, pageSize: 20);

        var model = new AdminBookingsViewModel
        {
            StatusFilter = status,
            Search = search,
            Page = page,
            TotalCount = totalCount,
            Bookings = items
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> BookingDetails(int id)
    {
        var booking = await _adminPlatformService.GetBookingDetailsAsync(id);
        if (booking == null)
        {
            return NotFound();
        }

        return View(booking);
    }

    // Review Moderation
    [HttpGet]
    public async Task<IActionResult> Reviews(bool? isHidden, int? rating)
    {
        var list = await _reviewService.GetReviewsForAdminAsync(isHidden, rating);
        var model = new AdminReviewsViewModel
        {
            IsHiddenFilter = isHidden,
            RatingFilter = rating,
            Reviews = list
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleReviewVisibility(int id, bool isHidden, string? reason, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _reviewService.ModerateReviewAsync(id, isHidden, reason, adminId, ip);
        if (success)
        {
            TempData["SuccessMessage"] = isHidden 
                ? "Review has been hidden from public catalog display." 
                : "Review has been unhidden and restored to public catalog display.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not update review moderation status.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Reviews));
    }

    // Damage Reports Moderation
    [HttpGet]
    public async Task<IActionResult> DamageReports(bool? isResolved)
    {
        var list = await _adminPlatformService.GetDamageReportsAsync(isResolved);
        var model = new AdminDamageReportsViewModel
        {
            IsResolvedFilter = isResolved,
            DamageReports = list
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveDamageReport(int id, string? resolutionNotes, string? returnUrl = null)
    {
        var adminId = _userManager.GetUserId(User) ?? "Admin";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();

        var success = await _adminPlatformService.ResolveDamageReportAsync(id, adminId, resolutionNotes, ip);
        if (success)
        {
            TempData["SuccessMessage"] = "Damage incident has been marked as resolved.";
        }
        else
        {
            TempData["ErrorMessage"] = "Could not resolve damage report.";
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(DamageReports));
    }

    // Platform Analytics
    [HttpGet]
    public async Task<IActionResult> Analytics()
    {
        var analytics = await _adminPlatformService.GetPlatformAnalyticsAsync();
        return View(analytics);
    }

    // Audit Logs
    [HttpGet]
    public async Task<IActionResult> AuditLogs(string? actionFilter, string? entityTypeFilter, int page = 1)
    {
        if (page < 1) page = 1;
        var (logs, totalCount) = await _adminPlatformService.GetAuditLogsAsync(actionFilter, entityTypeFilter, page, pageSize: 30);

        var model = new AdminAuditLogsViewModel
        {
            ActionFilter = actionFilter,
            EntityTypeFilter = entityTypeFilter,
            Page = page,
            TotalCount = totalCount,
            Logs = logs
        };

        return View(model);
    }
}
