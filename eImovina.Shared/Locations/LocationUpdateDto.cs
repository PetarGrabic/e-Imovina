using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Locations;

public sealed record LocationUpdateDto(
    [Required(ErrorMessage = "Naziv je obavezan.")] [MaxLength(200)] string Name,
    [Required(ErrorMessage = "Oznaka je obavezna.")] [MaxLength(20)] string Code,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati tip lokacije.")] int LocationTypeId,
    [MaxLength(300)] string? Address,
    bool IsActive);
