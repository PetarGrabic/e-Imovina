namespace eImovina.Shared.WriteOffRequests;

public sealed record WriteOffRequestListDto(
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
    DateTime? DecisionAtUtc,
    DateTime? ExecutedAtUtc);
