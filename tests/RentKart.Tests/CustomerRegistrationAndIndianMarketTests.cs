using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
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
using RentKart.Web.Services;
using Xunit;

namespace RentKart.Tests;

public class CustomerRegistrationAndIndianMarketTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CustomerRegistrationAndIndianMarketTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    private class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = null!;
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "RentKart.Web";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    #region 1. Currency Formatting Tests

    [Theory]
    [InlineData(0, "₹0.00")]
    [InlineData(1250.50, "₹1,250.50")]
    [InlineData(100000, "₹1,00,000.00")]
    [InlineData(5000000, "₹50,00,000.00")]
    public void CurrencyHelper_FormatsInr_Accurately(decimal amount, string expectedSub)
    {
        var formatted = amount.ToInr();
        Assert.StartsWith("₹", formatted);
        var normalized = formatted.Replace("\u00A0", " ").Replace(" ", "");
        var expectedNormalized = expectedSub.Replace(" ", "");
        Assert.Equal(expectedNormalized, normalized);
    }

    [Fact]
    public void CurrencyHelper_NullableExtension_HandlesNullSafely()
    {
        decimal? nullAmount = null;
        Assert.Equal("—", nullAmount.ToInr(nullFallback: "—"));
        Assert.Equal("₹0.00", nullAmount.ToInr());

        decimal? validAmount = 2500m;
        Assert.StartsWith("₹", validAmount.ToInr());
    }

    #endregion

    #region 2. Indian Phone Number Validation Tests

    [Theory]
    [InlineData("9876543210", true)]
    [InlineData("+91 98765 43210", true)]
    [InlineData("+919876543210", true)]
    [InlineData("09876543210", true)]
    [InlineData("8123456789", true)]
    [InlineData("7000000000", true)]
    [InlineData("6999999999", true)]
    [InlineData("1234567890", false)] // Invalid starting digit 1
    [InlineData("5551234567", false)] // Invalid starting digit 5
    [InlineData("987654321", false)]  // 9 digits
    [InlineData("98765432101", false)] // 11 digits
    [InlineData("abcdefghij", false)] // Letters
    public void IndianPhoneNumberAttribute_Validates_Correctly(string phoneNumber, bool expectedValid)
    {
        var attribute = new IndianPhoneNumberAttribute();
        var validationContext = new ValidationContext(new object());
        var result = attribute.GetValidationResult(phoneNumber, validationContext);

        if (expectedValid)
        {
            Assert.Equal(ValidationResult.Success, result);
        }
        else
        {
            Assert.NotEqual(ValidationResult.Success, result);
        }
    }

    [Fact]
    public void IndianPhoneNumberAttribute_Normalizes_ToStandardE164Format()
    {
        var rawNumber = "9820112345";
        var normalized = IndianPhoneNumberAttribute.Normalize(rawNumber);
        Assert.Equal("+91 98201 12345", normalized);

        var prefixedNumber = "+91 98201 12345";
        var normalized2 = IndianPhoneNumberAttribute.Normalize(prefixedNumber);
        Assert.Equal("+91 98201 12345", normalized2);
    }

    #endregion

    #region 3. Indian Postal Code (PIN Code) Validation Tests

    [Theory]
    [InlineData("400053", true)] // Mumbai
    [InlineData("560034", true)] // Bengaluru
    [InlineData("110001", true)] // New Delhi
    [InlineData("380054", true)] // Ahmedabad
    [InlineData("012345", false)] // Cannot start with 0
    [InlineData("40005", false)]  // 5 digits
    [InlineData("4000531", false)] // 7 digits
    [InlineData("40005A", false)] // Alphanumeric
    public void RegisterBusinessViewModel_ValidatesIndianPostalCode(string postalCode, bool expectedValid)
    {
        var model = new RegisterBusinessViewModel
        {
            FullName = "Store Owner",
            ShopName = "Test Rental Shop",
            RegistrationNumber = "27AABCS1429B1Z1",
            Email = "shop@rentkart.local",
            ContactPhone = "9876543210",
            AddressLine = "Plot 101, Main Road",
            City = "Mumbai",
            State = "Maharashtra",
            PostalCode = postalCode,
            OperatingHours = "9 AM - 7 PM",
            Password = "StorePass@2026!",
            ConfirmPassword = "StorePass@2026!"
        };

        var validationResults = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, context, validationResults, true);

        Assert.Equal(expectedValid, isValid);
        if (!expectedValid)
        {
            Assert.Contains(validationResults, r => r.MemberNames.Contains(nameof(RegisterBusinessViewModel.PostalCode)));
        }
    }

    #endregion

    #region 4. Image Upload Service Tests

    [Fact]
    public async Task ImageUploadService_AllowsValidImageTypes_AndSavesFile()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "RentKartTests_" + Guid.NewGuid().ToString("N"));
        var testEnv = new TestWebHostEnvironment { WebRootPath = tempRoot };
        var logger = NullLogger<ImageUploadService>.Instance;

        var service = new ImageUploadService(testEnv, logger);

        try
        {
            var content = "fake-jpg-binary-bytes";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
            var result = await service.UploadEquipmentImageAsync(stream, "sample-dslr.jpg", "image/jpeg", stream.Length);

            Assert.True(result.Success);
            Assert.NotNull(result.ImageUrl);
            Assert.StartsWith("/uploads/equipment/eq_", result.ImageUrl);
            Assert.EndsWith(".jpg", result.ImageUrl);

            var savedFileName = Path.GetFileName(result.ImageUrl);
            var expectedPath = Path.Combine(tempRoot, "uploads", "equipment", savedFileName);
            Assert.True(File.Exists(expectedPath));

            var deleted = service.DeleteImage(result.ImageUrl);
            Assert.True(deleted);
            Assert.False(File.Exists(expectedPath));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, true);
            }
        }
    }

    [Theory]
    [InlineData("script.exe", "application/x-msdownload")]
    [InlineData("payload.sh", "text/x-shellscript")]
    [InlineData("page.php", "application/x-php")]
    [InlineData("document.pdf", "application/pdf")]
    public async Task ImageUploadService_RejectsDisallowedExtensions(string fileName, string contentType)
    {
        var testEnv = new TestWebHostEnvironment { WebRootPath = Path.GetTempPath() };
        var logger = NullLogger<ImageUploadService>.Instance;

        var service = new ImageUploadService(testEnv, logger);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("malicious content"));

        var result = await service.UploadEquipmentImageAsync(stream, fileName, contentType, stream.Length);

        Assert.False(result.Success);
        Assert.Contains("Only JPG, JPEG, PNG, and WebP", result.ErrorMessage);
    }

    [Fact]
    public async Task ImageUploadService_RejectsFilesExceeding5MB()
    {
        var testEnv = new TestWebHostEnvironment { WebRootPath = Path.GetTempPath() };
        var logger = NullLogger<ImageUploadService>.Instance;

        var service = new ImageUploadService(testEnv, logger);
        using var stream = new MemoryStream(new byte[10]);

        var excessiveSize = 5 * 1024 * 1024 + 1;
        var result = await service.UploadEquipmentImageAsync(stream, "giant_photo.jpg", "image/jpeg", excessiveSize);

        Assert.False(result.Success);
        Assert.Contains("5 MB", result.ErrorMessage);
    }

    #endregion

    #region 5. Customer Registration Flow Tests

    [Fact]
    public async Task RegisterCustomer_CreatesUser_AssignsCustomerRole_AndNormalizesPhone()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AccountController>>();

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContextAccessor.HttpContext = httpContext;

        var routeData = new Microsoft.AspNetCore.Routing.RouteData();
        var actionContext = new ActionContext(httpContext, routeData, new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());

        var controller = new AccountController(userManager, signInManager, roleManager, dbContext, logger)
        {
            ControllerContext = new ControllerContext(actionContext)
        };

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var testEmail = $"cust_in_{uniqueKey}@rentkart.local";

        var model = new RegisterCustomerViewModel
        {
            FullName = "Rahul Sharma",
            Email = testEmail,
            PhoneNumber = "9820112345",
            Password = "CustomerPass@2026!",
            ConfirmPassword = "CustomerPass@2026!"
        };

        try
        {
            var result = await controller.RegisterCustomer(model);

            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Dashboard", redirectResult.ActionName);
            Assert.Equal("Customer", redirectResult.ControllerName);

            var createdUser = await userManager.FindByEmailAsync(testEmail);
            Assert.NotNull(createdUser);
            Assert.Equal("Rahul Sharma", createdUser.FullName);
            Assert.Equal("+91 98201 12345", createdUser.PhoneNumber);
            Assert.Equal(UserRole.Customer, createdUser.UserType);

            Assert.True(await userManager.IsInRoleAsync(createdUser, IdentityDataSeeder.RoleCustomer));
        }
        finally
        {
            var createdUser = await userManager.FindByEmailAsync(testEmail);
            if (createdUser != null)
            {
                await userManager.DeleteAsync(createdUser);
            }
        }
    }

    [Fact]
    public async Task RegisterCustomer_RejectsDuplicateEmail_WithErrorMessage()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<ApplicationUser>>();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AccountController>>();

        var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        httpContextAccessor.HttpContext = httpContext;

        var routeData = new Microsoft.AspNetCore.Routing.RouteData();
        var actionContext = new ActionContext(httpContext, routeData, new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor());

        var controller = new AccountController(userManager, signInManager, roleManager, dbContext, logger)
        {
            ControllerContext = new ControllerContext(actionContext)
        };

        var uniqueKey = Guid.NewGuid().ToString("N")[..8];
        var testEmail = $"dup_cust_{uniqueKey}@rentkart.local";

        var model = new RegisterCustomerViewModel
        {
            FullName = "Pooja Patel",
            Email = testEmail,
            PhoneNumber = "9845067890",
            Password = "CustomerPass@2026!",
            ConfirmPassword = "CustomerPass@2026!"
        };

        try
        {
            var firstResult = await controller.RegisterCustomer(model);
            Assert.IsType<RedirectToActionResult>(firstResult);

            var duplicateController = new AccountController(userManager, signInManager, roleManager, dbContext, logger)
            {
                ControllerContext = new ControllerContext(actionContext)
            };
            var secondResult = await duplicateController.RegisterCustomer(model);

            var viewResult = Assert.IsType<ViewResult>(secondResult);
            Assert.False(duplicateController.ModelState.IsValid);
            Assert.True(duplicateController.ModelState.ContainsKey(nameof(RegisterCustomerViewModel.Email)));
            var error = duplicateController.ModelState[nameof(RegisterCustomerViewModel.Email)]!.Errors.First().ErrorMessage;
            Assert.Contains("already exists", error);
        }
        finally
        {
            var createdUser = await userManager.FindByEmailAsync(testEmail);
            if (createdUser != null)
            {
                await userManager.DeleteAsync(createdUser);
            }
        }
    }

    #endregion
}
