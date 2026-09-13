namespace eImovina.Shared.EquipmentRequests;

public sealed record EquipmentRequestDetailDto(
    int Id,
    int RequesterEmployeeId,
    string RequesterEmployeeName,
    int EquipmentCategoryId,
    string CategoryName,
    int RequestStatusId,
    string StatusName,
    string Description,
    DateTime CreatedAtUtc,
    int? DecisionByUserId,
    string? DecisionByName,
    DateTime? DecisionAtUtc,
    string? DecisionNote);
