using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RentKart.Core.Entities;
using RentKart.Core.Enums;

namespace RentKart.Infrastructure.Identity;

public static class IdentityDataSeeder
{
    public const string RoleAdmin = "Admin";
    public const string RoleBusiness = "Business";
    public const string RoleCustomer = "Customer";

    public static async Task SeedRolesAndAdminAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("IdentityDataSeeder");

        // 1. Seed authoritative roles
        string[] roles = { RoleAdmin, RoleBusiness, RoleCustomer };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole(role));
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Role '{Role}' created successfully.", role);
                }
                else
                {
                    logger.LogError("Failed to create role '{Role}': {Errors}", role, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
        }

        // 2. Ensure initial Admin account exists
        var adminEmail = configuration["AdminUser:Email"] ?? "admin@rentkart.local";
        var adminPassword = configuration["AdminUser:Password"] ?? "Admin@RentKart2026!";

        var existingAdmin = await userManager.FindByEmailAsync(adminEmail);
        if (existingAdmin == null)
        {
            var adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                FullName = "RentKart Administrator",
                UserType = UserRole.Admin,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(adminUser, adminPassword);
            if (createResult.Succeeded)
            {
                var roleResult = await userManager.AddToRoleAsync(adminUser, RoleAdmin);
                if (roleResult.Succeeded)
                {
                    logger.LogInformation("Admin user '{Email}' provisioned successfully with Admin role.", adminEmail);
                }
                else
                {
                    logger.LogError("Failed to assign Admin role to '{Email}': {Errors}", adminEmail, string.Join(", ", roleResult.Errors.Select(e => e.Description)));
                }
            }
            else
            {
                logger.LogError("Failed to provision Admin user '{Email}': {Errors}", adminEmail, string.Join(", ", createResult.Errors.Select(e => e.Description)));
            }
        }
        else
        {
            // Ensure the existing admin has the Admin role
            if (!await userManager.IsInRoleAsync(existingAdmin, RoleAdmin))
            {
                await userManager.AddToRoleAsync(existingAdmin, RoleAdmin);
                logger.LogInformation("Assigned Admin role to existing user '{Email}'.", adminEmail);
            }
        }
    }
}
