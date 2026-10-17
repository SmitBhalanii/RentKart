using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class EquipmentImageConfiguration : IEntityTypeConfiguration<EquipmentImage>
{
    public void Configure(EntityTypeBuilder<EquipmentImage> builder)
    {
        builder.HasKey(i => i.Id);

        builder.Property(i => i.ImageUrl)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.IsPrimary)
            .HasDefaultValue(false);

        builder.Property(i => i.DisplayOrder)
            .HasDefaultValue(0);

        builder.Property(i => i.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // Cascade delete image records when parent equipment is deleted
        builder.HasOne(i => i.Equipment)
            .WithMany(e => e.Images)
            .HasForeignKey(i => i.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(i => i.EquipmentId);
        builder.HasIndex(i => new { i.EquipmentId, i.IsPrimary });
    }
}
