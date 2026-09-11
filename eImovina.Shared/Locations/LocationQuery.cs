using eImovina.Shared.Common;

namespace eImovina.Shared.Locations;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/locations. Sort accepts "name" | "code" |
/// "locationType" | "isActive" (case-insensitive); defaults to "name" ascending.
/// </summary>
public sealed class LocationQuery : ListQuery
{
    public int? LocationTypeId { get; set; }
    public bool? IsActive { get; set; }
}
