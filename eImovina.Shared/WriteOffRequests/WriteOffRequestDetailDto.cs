namespace eImovina.Shared.WriteOffRequests;

public sealed record WriteOffRequestDetailDto(
    int Id,
    int EquipmentId,
    string EquipmentInventoryNumber,
    string EquipmentName,
    int WriteOffRequestStatusId,
    string StatusName,
    string Reason,
    int SubmittedByUserId,
    string SubmittedByName,
    DateTime CreatedAtUtc,
    int? DecisionByUserId,
    string? DecisionByName,
    DateTime? DecisionAtUtc,
    string? DecisionNote,
    DateTime? ExecutedAtUtc,
    int? ExecutedByUserId,
    string? ExecutedByName);
