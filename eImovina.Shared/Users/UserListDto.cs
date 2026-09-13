namespace eImovina.Shared.Users;

public sealed record UserListDto(
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
