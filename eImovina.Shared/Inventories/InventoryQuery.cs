using eImovina.Shared.Common;

namespace eImovina.Shared.Inventories;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/inventories. Sort accepts "location" |
/// "status" | "opened" (case-insensitive); defaults to "createdat" descending. For a
/// non-Admin/Manager caller, LocationId is ignored server-side - the result is always force-scoped
/// to their own location.
/// </summary>
public sealed class InventoryQuery : ListQuery
{
    public int? LocationId { get; set; }
    public int? InventoryStatusId { get; set; }
    public int? ResponsibleEmployeeId { get; set; }
}
