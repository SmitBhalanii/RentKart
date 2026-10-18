using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Account;

namespace RentKart.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            _logger.LogWarning("Failed login attempt: non-existent email.");
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login rejected for deactivated user {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "This account is inactive. Please contact support.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} logged in successfully.", user.Id);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return await RedirectUserBasedOnRoleAsync(user);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for {UserId}.", user.Id);
            ModelState.AddModelError(string.Empty, "This account has been temporarily locked due to multiple failed attempts. Please try again later.");
            return View(model);
        }

        _logger.LogWarning("Failed login attempt: invalid credentials for user {UserId}.", user.Id);
        ModelState.AddModelError(string.Empty, "Invalid email or password.");
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out.");
        TempData["SuccessMessage"] = "You have been logged out successfully.";
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View();
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterCustomer()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View(new RegisterCustomerViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCustomer(RegisterCustomerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.PhoneNumber,
            UserType = UserRole.Customer,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleCustomer);
        _logger.LogInformation("New customer account registered: {UserId}.", user.Id);

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["SuccessMessage"] = $"Welcome to RentKart, {user.FullName}! Your customer account is ready.";
        return RedirectToAction("Dashboard", "Customer");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult RegisterBusiness()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToRoleDashboard();
        }

        return View(new RegisterBusinessViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterBusiness(RegisterBusinessViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists.");
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            PhoneNumber = model.ContactPhone,
            UserType = UserRole.Business,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        await _userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleBusiness);

        var businessProfile = new BusinessProfile
        {
            UserId = user.Id,
            ShopName = model.ShopName,
            RegistrationNumber = model.RegistrationNumber,
            ContactPhone = model.ContactPhone,
            AddressLine = model.AddressLine,
            City = model.City,
            State = model.State,
            PostalCode = model.PostalCode,
            OperatingHours = model.OperatingHours,
            PickupInstructions = model.PickupInstructions,
            Status = BusinessStatus.PendingApproval, // Pending approval by default
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.BusinessProfiles.Add(businessProfile);
        await _context.SaveChangesAsync();

        _logger.LogInformation("New business registered: {UserId}, Shop: {ShopName}, Status: PendingApproval.", user.Id, model.ShopName);

        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["InfoMessage"] = "Your business profile was created and is currently pending administrator verification.";
        return RedirectToAction("Dashboard", "Business");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private async Task<IActionResult> RedirectUserBasedOnRoleAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        if (roles.Contains(IdentityDataSeeder.RoleAdmin))
        {
            return RedirectToAction("Dashboard", "Admin");
        }

        if (roles.Contains(IdentityDataSeeder.RoleBusiness))
        {
            return RedirectToAction("Dashboard", "Business");
        }

        return RedirectToAction("Dashboard", "Customer");
    }

    private IActionResult RedirectToRoleDashboard()
    {
        if (User.IsInRole(IdentityDataSeeder.RoleAdmin))
        {
            return RedirectToAction("Dashboard", "Admin");
        }
        if (User.IsInRole(IdentityDataSeeder.RoleBusiness))
        {
            return RedirectToAction("Dashboard", "Business");
        }
        return RedirectToAction("Dashboard", "Customer");
    }
}
