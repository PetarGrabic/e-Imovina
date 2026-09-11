using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentRequestConfiguration : IEntityTypeConfiguration<EquipmentRequest>
{
    public void Configure(EntityTypeBuilder<EquipmentRequest> builder)
    {
        builder.ToTable("EquipmentRequests");
        builder.Property(x => x.Description).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DecisionNote).HasMaxLength(1000);
        builder.Property(x => x.CreatedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasIndex(x => x.RequesterEmployeeId);
        builder.HasIndex(x => x.EquipmentCategoryId);
        builder.HasIndex(x => x.RequestStatusId);

        builder.HasOne<Employee>()
            .WithMany()
            .HasForeignKey(x => x.RequesterEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EquipmentCategory>()
            .WithMany()
            .HasForeignKey(x => x.EquipmentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(x => x.RelatedEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<RequestStatus>()
            .WithMany()
            .HasForeignKey(x => x.RequestStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.DecisionByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
