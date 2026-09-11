using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class InventoryStatusConfiguration : IEntityTypeConfiguration<InventoryStatus>
{
    public void Configure(EntityTypeBuilder<InventoryStatus> builder)
    {
        builder.ToTable("InventoryStatuses");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - referenced by Inventories.InventoryStatusId in DemoDataSeeder. Do not renumber.
        builder.HasData(
            new InventoryStatus { Id = 1, Name = "Nacrt" },
            new InventoryStatus { Id = 2, Name = "Otvorena" },
            new InventoryStatus { Id = 3, Name = "U tijeku" },
            new InventoryStatus { Id = 4, Name = "Završena" },
            new InventoryStatus { Id = 5, Name = "Zaključana" });
    }
}
