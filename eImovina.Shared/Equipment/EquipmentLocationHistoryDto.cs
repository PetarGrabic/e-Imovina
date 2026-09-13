namespace eImovina.Shared.Equipment;

public sealed record EquipmentLocationHistoryDto(
    int Id,
    int EquipmentId,
    int? FromLocationId,
    string? FromLocationName,
    int ToLocationId,
    string ToLocationName,
    DateTime ChangedAtUtc,
    string ChangedByName);
