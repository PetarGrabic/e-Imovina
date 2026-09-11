using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class AssignmentStatusConfiguration : IEntityTypeConfiguration<AssignmentStatus>
{
    public void Configure(EntityTypeBuilder<AssignmentStatus> builder)
    {
        builder.ToTable("AssignmentStatuses");
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();
        builder.HasIndex(x => x.Name).IsUnique();

        // Fixed IDs - Id 1 (Aktivno) is hardcoded into the filtered unique index on
        // EquipmentAssignments (see EquipmentAssignmentConfiguration). Do not renumber.
        builder.HasData(
            new AssignmentStatus { Id = 1, Name = "Aktivno" },
            new AssignmentStatus { Id = 2, Name = "Vraćeno" },
            new AssignmentStatus { Id = 3, Name = "Premješteno" },
            new AssignmentStatus { Id = 4, Name = "Stornirano" });
    }
}
