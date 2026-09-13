using eImovina.Shared.Common;

namespace eImovina.Shared.Equipment;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/equipment. Sort accepts "name" |
/// "inventorynumber" | "category" | "status" | "location" (case-insensitive); defaults to "name"
/// ascending.
/// </summary>
public sealed class EquipmentQuery : ListQuery
{
    public int? CategoryId { get; set; }
    public int? StatusId { get; set; }
    public int? ExcludeStatusId { get; set; }
    public int? LocationId { get; set; }
    public int? EmployeeId { get; set; }
    public bool? IsArchived { get; set; }
}
