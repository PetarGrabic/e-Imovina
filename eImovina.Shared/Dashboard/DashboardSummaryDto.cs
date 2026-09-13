namespace eImovina.Shared.Dashboard;

public sealed record DashboardSummaryDto(
    int TotalEquipmentCount,
    int AssignedEquipmentCount,
    int OnServiceCount,
    int MissingCount,
    int OpenRequestsCount,
    int OpenInventoriesCount,
    decimal TotalRecordedValue,
    IReadOnlyList<LocationValueDto> ValueByLocation,
    IReadOnlyList<RecentChangeDto> RecentChanges);
