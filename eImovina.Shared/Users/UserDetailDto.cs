namespace eImovina.Shared.Users;

// Same shape as UserListDto - a user has no extra detail-only fields worth a separate shape.
public sealed record UserDetailDto(
    int Id,
    string UserName,
    string Email,
    bool IsActive,
    IReadOnlyList<string> Roles,
    int? EmployeeId,
    string? EmployeeName,
    int? EmployeeLocationId,
    string? EmployeeLocationName,
    DateTime CreatedAtUtc,
    DateTime? LastLoginAtUtc);
