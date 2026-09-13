using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.EquipmentRequests;

public sealed record DecideEquipmentRequestDto(
    [Range(1, 6, ErrorMessage = "Nepoznat status.")] int RequestStatusId,
    [MaxLength(1000)] string? DecisionNote);
