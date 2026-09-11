using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class WriteOffRequestConfiguration : IEntityTypeConfiguration<WriteOffRequest>
{
    public void Configure(EntityTypeBuilder<WriteOffRequest> builder)
    {
        builder.ToTable("WriteOffRequests");
        builder.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DecisionNote).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.EquipmentId);
        builder.HasIndex(x => x.WriteOffRequestStatusId);

        // Enforces "block a second write-off on the same equipment" (Section 13) as a real DB
        // constraint. 4 = Odbijeno, 5 = Provedeno (WriteOffRequestStatusConfiguration's fixed
        // lookup ids) - the two terminal statuses that free up the equipment for a new request.
        builder.HasIndex(x => x.EquipmentId)
            .IsUnique()
            .HasDatabaseName("UX_WriteOff_OneOpenPerEquipment")
            .HasFilter("WriteOffRequestStatusId NOT IN (4, 5)");

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.SubmittedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<WriteOffRequestStatus>()
            .WithMany()
            .HasForeignKey(x => x.WriteOffRequestStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.DecisionByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.ExecutedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
