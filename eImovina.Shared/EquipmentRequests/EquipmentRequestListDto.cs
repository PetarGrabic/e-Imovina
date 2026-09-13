namespace eImovina.Shared.EquipmentRequests;

public sealed record EquipmentRequestListDto(
    int Id,
    int RequesterEmployeeId,
    string RequesterEmployeeName,
    int EquipmentCategoryId,
    string CategoryName,
    int RequestStatusId,
    string StatusName,
    string Description,
    DateTime CreatedAtUtc,
    DateTime? DecisionAtUtc);
