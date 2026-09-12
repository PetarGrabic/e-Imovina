using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Assignments;

public sealed record AssignEquipmentDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati opremu.")] int EquipmentId,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati zaposlenika.")] int EmployeeId,
    [MaxLength(1000)] string? Note);
