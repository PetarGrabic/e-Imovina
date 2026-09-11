namespace eImovina.Shared.Locations;

public sealed record LocationDetailDto(
    int Id,
    string Name,
    string Code,
    int LocationTypeId,
    string LocationTypeName,
    string? Address,
    bool IsActive,
    DateTime CreatedAtUtc);
