using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentStatusHistoryConfiguration : IEntityTypeConfiguration<EquipmentStatusHistory>
{
    public void Configure(EntityTypeBuilder<EquipmentStatusHistory> builder)
    {
        builder.ToTable("EquipmentStatusHistories");

        builder.HasIndex(h => h.EquipmentId);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(h => h.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EquipmentStatus>()
            .WithMany()
            .HasForeignKey(h => h.FromStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<EquipmentStatus>()
            .WithMany()
            .HasForeignKey(h => h.ToStatusId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
