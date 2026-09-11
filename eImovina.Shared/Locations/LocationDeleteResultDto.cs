namespace eImovina.Shared.Locations;

/// <summary>
/// Tells the caller which of the two DELETE outcomes actually happened, so the UI can show the
/// right Croatian message: the row was physically removed (no history ever existed), or it was
/// only soft-deactivated (some history exists, but nothing currently blocks deactivation).
/// </summary>
public sealed record LocationDeleteResultDto(bool HardDeleted);
