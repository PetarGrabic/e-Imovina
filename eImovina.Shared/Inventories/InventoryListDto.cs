namespace eImovina.Shared.Inventories;

public sealed record InventoryListDto(
    int Id,
    int LocationId,
    string LocationName,
    int ResponsibleEmployeeId,
    string ResponsibleEmployeeName,
    int InventoryStatusId,
    string StatusName,
    DateTime? OpenedAtUtc,
    DateTime? CompletedAtUtc,
    DateTime? LockedAtUtc,
    DateTime CreatedAtUtc,
    string? Note);
