using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class WriteOffRequestStatusConfiguration : IEntityTypeConfiguration<WriteOffRequestStatus>
{
    public void Configure(EntityTypeBuilder<WriteOffRequestStatus> builder)
    {
        builder.ToTable("WriteOffRequestStatuses");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - Id 4 (Odbijeno) and 5 (Provedeno) are hardcoded into the filtered unique
        // index on WriteOffRequests (see WriteOffRequestConfiguration). Do not renumber.
        builder.HasData(
            new WriteOffRequestStatus { Id = 1, Name = "Zaprimljeno" },
            new WriteOffRequestStatus { Id = 2, Name = "U obradi" },
            new WriteOffRequestStatus { Id = 3, Name = "Odobreno" },
            new WriteOffRequestStatus { Id = 4, Name = "Odbijeno" },
            new WriteOffRequestStatus { Id = 5, Name = "Provedeno" });
    }
}
