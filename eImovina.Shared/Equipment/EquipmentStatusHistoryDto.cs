namespace eImovina.Shared.Equipment;

public sealed record EquipmentStatusHistoryDto(
    int Id,
    int EquipmentId,
    int? FromStatusId,
    string? FromStatusName,
    int ToStatusId,
    string ToStatusName,
    DateTime ChangedAtUtc,
    string ChangedByName);
