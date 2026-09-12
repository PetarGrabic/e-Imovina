namespace eImovina.Shared.Inventories;

/// <summary>
/// Snapshot fields (SnapshotInventoryNumber/SnapshotEquipmentName/SnapshotCategoryName/
/// SnapshotStatusName) are the ones captured by text value when the inventory was opened - they
/// never change even if the underlying Equipment is later renamed/recategorized. Reused as-is for
/// GET .../discrepancies (no separate DiscrepancyDto).
/// </summary>
public sealed record InventoryItemDto(
    int Id,
    int InventoryId,
    int EquipmentId,
    string SnapshotInventoryNumber,
    string SnapshotEquipmentName,
    string SnapshotCategoryName,
    string SnapshotStatusName,
    int ExpectedLocationId,
    string ExpectedLocationName,
    int? FoundLocationId,
    string? FoundLocationName,
    bool? IsFound,
    bool? IsDamaged,
    DateTime? ProcessedAtUtc,
    int? ProcessedByUserId,
    string? ProcessedByName,
    string? Note);
