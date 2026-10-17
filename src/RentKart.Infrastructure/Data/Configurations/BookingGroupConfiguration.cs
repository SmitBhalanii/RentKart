using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class BookingGroupConfiguration : IEntityTypeConfiguration<BookingGroup>
{
    public void Configure(EntityTypeBuilder<BookingGroup> builder)
    {
        builder.HasKey(bg => bg.Id);

        builder.Property(bg => bg.GroupNumber)
            .IsRequired()
            .HasMaxLength(35);

        builder.Property(bg => bg.PickupVerificationCode)
            .IsRequired()
            .HasMaxLength(12);

        builder.Property(bg => bg.SubtotalRentalFee)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(bg => bg.SubtotalSecurityDeposit)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(bg => bg.Status)
            .IsRequired();

        builder.Property(bg => bg.HandoverNotes)
            .HasMaxLength(1000);

        builder.Property(bg => bg.ReturnInspectionNotes)
            .HasMaxLength(1000);

        builder.Property(bg => bg.RejectionReason)
            .HasMaxLength(500);

        builder.Property(bg => bg.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // Cascade delete child groups when parent booking is deleted
        builder.HasOne(bg => bg.ParentBooking)
            .WithMany(b => b.BookingGroups)
            .HasForeignKey(bg => bg.ParentBookingId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict delete on business profile to prevent orphaning group orders
        builder.HasOne(bg => bg.Business)
            .WithMany(b => b.BookingGroups)
            .HasForeignKey(bg => bg.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(bg => bg.GroupNumber)
            .IsUnique();

        builder.HasIndex(bg => bg.ParentBookingId);
        builder.HasIndex(bg => bg.BusinessId);
        builder.HasIndex(bg => bg.Status);
        builder.HasIndex(bg => bg.PickupVerificationCode);
    }
}
