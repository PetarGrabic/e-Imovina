using eImovina.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace eImovina.Api.Data.Configurations;

public class EquipmentFileConfiguration : IEntityTypeConfiguration<EquipmentFile>
{
    public void Configure(EntityTypeBuilder<EquipmentFile> builder)
    {
        builder.ToTable("EquipmentFiles");
        builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.RelativePath).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UploadedAtUtc).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(x => x.FileKind).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(x => x.StoredFileName).IsUnique();
        builder.HasIndex(x => x.EquipmentId);

        // At most one cover image per equipment.
        builder.HasIndex(x => x.EquipmentId)
            .IsUnique()
            .HasDatabaseName("UX_EquipmentFiles_OneCoverPerEquipment")
            .HasFilter("IsCoverImage = 1");

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(x => x.EquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
