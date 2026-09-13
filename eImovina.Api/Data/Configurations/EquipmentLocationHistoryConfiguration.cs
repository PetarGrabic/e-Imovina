using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentLocationHistoryConfiguration : IEntityTypeConfiguration<EquipmentLocationHistory>
{
    public void Configure(EntityTypeBuilder<EquipmentLocationHistory> builder)
    {
        builder.ToTable("EquipmentLocationHistories");

        builder.HasIndex(h => h.EquipmentId);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(h => h.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(h => h.FromLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Location>()
            .WithMany()
            .HasForeignKey(h => h.ToLocationId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(h => h.ChangedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
