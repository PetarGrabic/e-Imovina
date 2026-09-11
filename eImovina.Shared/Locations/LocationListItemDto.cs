namespace eImovina.Shared.Locations;

public sealed record LocationListItemDto(int Id, string Name, string Code, string LocationTypeName, string? Address, bool IsActive);
