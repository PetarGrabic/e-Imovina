using eImovina.Shared.Common;

namespace eImovina.Shared.EquipmentRequests;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/equipmentrequests. Text searches
/// Description. Sort accepts "requester" | "category" | "status" (case-insensitive); defaults to
/// "createdat" descending.
/// </summary>
public sealed class EquipmentRequestQuery : ListQuery
{
    public int? RequestStatusId { get; set; }
    public int? EquipmentCategoryId { get; set; }
    public int? RequesterEmployeeId { get; set; }
}
