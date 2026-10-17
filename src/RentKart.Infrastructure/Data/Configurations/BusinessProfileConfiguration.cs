using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class BusinessProfileConfiguration : IEntityTypeConfiguration<BusinessProfile>
{
    public void Configure(EntityTypeBuilder<BusinessProfile> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.UserId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(b => b.ShopName)
            .IsRequired()
            .HasMaxLength(150);

        builder.Property(b => b.RegistrationNumber)
            .HasMaxLength(100);

        builder.Property(b => b.ContactPhone)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(b => b.AddressLine)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.City)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.State)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(b => b.PostalCode)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(b => b.OperatingHours)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(b => b.PickupInstructions)
            .HasMaxLength(1000);

        builder.Property(b => b.Status)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // 1-to-1 relationship with ApplicationUser
        builder.HasOne(b => b.User)
            .WithOne(u => u.BusinessProfile)
            .HasForeignKey<BusinessProfile>(b => b.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(b => b.UserId)
            .IsUnique();

        builder.HasIndex(b => b.City);
        builder.HasIndex(b => b.Status);
    }
}
