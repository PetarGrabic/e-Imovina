namespace eImovina.Shared.Auth;

public sealed record LoginResponse(
    string Token,
    DateTime ExpiresAtUtc,
    string DisplayName,
    IReadOnlyList<string> Roles,
    int? EmployeeId);
