namespace eImovina.Api.Data.Entities;

public class EquipmentFile
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public EquipmentFileKind FileKind { get; set; }
    public string OriginalFileName { get; set; } = null!;
    public string StoredFileName { get; set; } = null!;
    public string RelativePath { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public long SizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public int UploadedByUserId { get; set; }
    public bool IsCoverImage { get; set; }
}
