using System.Security.Claims;
using eImovina.Shared.Common;

namespace eImovina.Api.Services;

public class CurrentUser : ICurrentUser
{
    private readonly ClaimsPrincipal _principal;

    public CurrentUser(IHttpContextAccessor accessor)
    {
        _principal = accessor.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
    }

    public int UserId => int.Parse(_principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0");

    public string DisplayName => _principal.FindFirstValue(ClaimTypes.Name) ?? "";

    public IReadOnlyList<string> Roles => _principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    public int? EmployeeId
    {
        get
        {
            var value = _principal.FindFirstValue(AuthClaimTypes.EmployeeId);
            return value is null ? null : int.Parse(value);
        }
    }

    public bool IsInRole(string role) => _principal.IsInRole(role);
}
