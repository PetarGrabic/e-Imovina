namespace eImovina.Shared.Dashboard;

public sealed record MyDashboardDto(
    int CurrentAssignmentsCount,
    int OpenRequestsCount,
    int OpenInventoriesAtMyLocationCount);
