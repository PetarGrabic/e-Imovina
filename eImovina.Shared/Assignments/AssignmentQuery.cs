using eImovina.Shared.Common;

namespace eImovina.Shared.Assignments;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/assignments. Sort accepts "assignedat" |
/// "equipment" | "employee" (case-insensitive); defaults to "assignedat" descending.
/// </summary>
public sealed class AssignmentQuery : ListQuery
{
    public int? EquipmentId { get; set; }
    public int? EmployeeId { get; set; }
    public int? StatusId { get; set; }
}
