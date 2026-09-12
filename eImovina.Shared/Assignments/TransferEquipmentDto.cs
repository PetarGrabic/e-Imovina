using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Assignments;

public sealed record TransferEquipmentDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati opremu.")] int EquipmentId,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati novog zaposlenika.")] int NewEmployeeId,
    [MaxLength(1000)] string? Note);
