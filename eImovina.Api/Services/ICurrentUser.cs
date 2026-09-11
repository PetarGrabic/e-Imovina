namespace eImovina.Api.Services;

/// <summary>
/// Reads the authenticated caller's identity from HttpContext.User claims. The single source of
/// truth every /mine endpoint (Sections 10, 12) will use to derive identity - never a
/// client-supplied id.
/// </summary>
public interface ICurrentUser
{
    int UserId { get; }
    string DisplayName { get; }
    IReadOnlyList<string> Roles { get; }
    int? EmployeeId { get; }
    bool IsInRole(string role);
}
