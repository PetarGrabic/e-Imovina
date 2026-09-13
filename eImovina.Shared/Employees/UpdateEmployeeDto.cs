using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Employees;

public sealed record UpdateEmployeeDto(
    [Required(ErrorMessage = "Ime je obavezno.")] [MaxLength(100)] string FirstName,
    [Required(ErrorMessage = "Prezime je obavezno.")] [MaxLength(100)] string LastName,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati lokaciju.")] int LocationId);
