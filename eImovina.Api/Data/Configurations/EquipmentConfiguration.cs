using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("Equipment");
        builder.Property(e => e.InventoryNumber).HasMaxLength(50).IsRequired();
        builder.Property(e => e.SerialNumber).HasMaxLength(100);
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Currency).HasMaxLength(3);
        builder.Property(e => e.Notes).HasMaxLength(1000);
        builder.Property(e => e.PurchaseValue).HasPrecision(18, 2);
        builder.Property(e => e.IsArchived).HasDefaultValue(false);
        builder.Property(e => e.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

        // Plain unique index - NOT filtered. SQLite/ANSI SQL already treat every NULL as
        // distinct under UNIQUE, so "unique if provided" falls out for free.
        builder.HasIndex(e => e.InventoryNumber).IsUnique().HasDatabaseName("UX_Equipment_InventoryNumber");
        builder.HasIndex(e => e.SerialNumber).IsUnique().HasDatabaseName("UX_Equipment_SerialNumber");
        builder.HasIndex(e => e.EquipmentCategoryId);
        builder.HasIndex(e => e.EquipmentStatusId);
        builder.HasIndex(e => e.CurrentLocationId);

        builder.HasOne<EquipmentCategory>()
            .WithMany()
            .HasForeignKey(e => e.EquipmentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EquipmentStatus>()
            .WithMany()
            .HasForeignKey(e => e.EquipmentStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(e => e.CurrentLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
