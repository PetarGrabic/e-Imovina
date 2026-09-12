using eImovina.Shared.Common;

namespace eImovina.Shared.Equipment;

public sealed record EquipmentFileDto(
    int Id,
    int EquipmentId,
    FileKind FileKind,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    DateTime UploadedAtUtc,
    bool IsCoverImage);
