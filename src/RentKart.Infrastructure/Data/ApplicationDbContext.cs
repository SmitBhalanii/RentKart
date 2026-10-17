using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole, string>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<BusinessProfile> BusinessProfiles => Set<BusinessProfile>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
    public DbSet<EquipmentImage> EquipmentImages => Set<EquipmentImage>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingGroup> BookingGroups => Set<BookingGroup>();
    public DbSet<BookingItem> BookingItems => Set<BookingItem>();
    public DbSet<MaintenanceRecord> MaintenanceRecords => Set<MaintenanceRecord>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Apply all entity configurations from this infrastructure assembly
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
