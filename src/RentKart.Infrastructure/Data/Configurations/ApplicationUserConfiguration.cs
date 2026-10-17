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

        builder.HasIndex(u => u.FullName);
    }
}
