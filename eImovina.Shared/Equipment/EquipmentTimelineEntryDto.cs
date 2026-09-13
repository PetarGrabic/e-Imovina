namespace eImovina.Shared.Equipment;

public sealed record EquipmentTimelineEntryDto(
    DateTime OccurredAtUtc,
    string EventType,
    string Description,
    int? RelatedFileId);
