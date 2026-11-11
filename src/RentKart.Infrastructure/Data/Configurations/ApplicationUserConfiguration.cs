using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FullName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.UserType)
            .IsRequired();

        builder.Property(u => u.IsActive)
            .HasDefaultValue(true);

        builder.Property(u => u.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // India-wide customer address properties
        builder.Property(u => u.AddressLine1).HasMaxLength(200);
        builder.Property(u => u.AddressLine2).HasMaxLength(200);
        builder.Property(u => u.Landmark).HasMaxLength(150);
        builder.Property(u => u.City).HasMaxLength(100);
        builder.Property(u => u.District).HasMaxLength(100);
        builder.Property(u => u.State).HasMaxLength(100);
        builder.Property(u => u.PostalCode).HasMaxLength(20);
        builder.Property(u => u.Country).HasMaxLength(10).HasDefaultValue("IN");

        builder.Ignore(u => u.FullFormattedAddress);

        builder.HasIndex(u => u.FullName);
    }
}
