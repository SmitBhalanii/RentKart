using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.HasKey(b => b.Id);

        builder.Property(b => b.BookingNumber)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(b => b.CustomerId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(b => b.TotalRentalFee)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(b => b.TotalSecurityDeposit)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(b => b.GrandTotal)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired();

        builder.Property(b => b.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(b => b.Customer)
            .WithMany(u => u.Bookings)
            .HasForeignKey(b => b.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(b => b.BookingNumber)
            .IsUnique();

        builder.HasIndex(b => b.CustomerId);
        builder.HasIndex(b => b.Status);
        builder.HasIndex(b => b.CreatedAtUtc);
    }
}
