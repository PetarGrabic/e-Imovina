using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class RequestStatusConfiguration : IEntityTypeConfiguration<RequestStatus>
{
    public void Configure(EntityTypeBuilder<RequestStatus> builder)
    {
        builder.ToTable("RequestStatuses");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - referenced by EquipmentRequests.RequestStatusId in DemoDataSeeder. Do not renumber.
        builder.HasData(
            new RequestStatus { Id = 1, Name = "Zaprimljeno" },
            new RequestStatus { Id = 2, Name = "U obradi" },
            new RequestStatus { Id = 3, Name = "Odobreno" },
            new RequestStatus { Id = 4, Name = "Odbijeno" },
            new RequestStatus { Id = 5, Name = "Realizirano" },
            new RequestStatus { Id = 6, Name = "Zatvoreno" });
    }
}
