namespace eImovina.Shared.Analytics;

public sealed record LocationDiscrepancyDto(
    int LocationId,
    string LocationName,
    int MissingCount,
    int DamagedCount,
    int RelocatedCount,
    int InventoriesConsidered);
