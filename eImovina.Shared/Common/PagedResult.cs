namespace eImovina.Shared.Common;

/// <summary>
/// Generic server-side paging envelope returned by every paged list endpoint.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
