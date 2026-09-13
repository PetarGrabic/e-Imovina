namespace eImovina.Shared.Equipment;

/// <summary>
/// Lets the caller warn the user that reactivated equipment won't retroactively join a
/// currently-open inventory at its location (inventory item snapshots are frozen at open time -
/// see PROJECT_GUIDELINES.md).
/// </summary>
public sealed record ReactivateEquipmentResultDto(bool HasLiveInventoryAtLocation);
