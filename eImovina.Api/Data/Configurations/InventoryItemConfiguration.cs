using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems");
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.SnapshotInventoryNumber).HasMaxLength(50).IsRequired();
        builder.Property(x => x.SnapshotEquipmentName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SnapshotCategoryName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.SnapshotStatusName).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => x.InventoryId);
        builder.HasIndex(x => x.EquipmentId);
        builder.HasIndex(x => new { x.InventoryId, x.EquipmentId })
            .IsUnique()
            .HasDatabaseName("UX_InventoryItems_OnePerEquipment");

        // Fully-owned child collection, always created together with its parent Inventory.
        builder.HasOne<Inventory>()
            .WithMany()
            .HasForeignKey(x => x.InventoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.ExpectedLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(x => x.FoundLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ProcessedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
