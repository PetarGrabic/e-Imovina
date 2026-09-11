using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class LocationTypeConfiguration : IEntityTypeConfiguration<LocationType>
{
    public void Configure(EntityTypeBuilder<LocationType> builder)
    {
        builder.ToTable("LocationTypes");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - referenced by Locations.LocationTypeId in DemoDataSeeder. Do not renumber.
        builder.HasData(
            new LocationType { Id = 1, Name = "Ured" },
            new LocationType { Id = 2, Name = "Škola" },
            new LocationType { Id = 3, Name = "Skladište" },
            new LocationType { Id = 4, Name = "Terenska lokacija" });
    }
}
