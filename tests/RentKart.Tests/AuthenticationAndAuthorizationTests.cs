using System.Net;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RentKart.Core.Entities;
using RentKart.Core.Enums;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using Xunit;

namespace RentKart.Tests;

public class AuthenticationAndAuthorizationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ConnectionString = "Server=localhost\\SQLEXPRESS;Database=RentKartDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
    private readonly WebApplicationFactory<Program> _factory;

    public AuthenticationAndAuthorizationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task IdentitySeeder_SeedsRolesAndDefaultAdmin_Successfully()
    {
        using var scope = _factory.Services.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Verify Authoritative Roles
        Assert.True(await roleManager.RoleExistsAsync(IdentityDataSeeder.RoleAdmin));
        Assert.True(await roleManager.RoleExistsAsync(IdentityDataSeeder.RoleBusiness));
        Assert.True(await roleManager.RoleExistsAsync(IdentityDataSeeder.RoleCustomer));

        // Verify Default Admin Provisioning
        var admin = await userManager.FindByEmailAsync("admin@rentkart.local");
        Assert.NotNull(admin);
        Assert.Equal(UserRole.Admin, admin.UserType);
        Assert.True(admin.IsActive);
        Assert.True(await userManager.IsInRoleAsync(admin, IdentityDataSeeder.RoleAdmin));
        Assert.True(await userManager.CheckPasswordAsync(admin, "Admin@RentKart2026!"));
    }

    [Fact]
    public async Task CustomerRegistration_CreatesUser_WithCustomerRole_AndNoElevatedPrivileges()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"customer_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"Test Customer {testSuffix}",
            PhoneNumber = "+1-555-0100",
            UserType = UserRole.Customer,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, "CustPass@2026!");
        Assert.True(result.Succeeded);

        await userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleCustomer);

        // Verify role membership
        var roles = await userManager.GetRolesAsync(user);
        Assert.Single(roles);
        Assert.Contains(IdentityDataSeeder.RoleCustomer, roles);
        Assert.DoesNotContain(IdentityDataSeeder.RoleAdmin, roles);
        Assert.DoesNotContain(IdentityDataSeeder.RoleBusiness, roles);

        // Verify password check
        Assert.True(await userManager.CheckPasswordAsync(user, "CustPass@2026!"));
        Assert.False(await userManager.CheckPasswordAsync(user, "WrongPassword@123"));

        // Cleanup
        await userManager.DeleteAsync(user);
    }

    [Fact]
    public async Task BusinessRegistration_InitializesInPendingApprovalStatus_AndCannotOperate()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var businessService = scope.ServiceProvider.GetRequiredService<IBusinessManagementService>();
        using var context = CreateDbContext();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"business_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"Test Business Owner {testSuffix}",
            UserType = UserRole.Business,
            IsActive = true
        };

        var createResult = await userManager.CreateAsync(user, "BizPass@2026!");
        Assert.True(createResult.Succeeded);
        await userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleBusiness);

        var businessProfile = new BusinessProfile
        {
            UserId = user.Id,
            ShopName = $"Metro Tool Rental {testSuffix}",
            ContactPhone = "+1-555-0198",
            AddressLine = "789 Industrial Pkwy",
            City = "San Jose",
            State = "CA",
            PostalCode = "95110",
            OperatingHours = "Mon-Sat 08:00 - 18:00",
            Status = BusinessStatus.PendingApproval
        };

        context.BusinessProfiles.Add(businessProfile);
        await context.SaveChangesAsync();

        // Verify pending approval status
        Assert.Equal(BusinessStatus.PendingApproval, businessProfile.Status);

        // Crucial business rule: Pending businesses cannot operate/publish listings
        var canOperate = await businessService.CanBusinessOperateAsync(businessProfile.Id);
        Assert.False(canOperate, "Pending business must not be allowed to operate or publish listings.");

        // Cleanup
        context.BusinessProfiles.Remove(businessProfile);
        await context.SaveChangesAsync();
        await userManager.DeleteAsync(user);
    }

    [Fact]
    public async Task BusinessApprovalLifecycle_TransitionsStatus_AndRecordsAuditLog()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var businessService = scope.ServiceProvider.GetRequiredService<IBusinessManagementService>();
        using var context = CreateDbContext();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"lifecycle_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = $"Lifecycle Owner {testSuffix}",
            UserType = UserRole.Business,
            IsActive = true
        };

        await userManager.CreateAsync(user, "BizPass@2026!");
        await userManager.AddToRoleAsync(user, IdentityDataSeeder.RoleBusiness);

        var business = new BusinessProfile
        {
            UserId = user.Id,
            ShopName = $"Lifecycle Store {testSuffix}",
            ContactPhone = "+1-555-0155",
            AddressLine = "100 Main St",
            City = "San Jose",
            State = "CA",
            PostalCode = "95112",
            OperatingHours = "Mon-Fri 09:00 - 17:00",
            Status = BusinessStatus.PendingApproval
        };

        context.BusinessProfiles.Add(business);
        await context.SaveChangesAsync();

        var admin = await userManager.FindByEmailAsync("admin@rentkart.local");
        Assert.NotNull(admin);

        // 1. Approve Business
        var approved = await businessService.ApproveBusinessAsync(business.Id, admin.Id, "127.0.0.1");
        Assert.True(approved);

        var approvedBusiness = await businessService.GetBusinessByIdAsync(business.Id);
        Assert.NotNull(approvedBusiness);
        Assert.Equal(BusinessStatus.Approved, approvedBusiness.Status);
        Assert.NotNull(approvedBusiness.ApprovedAtUtc);
        Assert.True(await businessService.CanBusinessOperateAsync(business.Id));

        // 2. Suspend Business
        var suspended = await businessService.SuspendBusinessAsync(business.Id, admin.Id, "Policy audit", "127.0.0.1");
        Assert.True(suspended);

        var suspendedBusiness = await businessService.GetBusinessByIdAsync(business.Id);
        Assert.NotNull(suspendedBusiness);
        Assert.Equal(BusinessStatus.Suspended, suspendedBusiness.Status);
        Assert.NotNull(suspendedBusiness.SuspendedAtUtc);
        Assert.False(await businessService.CanBusinessOperateAsync(business.Id));

        // 3. Reactivate Business
        var reactivated = await businessService.ActivateBusinessAsync(business.Id, admin.Id, "127.0.0.1");
        Assert.True(reactivated);

        var reactivatedBusiness = await businessService.GetBusinessByIdAsync(business.Id);
        Assert.NotNull(reactivatedBusiness);
        Assert.Equal(BusinessStatus.Approved, reactivatedBusiness.Status);
        Assert.True(await businessService.CanBusinessOperateAsync(business.Id));

        // 4. Verify AuditLog entries exist for the lifecycle
        var auditLogs = await context.AuditLogs
            .Where(a => a.EntityType == nameof(BusinessProfile) && a.EntityId == business.Id.ToString())
            .ToListAsync();
        Assert.Equal(3, auditLogs.Count); // Approve, Suspend, Activate

        // Cleanup
        context.AuditLogs.RemoveRange(auditLogs);
        context.BusinessProfiles.Remove(business);
        await context.SaveChangesAsync();
        await userManager.DeleteAsync(user);
    }

    [Fact]
    public async Task DuplicateEmail_IsRejectedByUserManager()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"dup_{testSuffix}@test.rentkart.local";

        var user1 = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "First User",
            UserType = UserRole.Customer
        };
        var res1 = await userManager.CreateAsync(user1, "PassOne@2026!");
        Assert.True(res1.Succeeded);

        var user2 = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Second User",
            UserType = UserRole.Customer
        };
        var res2 = await userManager.CreateAsync(user2, "PassTwo@2026!");
        Assert.False(res2.Succeeded);
        Assert.Contains(res2.Errors, e => e.Code.Contains("Duplicate"));

        // Cleanup
        await userManager.DeleteAsync(user1);
    }

    [Fact]
    public async Task DirectAccess_ToProtectedRoutes_RedirectsToLogin_ForUnauthenticatedUser()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // Test protected admin endpoint
        var adminResponse = await client.GetAsync("/Admin/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, adminResponse.StatusCode);
        Assert.Contains("/Account/Login", adminResponse.Headers.Location?.ToString());

        // Test protected business endpoint
        var businessResponse = await client.GetAsync("/Business/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, businessResponse.StatusCode);
        Assert.Contains("/Account/Login", businessResponse.Headers.Location?.ToString());

        // Test protected customer endpoint
        var customerResponse = await client.GetAsync("/Customer/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, customerResponse.StatusCode);
        Assert.Contains("/Account/Login", customerResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task PostActions_RequireAntiForgeryToken_OrReturnBadRequest()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        // 1. Public POST action with [ValidateAntiForgeryToken] without token returns 400 Bad Request
        var formContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            { "Email", "test@rentkart.local" },
            { "Password", "SomePassword123!" }
        });
        var loginPostResponse = await client.PostAsync("/Account/Login", formContent);
        Assert.Equal(HttpStatusCode.BadRequest, loginPostResponse.StatusCode);

        // 2. Protected POST action requires authentication (302 to Login)
        var logoutResponse = await client.PostAsync("/Account/Logout", null);
        Assert.Equal(HttpStatusCode.Redirect, logoutResponse.StatusCode);
        Assert.Contains("/Account/Login", logoutResponse.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Login_WithIncorrectPassword_FailsAuthentication()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"login_fail_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Login Fail Test User",
            UserType = UserRole.Customer,
            IsActive = true
        };

        await userManager.CreateAsync(user, "CorrectPass@2026!");

        var validResult = await signInManager.CheckPasswordSignInAsync(user, "CorrectPass@2026!", lockoutOnFailure: false);
        Assert.True(validResult.Succeeded);

        var badResult = await signInManager.CheckPasswordSignInAsync(user, "WrongPass@999!", lockoutOnFailure: false);
        Assert.False(badResult.Succeeded);

        // Cleanup
        await userManager.DeleteAsync(user);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"deactivated_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Deactivated User",
            UserType = UserRole.Customer,
            IsActive = false // Deactivated
        };

        await userManager.CreateAsync(user, "Pass@2026!");

        // Assert user's active flag
        var retrievedUser = await userManager.FindByEmailAsync(email);
        Assert.NotNull(retrievedUser);
        Assert.False(retrievedUser.IsActive);

        // Cleanup
        await userManager.DeleteAsync(user);
    }

    [Fact]
    public async Task CustomerProfile_UpdatesContactDetails_Successfully()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var testSuffix = Guid.NewGuid().ToString("N")[..8];
        var email = $"profile_update_{testSuffix}@test.rentkart.local";

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Original Name",
            PhoneNumber = "+1-555-0001",
            UserType = UserRole.Customer,
            IsActive = true
        };

        await userManager.CreateAsync(user, "Pass@2026!");

        // Update profile
        user.FullName = "Updated Full Name";
        user.PhoneNumber = "+1-555-9999";
        user.UpdatedAtUtc = DateTime.UtcNow;

        var updateResult = await userManager.UpdateAsync(user);
        Assert.True(updateResult.Succeeded);

        var updated = await userManager.FindByEmailAsync(email);
        Assert.NotNull(updated);
        Assert.Equal("Updated Full Name", updated.FullName);
        Assert.Equal("+1-555-9999", updated.PhoneNumber);
        Assert.NotNull(updated.UpdatedAtUtc);

        // Cleanup
        await userManager.DeleteAsync(user);
    }

    [Theory]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/RegisterCustomer")]
    [InlineData("/Account/RegisterBusiness")]
    [InlineData("/Account/AccessDenied")]
    public async Task PublicAuthPages_RenderSuccessfully(string url)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("RentKart", content);
    }
}
