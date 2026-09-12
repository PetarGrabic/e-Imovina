namespace eImovina.Shared.Assignments;

public sealed record MyAssignmentDto(
    int AssignmentId,
    int EquipmentId,
    string InventoryNumber,
    string EquipmentName,
    string StatusName,
    DateTime AssignedAtUtc,
    string? Note);
