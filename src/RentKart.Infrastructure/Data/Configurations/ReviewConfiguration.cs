using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Rating)
            .IsRequired();

        builder.Property(r => r.Comment)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(r => r.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // 1-to-1 relationship with BookingItem
        builder.HasOne(r => r.BookingItem)
            .WithOne(bi => bi.Review)
            .HasForeignKey<Review>(r => r.BookingItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Customer)
            .WithMany(u => u.Reviews)
            .HasForeignKey(r => r.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Equipment)
            .WithMany(e => e.Reviews)
            .HasForeignKey(r => r.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Crucial: Unique index on BookingItemId prevents duplicate reviews
        builder.HasIndex(r => r.BookingItemId)
            .IsUnique();

        builder.HasIndex(r => r.EquipmentId);
        builder.HasIndex(r => r.CustomerId);
        builder.HasIndex(r => r.Rating);
    }
}
