namespace eImovina.Shared.Users;

// EmployeeId null unlinks the account from any employee profile.
public sealed record LinkEmployeeDto(int? EmployeeId);
