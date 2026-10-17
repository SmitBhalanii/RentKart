using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class BookingItemConfiguration : IEntityTypeConfiguration<BookingItem>
{
    public void Configure(EntityTypeBuilder<BookingItem> builder)
    {
        builder.HasKey(bi => bi.Id);

        builder.Property(bi => bi.Quantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(bi => bi.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(bi => bi.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(bi => bi.DaysCount)
            .IsRequired();

        builder.Property(bi => bi.DailyRentalRate)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(bi => bi.ItemSecurityDeposit)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(bi => bi.TotalItemPrice)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        // Foreign keys
        builder.HasOne(bi => bi.BookingGroup)
            .WithMany(bg => bg.BookingItems)
            .HasForeignKey(bi => bi.BookingGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bi => bi.Equipment)
            .WithMany(e => e.BookingItems)
            .HasForeignKey(bi => bi.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Filtered composite index for lightning-fast date range reservation checks
        builder.HasIndex(bi => new { bi.EquipmentId, bi.StartDate, bi.EndDate });
        builder.HasIndex(bi => bi.BookingGroupId);
    }
}
