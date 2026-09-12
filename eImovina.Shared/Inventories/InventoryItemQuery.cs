using eImovina.Shared.Common;

namespace eImovina.Shared.Inventories;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/inventories/{id}/items. Text matches
/// SnapshotInventoryNumber or SnapshotEquipmentName. FoundLocationId is the "location" filter -
/// ExpectedLocationId is constant across every item in one inventory (they're all expected at
/// that inventory's own location), so filtering on it would be a no-op; FoundLocationId is the
/// field that actually varies row-to-row and is the discrepancy signal.
/// </summary>
public sealed class InventoryItemQuery : ListQuery
{
    public bool? IsFound { get; set; }
    public bool? IsDamaged { get; set; }
    public int? FoundLocationId { get; set; }
}
