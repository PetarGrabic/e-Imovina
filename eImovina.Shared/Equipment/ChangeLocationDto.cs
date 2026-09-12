using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Equipment;

public sealed record ChangeLocationDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati lokaciju.")] int NewLocationId);
