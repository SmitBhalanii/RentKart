using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> builder)
    {
        builder.ToTable("WishlistItems");

        builder.HasKey(w => w.Id);

        builder.Property(w => w.AddedAtUtc)
            .IsRequired();

        // Unique index: A user cannot add the same equipment multiple times
        builder.HasIndex(w => new { w.UserId, w.EquipmentId })
            .IsUnique();

        builder.HasOne(w => w.User)
            .WithMany(u => u.WishlistItems)
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Equipment)
            .WithMany(e => e.WishlistItems)
            .HasForeignKey(w => w.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
