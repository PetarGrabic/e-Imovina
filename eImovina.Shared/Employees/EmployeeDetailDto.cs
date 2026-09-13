namespace eImovina.Shared.Employees;

public sealed record EmployeeDetailDto(
    int Id,
    string FirstName,
    string LastName,
    int LocationId,
    string LocationName,
    bool IsActive);
