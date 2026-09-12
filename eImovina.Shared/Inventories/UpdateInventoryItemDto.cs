using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Inventories;

public sealed record UpdateInventoryItemDto(
    bool IsFound,
    int? FoundLocationId,
    bool IsDamaged,
    [MaxLength(1000)] string? Note);
