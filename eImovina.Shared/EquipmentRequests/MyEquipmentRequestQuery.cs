using eImovina.Shared.Common;

namespace eImovina.Shared.EquipmentRequests;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/equipmentrequests/mine. Text searches
/// Description. Sort accepts "category" | "description" | "status" | "decision"
/// (case-insensitive); defaults to "createdat" descending.
/// </summary>
public sealed class MyEquipmentRequestQuery : ListQuery
{
    public int? RequestStatusId { get; set; }
}
