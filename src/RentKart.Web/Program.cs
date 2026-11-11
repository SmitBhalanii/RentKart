using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RentKart.Core.Entities;
using RentKart.Core.Interfaces;
using RentKart.Infrastructure.Data;
using RentKart.Infrastructure.Identity;
using RentKart.Infrastructure.Services;
using RentKart.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, b => b.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = ".RentKart.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
});

builder.Services.AddHttpContextAccessor();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(4);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".RentKart.Session";
});

builder.Services.AddScoped<IBusinessManagementService, BusinessManagementService>();
builder.Services.AddScoped<IEquipmentCatalogService, EquipmentCatalogService>();
builder.Services.AddScoped<IWishlistService, WishlistService>();
builder.Services.AddScoped<IBusinessEquipmentService, BusinessEquipmentService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddScoped<ICartService, CartService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRentalDocumentService, RentalDocumentService>();
builder.Services.AddScoped<IReviewService, ReviewService>();
builder.Services.AddScoped<IAdminPlatformService, AdminPlatformService>();
builder.Services.AddScoped<IImageUploadService, ImageUploadService>();

builder.Services.AddControllersWithViews();

var app = builder.Build();

// Migrate and seed database
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var dbContext = services.GetRequiredService<ApplicationDbContext>();
        if (dbContext.Database.IsSqlServer())
        {
            logger.LogInformation("Applying any pending database migrations...");
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Database migrations verified and up to date.");
        }

        await IdentityDataSeeder.SeedRolesAndAdminAsync(services);
        await CatalogDataSeeder.SeedCatalogDataAsync(services);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred while initializing database migrations, Identity roles, or catalog data. Check SQL Server connection string.");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

// Required for WebApplicationFactory in integration tests
public partial class Program { }
