using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Business;

namespace RentKart.Web.Controllers;

[Authorize(Roles = $"{IdentityDataSeeder.RoleBusiness},{IdentityDataSeeder.RoleAdmin}")]
public class BusinessController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBusinessManagementService _businessService;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<BusinessController> _logger;

    public BusinessController(
        UserManager<ApplicationUser> userManager,
        IBusinessManagementService businessService,
        ApplicationDbContext context,
        ILogger<BusinessController> logger)
    {
        _userManager = userManager;
        _businessService = businessService;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            TempData["ErrorMessage"] = "No registered business profile was found for your account.";
            return RedirectToAction("RegisterBusiness", "Account");
        }

        var model = new BusinessDashboardViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            OwnerName = user.FullName,
            Email = user.Email ?? string.Empty,
            ContactPhone = business.ContactPhone,
            Address = $"{business.AddressLine}, {business.City}, {business.State} {business.PostalCode}",
            OperatingHours = business.OperatingHours,
            Status = business.Status,
            CreatedAtUtc = business.CreatedAtUtc,
            ApprovedAtUtc = business.ApprovedAtUtc,
            SuspendedAtUtc = business.SuspendedAtUtc
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var model = new BusinessProfileViewModel
        {
            BusinessId = business.Id,
            ShopName = business.ShopName,
            RegistrationNumber = business.RegistrationNumber,
            ContactPhone = business.ContactPhone,
            AddressLine = business.AddressLine,
            City = business.City,
            State = business.State,
            PostalCode = business.PostalCode,
            OperatingHours = business.OperatingHours,
            PickupInstructions = business.PickupInstructions,
            Status = business.Status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(BusinessProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var business = await _businessService.GetBusinessByUserIdAsync(user.Id);
        if (business == null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        // Server-side ownership security check
        if (business.UserId != user.Id && !User.IsInRole(IdentityDataSeeder.RoleAdmin))
        {
            _logger.LogWarning("Unauthorized attempt to update business profile {BusinessId} by user {UserId}.", model.BusinessId, user.Id);
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.Status = business.Status;
            return View(model);
        }

        // Update permitted business profile fields (business owner cannot change Status!)
        business.ShopName = model.ShopName;
        business.RegistrationNumber = model.RegistrationNumber;
        business.ContactPhone = model.ContactPhone;
        business.AddressLine = model.AddressLine;
        business.City = model.City;
        business.State = model.State;
        business.PostalCode = model.PostalCode;
        business.OperatingHours = model.OperatingHours;
        business.PickupInstructions = model.PickupInstructions;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Business profile {BusinessId} updated by user {UserId}.", business.Id, user.Id);

        TempData["SuccessMessage"] = "Your business profile has been updated successfully.";
        return RedirectToAction(nameof(Profile));
    }
}
