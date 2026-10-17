using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.EntityId)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(a => a.TimestampUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(a => a.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(a => a.TimestampUtc);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => new { a.EntityType, a.EntityId });
    }
}
