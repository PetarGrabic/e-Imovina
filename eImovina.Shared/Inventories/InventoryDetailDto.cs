namespace eImovina.Shared.Inventories;

/// <summary>
/// Deliberately has no embedded item array - items are server-paged/filtered/sorted via a
/// separate GET .../items endpoint (mirrors Assignments' separate GetHistory). The 6 counts here
/// drive InventoryDetails.razor's discrepancy-summary panel without requiring the full item list.
/// </summary>
public sealed record InventoryDetailDto(
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
    int CreatedByUserId,
    string CreatedByName,
    DateTime CreatedAtUtc,
    string? Note,
    int TotalItemCount,
    int ProcessedItemCount,
    int FoundCount,
    int MissingCount,
    int DamagedCount,
    int DiscrepancyCount);
