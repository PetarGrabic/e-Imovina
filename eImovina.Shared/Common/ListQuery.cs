namespace eImovina.Shared.Common;

/// <summary>
/// Base query shape for every server-side-filtered list (text search + sort + paging). Feature
/// query DTOs (e.g. an EquipmentQuery) inherit from this and add their own filter fields.
/// </summary>
public class ListQuery
{
    public string? Text { get; set; }
    public string? Sort { get; set; }
    public SortDirection Dir { get; set; } = SortDirection.Ascending;
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
