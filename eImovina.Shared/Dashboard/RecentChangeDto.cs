namespace eImovina.Shared.Dashboard;

/// <summary>
/// One row of the dashboard's "last 5 important changes" feed, sourced from the assignment/
/// inventory/write-off business history the guidelines already require - not a new audit log.
/// EquipmentId/EquipmentInventoryNumber/CoverFileId are null for a location-level event (an
/// inventory open/complete/lock) that doesn't identify one specific piece of equipment.
/// </summary>
public sealed record RecentChangeDto(
    DateTime OccurredAtUtc,
    string Description,
    int? EquipmentId,
    string? EquipmentInventoryNumber,
    int? CoverFileId);
