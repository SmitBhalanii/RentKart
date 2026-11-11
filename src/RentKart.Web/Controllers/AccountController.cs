using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Validation;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Models.Account;

namespace RentKart.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext context,
        ILogger<AccountController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
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

        var normalizedEmail = model.Email.Trim();
        var user = await _userManager.FindByEmailAsync(normalizedEmail);
        if (user == null)
        {
            _logger.LogWarning("Failed login attempt: non-existent email '{Email}'.", normalizedEmail);
            ModelState.AddModelError(string.Empty, "Invalid email address or password.");
            return View(model);
        }

        if (!user.IsActive)
        {
            _logger.LogWarning("Login rejected for deactivated user {UserId} ({Email}).", user.Id, user.Email);
            ModelState.AddModelError(string.Empty, "This account is inactive. Please contact RentKart support.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            model.Password,
            model.RememberMe,
            lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {UserId} ({Email}) logged in successfully.", user.Id, user.Email);

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }

            return await RedirectUserBasedOnRoleAsync(user);
        }

        if (result.IsLockedOut)
        {
            _logger.LogWarning("User account locked out for {UserId} ({Email}).", user.Id, user.Email);
            ModelState.AddModelError(string.Empty, "This account has been temporarily locked due to multiple failed login attempts. Please try again later.");
            return View(model);
        }

        _logger.LogWarning("Failed login attempt: invalid password for user {UserId} ({Email}).", user.Id, user.Email);
        ModelState.AddModelError(string.Empty, "Invalid email address or password.");
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

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // 1. Validate email uniqueness
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists. Please sign in or use another email.");
            return View(model);
        }

        // 2. Ensure Customer role exists in database before proceeding
        if (!await _roleManager.RoleExistsAsync(IdentityDataSeeder.RoleCustomer))
        {
            _logger.LogWarning("Customer role was missing from database. Auto-creating role.");
            var roleCreateResult = await _roleManager.CreateAsync(new IdentityRole(IdentityDataSeeder.RoleCustomer));
            if (!roleCreateResult.Succeeded)
            {
                _logger.LogError("Critical: Failed to auto-provision Customer role: {Errors}",
                    string.Join("; ", roleCreateResult.Errors.Select(e => e.Description)));
                ModelState.AddModelError(string.Empty, "Registration could not be completed due to a configuration issue. Please contact support.");
                return View(model);
            }
        }

        // 3. Create Identity user
        var normalizedPhone = IndianPhoneNumberAttribute.Normalize(model.PhoneNumber);
        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            PhoneNumber = normalizedPhone,
            UserType = UserRole.Customer,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Customer user creation failed for {Email}: {Errors}",
                normalizedEmail, string.Join("; ", result.Errors.Select(e => e.Description)));

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // 4. Assign Customer role and explicitly verify result
        var roleAssignResult = await _userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleCustomer);
        if (!roleAssignResult.Succeeded)
        {
            _logger.LogError("Role assignment failed for user {UserId} ({Email}): {Errors}. Initiating compensating cleanup.",
                user.Id, normalizedEmail, string.Join("; ", roleAssignResult.Errors.Select(e => e.Description)));

            // Compensating rollback: delete created user to prevent orphaned role-less accounts
            await _userManager.DeleteAsync(user);

            ModelState.AddModelError(string.Empty, "Account role assignment failed. Please try registering again.");
            return View(model);
        }

        _logger.LogInformation("New customer account registered successfully: {UserId} ({Email}).", user.Id, normalizedEmail);

        // 5. Sign in customer only after account creation and role assignment both succeed
        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["SuccessMessage"] = $"Welcome to RentKart, {user.FullName}! Your customer account is ready.";

        // 6. Redirect to Customer Dashboard
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

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        // 1. Validate email uniqueness
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address already exists. Please sign in or use another email.");
            return View(model);
        }

        // 2. Ensure Business role exists in database
        if (!await _roleManager.RoleExistsAsync(IdentityDataSeeder.RoleBusiness))
        {
            _logger.LogWarning("Business role was missing from database. Auto-creating role.");
            var roleCreateResult = await _roleManager.CreateAsync(new IdentityRole(IdentityDataSeeder.RoleBusiness));
            if (!roleCreateResult.Succeeded)
            {
                _logger.LogError("Critical: Failed to auto-provision Business role: {Errors}",
                    string.Join("; ", roleCreateResult.Errors.Select(e => e.Description)));
                ModelState.AddModelError(string.Empty, "Registration could not be completed due to a configuration issue. Please contact support.");
                return View(model);
            }
        }

        // 3. Create Identity user
        var normalizedPhone = IndianPhoneNumberAttribute.Normalize(model.ContactPhone);
        var addressLine1 = !string.IsNullOrWhiteSpace(model.AddressLine1) ? model.AddressLine1.Trim() : model.AddressLine.Trim();

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            PhoneNumber = normalizedPhone,
            UserType = UserRole.Business,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAtUtc = DateTime.UtcNow,
            AddressLine1 = addressLine1,
            AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim(),
            Landmark = string.IsNullOrWhiteSpace(model.Landmark) ? null : model.Landmark.Trim(),
            City = model.City.Trim(),
            District = string.IsNullOrWhiteSpace(model.District) ? null : model.District.Trim(),
            State = model.State.Trim(),
            PostalCode = model.PostalCode.Trim(),
            Country = string.IsNullOrWhiteSpace(model.Country) ? "IN" : model.Country.Trim()
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Business owner user creation failed for {Email}: {Errors}",
                normalizedEmail, string.Join("; ", result.Errors.Select(e => e.Description)));

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // 4. Assign Business role and check result
        var roleAssignResult = await _userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleBusiness);
        if (!roleAssignResult.Succeeded)
        {
            _logger.LogError("Role assignment failed for user {UserId} ({Email}): {Errors}. Initiating compensating cleanup.",
                user.Id, normalizedEmail, string.Join("; ", roleAssignResult.Errors.Select(e => e.Description)));

            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Account role assignment failed. Please try registering again.");
            return View(model);
        }

        // 5. Create Business Profile with India-wide address
        try
        {
            var businessProfile = new BusinessProfile
            {
                UserId = user.Id,
                ShopName = model.ShopName.Trim(),
                RegistrationNumber = string.IsNullOrWhiteSpace(model.RegistrationNumber) ? null : model.RegistrationNumber.Trim(),
                ContactPhone = normalizedPhone,
                AddressLine = addressLine1,
                AddressLine2 = string.IsNullOrWhiteSpace(model.AddressLine2) ? null : model.AddressLine2.Trim(),
                Landmark = string.IsNullOrWhiteSpace(model.Landmark) ? null : model.Landmark.Trim(),
                City = model.City.Trim(),
                District = string.IsNullOrWhiteSpace(model.District) ? null : model.District.Trim(),
                State = model.State.Trim(),
                PostalCode = model.PostalCode.Trim(),
                Country = string.IsNullOrWhiteSpace(model.Country) ? "IN" : model.Country.Trim(),
                OperatingHours = model.OperatingHours.Trim(),
                PickupInstructions = string.IsNullOrWhiteSpace(model.PickupInstructions) ? null : model.PickupInstructions.Trim(),
                Status = BusinessStatus.PendingApproval, // Pending approval by default
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.BusinessProfiles.Add(businessProfile);
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create business profile for user {UserId} ({Email}). Rolling back user creation.", user.Id, normalizedEmail);
            await _userManager.DeleteAsync(user);
            ModelState.AddModelError(string.Empty, "Failed to save business profile details. Please try registering again.");
            return View(model);
        }

        _logger.LogInformation("New business registered: {UserId}, Shop: {ShopName}, Status: PendingApproval.", user.Id, model.ShopName);

        // 6. Sign in and redirect to Business Dashboard
        await _signInManager.SignInAsync(user, isPersistent: false);
        TempData["SuccessMessage"] = "Your business profile was successfully registered! An administrator will review your account.";
        TempData["InfoMessage"] = "Your business profile is currently pending administrator verification.";
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
