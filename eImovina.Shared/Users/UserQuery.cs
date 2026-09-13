using eImovina.Shared.Common;

namespace eImovina.Shared.Users;

/// <summary>
/// Server-side filter/sort/paging shape for GET /api/users. Text searches UserName/Email. Sort
/// accepts "username" | "email" | "isactive" (case-insensitive); defaults to "username" ascending.
/// </summary>
public sealed class UserQuery : ListQuery
{
    public int? RoleId { get; set; }
    public bool? IsActive { get; set; }
}
