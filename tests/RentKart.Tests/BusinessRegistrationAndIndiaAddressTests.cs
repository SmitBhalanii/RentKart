using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Core.Utilities;
using RentKart.Core.Validation;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Web.Controllers;
using RentKart.Web.Models.Account;
using RentKart.Web.Models.Business;
using RentKart.Web.Models.Shared;
using Xunit;

namespace RentKart.Tests;

public class BusinessRegistrationAndIndiaAddressTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public BusinessRegistrationAndIndiaAddressTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object?> LoadTempData(HttpContext? context) => new Dictionary<string, object?>();
        public void SaveTempData(HttpContext? context, IDictionary<string, object?>? values) { }
    }

    #region 1. India-Wide Locations & Address Validation

    [Fact]
    public void IndianLocations_ContainsAll36StatesAndUnionTerritories()
    {
        var states = IndianLocations.StatesAndUnionTerritories;

        Assert.Equal(36, states.Count);
        Assert.Contains("Maharashtra", states);
        Assert.Contains("Karnataka", states);
        Assert.Contains("Gujarat", states);
        Assert.Contains("Delhi (NCT)", states);
        Assert.Contains("Tamil Nadu", states);
        Assert.Contains("West Bengal", states);
        Assert.Contains("Uttar Pradesh", states);
        Assert.Contains("Rajasthan", states);
        Assert.Contains("Kerala", states);
        Assert.Contains("Jammu and Kashmir", states);
        Assert.Contains("Ladakh", states);
        Assert.Contains("Puducherry", states);
    }

    [Theory]
    [InlineData("400053", true)]
    [InlineData("560034", true)]
    [InlineData("110001", true)]
    [InlineData("380054", true)]
    [InlineData("600001", true)]
    [InlineData("700001", true)]
    [InlineData("012345", false)] // Leading 0 invalid in Indian postal system
    [InlineData("40005", false)]  // 5 digits
    [InlineData("4000531", false)] // 7 digits
    [InlineData("400 053", true)] // Space normalized
    [InlineData("ABCDEF", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IndianLocations_PinCodeValidation_AccuratelyValidates(string? pinCode, bool expectedValid)
    {
        var isValid = IndianLocations.IsValidPinCode(pinCode);
        Assert.Equal(expectedValid, isValid);
    }

    [Fact]
    public void IndianLocations_FormatFullAddress_FormatsStandardIndianAddress()
    {
        var address = IndianLocations.FormatFullAddress(
            addressLine1: "Shop 12, Ground Floor",
            addressLine2: "Titanium City Centre",
            landmark: "Opp. Sachin Tower",
            city: "Ahmedabad",
            district: "Ahmedabad",
            state: "Gujarat",
            postalCode: "380054",
            country: "IN");

        Assert.Contains("Shop 12, Ground Floor", address);
        Assert.Contains("Titanium City Centre", address);
        Assert.Contains("Opp. Sachin Tower", address);
        Assert.Contains("Ahmedabad", address);
        Assert.Contains("Gujarat - 380054", address);
        Assert.Contains("India", address);
    }

    [Fact]
    public void IndianAddressViewModel_ValidatesCorrectly()
    {
        var model = new IndianAddressViewModel
        {
            AddressLine1 = "128, 80 Feet Road",
            AddressLine2 = "Koramangala 4th Block",
            Landmark = "Near Sony World Junction",
            City = "Bengaluru",
            District = "Bengaluru Urban",
            State = "Karnataka",
            PostalCode = "560034",
            Country = "IN"
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        Assert.True(isValid);
        Assert.Empty(results);

        var formatted = model.ToFullFormattedAddress();
        Assert.Contains("Bengaluru", formatted);
        Assert.Contains("Karnataka - 560034", formatted);
    }

    [Fact]
    public void IndianAddressViewModel_InvalidPinCode_FailsValidation()
    {
        var model = new IndianAddressViewModel
        {
            AddressLine1 = "Plot 10, Industrial Area",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "012345" // Invalid
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.PostalCode)));
    }

    #endregion

    #region 2. Business Registration End-to-End

    [Fact]
    public async Task RegisterBusiness_ValidSubmission_CreatesUserRoleAndPendingProfile()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var controller = new AccountController(
            userManager,
            signInManager,
            roleManager,
            db,
            NullLogger<AccountController>.Instance);

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContextAccessor.HttpContext = httpContext;

        var tempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.TempData = tempData;
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"biz.test.{suffix}@rentkart.local";

        var model = new RegisterBusinessViewModel
        {
            FullName = "Ramesh Patel",
            Email = email,
            Password = "SecurePass@2026!",
            ConfirmPassword = "SecurePass@2026!",
            ShopName = $"Patel Tools Hire {suffix}",
            RegistrationNumber = "24AABCP1234Q1Z1",
            ContactPhone = "98250 11223",
            AddressLine1 = "Shop 101, Titanium Square",
            AddressLine2 = "Thaltej Cross Roads, SG Highway",
            Landmark = "Opp. Acropolis Mall",
            City = "Ahmedabad",
            District = "Ahmedabad",
            State = "Gujarat",
            PostalCode = "380054",
            OperatingHours = "Mon-Sat: 09:00 - 19:00",
            PickupInstructions = "Counter check-in with digital code."
        };

        var actionResult = await controller.RegisterBusiness(model);

        var redirect = Assert.IsType<RedirectToActionResult>(actionResult);
        Assert.Equal("Dashboard", redirect.ActionName);
        Assert.Equal("Business", redirect.ControllerName);

        // Verify User was created
        var user = await userManager.FindByEmailAsync(email);
        Assert.NotNull(user);
        Assert.Equal("Ramesh Patel", user.FullName);
        Assert.Equal(UserRole.Business, user.UserType);
        Assert.True(await userManager.IsInRoleAsync(user, IdentityDataSeeder.RoleBusiness));

        // Verify Business Profile was created in PendingApproval status
        var profile = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user.Id);
        Assert.NotNull(profile);
        Assert.Equal($"Patel Tools Hire {suffix}", profile.ShopName);
        Assert.Equal(BusinessStatus.PendingApproval, profile.Status);
        Assert.Null(profile.ApprovedAtUtc);
        Assert.Equal("Shop 101, Titanium Square", profile.AddressLine);
        Assert.Equal("Thaltej Cross Roads, SG Highway", profile.AddressLine2);
        Assert.Equal("Opp. Acropolis Mall", profile.Landmark);
        Assert.Equal("Ahmedabad", profile.City);
        Assert.Equal("Gujarat", profile.State);
        Assert.Equal("380054", profile.PostalCode);
        Assert.Equal("IN", profile.Country);

        // Verify formatted full address
        Assert.Contains("Shop 101, Titanium Square", profile.FullFormattedAddress);
        Assert.Contains("Gujarat - 380054", profile.FullFormattedAddress);
    }

    [Fact]
    public async Task RegisterBusiness_DuplicateEmail_FailsWithValidationError()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var controller = new AccountController(
            userManager,
            signInManager,
            roleManager,
            db,
            NullLogger<AccountController>.Instance);

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContextAccessor.HttpContext = httpContext;

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
            RouteData = new RouteData(),
            ActionDescriptor = new ControllerActionDescriptor()
        };

        // Try to register with already existing Business 1 email
        var model = new RegisterBusinessViewModel
        {
            FullName = "Copycat Owner",
            Email = "store.sf@rentkart.local", // Existing seeded business email
            Password = "SecurePass@2026!",
            ConfirmPassword = "SecurePass@2026!",
            ShopName = "Duplicate Store",
            ContactPhone = "98201 99999",
            AddressLine1 = "Test Address",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400053"
        };

        var result = await controller.RegisterBusiness(model);

        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(model.Email)));
    }

    [Theory]
    [InlineData("12345")]        // Too short
    [InlineData("+1 555-1234")]  // US number
    [InlineData("abcdefghij")]   // Letters
    [InlineData("1234567890")]   // Invalid starting digit (must start with 6, 7, 8, or 9)
    public void BusinessRegistration_InvalidPhone_FailsValidation(string invalidPhone)
    {
        var model = new RegisterBusinessViewModel
        {
            FullName = "Store Owner",
            Email = "valid@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            ShopName = "Valid Shop",
            ContactPhone = invalidPhone,
            AddressLine1 = "Valid Address",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = "400053"
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.ContactPhone)));
    }

    [Theory]
    [InlineData("400")]          // Too short
    [InlineData("012345")]       // Starts with 0
    [InlineData("4000531")]      // Too long (7 digits)
    [InlineData("ABCDEF")]       // Letters
    public void BusinessRegistration_InvalidPinCode_FailsValidation(string invalidPin)
    {
        var model = new RegisterBusinessViewModel
        {
            FullName = "Store Owner",
            Email = "valid@example.com",
            Password = "Password@123",
            ConfirmPassword = "Password@123",
            ShopName = "Valid Shop",
            ContactPhone = "+91 98765 43210",
            AddressLine1 = "Valid Address",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = invalidPin
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(model.PostalCode)));
    }

    #endregion

    #region 3. Business Approval Workflow & Lifecycle

    [Fact]
    public async Task BusinessApproval_PendingBusiness_CannotPublishEquipment()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();

        // Ensure a pending business profile exists
        var pendingBiz = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.Status == BusinessStatus.PendingApproval);
        if (pendingBiz == null)
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync("store.pending@rentkart.local");
            if (user != null)
            {
                pendingBiz = await db.BusinessProfiles.FirstOrDefaultAsync(b => b.UserId == user.Id);
                if (pendingBiz != null)
                {
                    pendingBiz.Status = BusinessStatus.PendingApproval;
                    pendingBiz.ApprovedAtUtc = null;
                    await db.SaveChangesAsync();
                }
            }
        }

        Assert.NotNull(pendingBiz);
        Assert.Equal(BusinessStatus.PendingApproval, pendingBiz.Status);

        var category = await db.Categories.FirstAsync(c => c.IsActive);

        // Attempt to create equipment
        var result = await equipmentService.CreateEquipmentAsync(pendingBiz.Id, new CreateEquipmentDto
        {
            Title = "Unauthorized Pending Listing",
            Description = "Should be rejected because business is not approved",
            CategoryId = category.Id,
            DailyRentalRate = 1500m,
            SecurityDeposit = 3000m,
            TotalQuantity = 2
        });

        Assert.False(result.Succeeded);
        Assert.Contains("approved", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BusinessApproval_AdminApproveAndReject_TogglesOperationalStatus()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var businessService = scope.ServiceProvider.GetRequiredService<IBusinessManagementService>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var admin = await userManager.FindByEmailAsync("admin@rentkart.local");
        Assert.NotNull(admin);

        // Create a unique transient business for lifecycle testing
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var testUser = new ApplicationUser
        {
            UserName = $"biz.life.{suffix}@rentkart.local",
            Email = $"biz.life.{suffix}@rentkart.local",
            FullName = "Lifecycle Test Owner",
            PhoneNumber = "+91 98765 00001",
            UserType = UserRole.Business,
            IsActive = true
        };
        await userManager.CreateAsync(testUser, "TestPass@2026!");
        await userManager.AddToRoleAsync(testUser, IdentityDataSeeder.RoleBusiness);

        var testBiz = new BusinessProfile
        {
            UserId = testUser.Id,
            ShopName = $"Lifecycle Rental Hub {suffix}",
            ContactPhone = "+91 98765 00001",
            AddressLine = "Test Line 1",
            City = "Jaipur",
            State = "Rajasthan",
            PostalCode = "302001",
            OperatingHours = "Mon-Sat: 09:00 - 18:00",
            Status = BusinessStatus.PendingApproval
        };
        db.BusinessProfiles.Add(testBiz);
        await db.SaveChangesAsync();

        // 1. Verify starts as Pending
        Assert.False(await businessService.CanBusinessOperateAsync(testBiz.Id));

        // 2. Admin Approves Business
        var approved = await businessService.ApproveBusinessAsync(testBiz.Id, admin.Id);
        Assert.True(approved);

        var updated = await businessService.GetBusinessByIdAsync(testBiz.Id);
        Assert.NotNull(updated);
        Assert.Equal(BusinessStatus.Approved, updated.Status);
        Assert.NotNull(updated.ApprovedAtUtc);
        Assert.True(await businessService.CanBusinessOperateAsync(testBiz.Id));

        // 3. Admin Rejects Business
        var rejected = await businessService.RejectBusinessAsync(testBiz.Id, admin.Id, "Non-compliant documentation");
        Assert.True(rejected);

        var rejectedBiz = await businessService.GetBusinessByIdAsync(testBiz.Id);
        Assert.NotNull(rejectedBiz);
        Assert.Equal(BusinessStatus.Rejected, rejectedBiz.Status);
        Assert.False(await businessService.CanBusinessOperateAsync(testBiz.Id));

        // 4. Admin Reactivates Business
        var reactivated = await businessService.ActivateBusinessAsync(testBiz.Id, admin.Id);
        Assert.True(reactivated);

        var reactivatedBiz = await businessService.GetBusinessByIdAsync(testBiz.Id);
        Assert.NotNull(reactivatedBiz);
        Assert.Equal(BusinessStatus.Approved, reactivatedBiz.Status);
        Assert.True(await businessService.CanBusinessOperateAsync(testBiz.Id));

        // 5. Admin Suspends Business
        var suspended = await businessService.SuspendBusinessAsync(testBiz.Id, admin.Id, "Safety audit failure");
        Assert.True(suspended);

        var suspendedBiz = await businessService.GetBusinessByIdAsync(testBiz.Id);
        Assert.NotNull(suspendedBiz);
        Assert.Equal(BusinessStatus.Suspended, suspendedBiz.Status);
        Assert.False(await businessService.CanBusinessOperateAsync(testBiz.Id));
    }

    #endregion

    #region 4. Business Data Isolation & Equipment Deletion

    [Fact]
    public async Task BusinessDataIsolation_BusinessACannotMutateBusinessBEquipment()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();

        // Store 1 (Mumbai) and Store 2 (Bengaluru)
        var store1 = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var store2 = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved && b.Id != store1.Id);

        var store1Equipment = await db.Equipments.FirstAsync(e => e.BusinessId == store1.Id && e.IsActive);

        // Store 2 attempts to toggle status of Store 1's equipment
        var toggleResult = await equipmentService.ToggleEquipmentStatusAsync(store1Equipment.Id, store2.Id);
        Assert.False(toggleResult.Succeeded);
        Assert.Contains("unauthorized", toggleResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Store 2 attempts to update Store 1's equipment
        var updateResult = await equipmentService.UpdateEquipmentAsync(store1Equipment.Id, store2.Id, new UpdateEquipmentDto
        {
            Title = "Hijacked Listing Title",
            Description = "Unauthorized modification",
            CategoryId = store1Equipment.CategoryId,
            DailyRentalRate = 99m,
            SecurityDeposit = 99m,
            TotalQuantity = 10,
            Condition = store1Equipment.Condition,
            IsActive = store1Equipment.IsActive
        });
        Assert.False(updateResult.Succeeded);
        Assert.Contains("permission", updateResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        // Store 2 attempts to delete Store 1's equipment
        var deleteResult = await equipmentService.DeleteEquipmentAsync(store1Equipment.Id, store2.Id);
        Assert.False(deleteResult.Succeeded);
        Assert.Contains("unauthorized", deleteResult.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EquipmentManagement_DeleteEquipment_SucceedsWhenNoActiveBookings()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var equipmentService = scope.ServiceProvider.GetRequiredService<IBusinessEquipmentService>();

        var approvedBiz = await db.BusinessProfiles.FirstAsync(b => b.Status == BusinessStatus.Approved);
        var category = await db.Categories.FirstAsync(c => c.IsActive);

        // Create transient equipment listing
        var createResult = await equipmentService.CreateEquipmentAsync(approvedBiz.Id, new CreateEquipmentDto
        {
            Title = "Transient Rig for Deletion Test",
            Description = "Created specifically to test deletion and decommissioning",
            CategoryId = category.Id,
            DailyRentalRate = 800m,
            SecurityDeposit = 1500m,
            TotalQuantity = 1
        });

        Assert.True(createResult.Succeeded);
        Assert.NotNull(createResult.EquipmentId);

        var equipId = createResult.EquipmentId.Value;

        // Perform deletion
        var deleteResult = await equipmentService.DeleteEquipmentAsync(equipId, approvedBiz.Id);
        Assert.True(deleteResult.Succeeded);

        // Verify equipment is removed from database
        var lookup = await db.Equipments.FindAsync(equipId);
        Assert.Null(lookup);
    }

    #endregion
}

