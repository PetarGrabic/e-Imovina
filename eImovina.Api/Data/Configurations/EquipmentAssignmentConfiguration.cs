using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentAssignmentConfiguration : IEntityTypeConfiguration<EquipmentAssignment>
{
    public void Configure(EntityTypeBuilder<EquipmentAssignment> builder)
    {
        builder.ToTable("EquipmentAssignments");
        builder.Property(a => a.Note).HasMaxLength(1000);

        builder.HasIndex(a => a.EquipmentId);
        builder.HasIndex(a => a.EmployeeId);
        builder.HasIndex(a => a.AssignmentStatusId);

        // The real DB-level guard against two active assignments for the same equipment.
        // 1 = Aktivno (AssignmentStatusConfiguration's fixed lookup id).
        builder.HasIndex(a => a.EquipmentId)
            .IsUnique()
            .HasDatabaseName("UX_Assignments_OneActivePerEquipment")
            .HasFilter("AssignmentStatusId = 1");

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(a => a.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AssignmentStatus>()
            .WithMany()
            .HasForeignKey(a => a.AssignmentStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        // Who opened the assignment.
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Who performed the return/transfer/cancel (nullable until closed).
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(a => a.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
