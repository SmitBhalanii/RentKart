using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);

        builder.Property(n => n.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(n => n.Message)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(n => n.TargetUrl)
            .HasMaxLength(500);

        builder.Property(n => n.EventKey)
            .HasMaxLength(150);

        builder.Property(n => n.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Booking)
            .WithMany(b => b.Notifications)
            .HasForeignKey(n => n.BookingId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(n => n.BookingGroup)
            .WithMany(bg => bg.Notifications)
            .HasForeignKey(n => n.BookingGroupId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasIndex(n => n.UserId);
        builder.HasIndex(n => new { n.UserId, n.IsRead });
        builder.HasIndex(n => n.CreatedAtUtc);
        builder.HasIndex(n => new { n.UserId, n.EventKey });
    }
}
