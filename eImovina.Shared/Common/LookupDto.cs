namespace eImovina.Shared.Common;

/// <summary>
/// Generic shape for every lookup endpoint (statuses, categories, location types, ...). The UI
/// always shows <see cref="Name"/> and stores/sends <see cref="Id"/>.
/// </summary>
public sealed record LookupDto(int Id, string Name, string? Code = null);
