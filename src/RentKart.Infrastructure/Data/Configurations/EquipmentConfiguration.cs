using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RentKart.Core.Entities;

namespace RentKart.Infrastructure.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .IsRequired();

        builder.Property(e => e.DailyRentalRate)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(e => e.SecurityDeposit)
            .HasColumnType("decimal(18,2)")
            .IsRequired();

        builder.Property(e => e.TotalQuantity)
            .HasDefaultValue(1)
            .IsRequired();

        builder.Property(e => e.Condition)
            .IsRequired();

        builder.Property(e => e.Status)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .HasDefaultValue(true);

        builder.Property(e => e.CreatedAtUtc)
            .HasDefaultValueSql("GETUTCDATE()");

        // Concurrency token via rowversion
        builder.Property(e => e.RowVersion)
            .IsRowVersion();

        // Foreign keys with Restrict delete behavior
        builder.HasOne(e => e.Business)
            .WithMany(b => b.Equipments)
            .HasForeignKey(e => e.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Category)
            .WithMany(c => c.Equipments)
            .HasForeignKey(e => e.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(e => e.BusinessId);
        builder.HasIndex(e => e.CategoryId);
        builder.HasIndex(e => e.Status);
        builder.HasIndex(e => e.IsActive);
        builder.HasIndex(e => new { e.IsActive, e.Status });
    }
}
