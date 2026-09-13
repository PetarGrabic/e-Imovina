using eImovina.Shared.Common;

namespace eImovina.Shared.WriteOffRequests;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/writeoffrequests. Text searches Reason. Sort
/// accepts "equipment" | "status" (case-insensitive); defaults to "createdat" descending.
/// </summary>
public sealed class WriteOffRequestQuery : ListQuery
{
    public int? EquipmentId { get; set; }
    public int? WriteOffRequestStatusId { get; set; }
}
