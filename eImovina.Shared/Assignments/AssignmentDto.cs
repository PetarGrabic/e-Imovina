namespace eImovina.Shared.Assignments;

public sealed record AssignmentDto(
    int Id,
    int EquipmentId,
    string InventoryNumber,
    string EquipmentName,
    int EmployeeId,
    string EmployeeName,
    int AssignmentStatusId,
    string StatusName,
    DateTime AssignedAtUtc,
    DateTime? ReturnedAtUtc,
    string AssignedByName,
    string? ClosedByName,
    string? Note);
