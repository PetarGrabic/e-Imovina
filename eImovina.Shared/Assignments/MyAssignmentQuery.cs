using eImovina.Shared.Common;

namespace eImovina.Shared.Assignments;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/assignments/mine. Text searches
/// InventoryNumber/Name. Sort accepts "inventorynumber" | "name" | "status" | "note"
/// (case-insensitive); defaults to "assignedat" descending. Always scoped to the caller's own
/// active (AssignmentStatusId == 1) assignments - no status filter, since that's the whole point
/// of "my currently assigned equipment" per the guidelines.
/// </summary>
public sealed class MyAssignmentQuery : ListQuery
{
}
