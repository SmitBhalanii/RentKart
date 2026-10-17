using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class MaintenanceRecordConfiguration : IEntityTypeConfiguration<MaintenanceRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceRecord> builder)
    {
        builder.HasKey(m => m.Id);

        builder.Property(m => m.BlockedQuantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(m => m.StartDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(m => m.EndDate)
            .HasColumnType("date")
            .IsRequired();

        builder.Property(m => m.Reason)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(m => m.EstimatedCost)
            .HasColumnType("decimal(18,2)");

        builder.Property(m => m.IsResolved)
            .HasDefaultValue(false);

        builder.Property(m => m.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasOne(m => m.Equipment)
            .WithMany(e => e.MaintenanceRecords)
            .HasForeignKey(m => m.EquipmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.EquipmentId, m.StartDate, m.EndDate });
        builder.HasIndex(m => m.IsResolved);
    }
}
