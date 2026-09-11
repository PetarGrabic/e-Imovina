using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentCategoryConfiguration : IEntityTypeConfiguration<EquipmentCategory>
{
    public void Configure(EntityTypeBuilder<EquipmentCategory> builder)
    {
        builder.ToTable("EquipmentCategories");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - referenced by Equipment.EquipmentCategoryId in DemoDataSeeder. Do not renumber.
        builder.HasData(
            new EquipmentCategory { Id = 1, Name = "Računalo" },
            new EquipmentCategory { Id = 2, Name = "Mrežna oprema" },
            new EquipmentCategory { Id = 3, Name = "Namještaj" },
            new EquipmentCategory { Id = 4, Name = "Alat" },
            new EquipmentCategory { Id = 5, Name = "Ostalo" });
    }
}
