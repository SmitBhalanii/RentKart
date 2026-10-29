using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class RentalDocumentConfiguration : IEntityTypeConfiguration<RentalDocument>
{
    public void Configure(EntityTypeBuilder<RentalDocument> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DocumentNumber)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(d => d.FileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(d => d.ContentType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.Content)
            .IsRequired();

        builder.Property(d => d.RentalSubtotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.SecurityDeposit)
            .HasPrecision(18, 2);

        builder.Property(d => d.GrandTotal)
            .HasPrecision(18, 2);

        builder.Property(d => d.FailureReason)
            .HasMaxLength(500);

        builder.Property(d => d.GeneratedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(d => d.CustomerUser)
            .WithMany(u => u.RentalDocuments)
            .HasForeignKey(d => d.CustomerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.Booking)
            .WithMany(b => b.RentalDocuments)
            .HasForeignKey(d => d.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.BookingGroup)
            .WithMany(bg => bg.RentalDocuments)
            .HasForeignKey(d => d.BookingGroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(d => d.Business)
            .WithMany()
            .HasForeignKey(d => d.BusinessId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(d => d.DocumentNumber).IsUnique();
        builder.HasIndex(d => d.BookingId);
        builder.HasIndex(d => d.BookingGroupId);
        builder.HasIndex(d => d.BusinessId);
        builder.HasIndex(d => d.CustomerUserId);
        builder.HasIndex(d => d.GeneratedAtUtc);
    }
}
