using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentStatusConfiguration : IEntityTypeConfiguration<EquipmentStatus>
{
    public void Configure(EntityTypeBuilder<EquipmentStatus> builder)
    {
        builder.ToTable("EquipmentStatuses");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - referenced by Equipment.EquipmentStatusId in DemoDataSeeder. Do not renumber.
        builder.HasData(
            new EquipmentStatus { Id = 1, Name = "Na skladištu" },
            new EquipmentStatus { Id = 2, Name = "Zaduženo" },
            new EquipmentStatus { Id = 3, Name = "Na servisu" },
            new EquipmentStatus { Id = 4, Name = "Nedostaje" },
            new EquipmentStatus { Id = 5, Name = "Otpisano" });
    }
}
