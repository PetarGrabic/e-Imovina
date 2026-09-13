using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Users;

public sealed record CreateUserDto(
    [Required(ErrorMessage = "Korisničko ime je obavezno.")] [MaxLength(100)] string UserName,
    [Required(ErrorMessage = "E-mail je obavezan.")] [MaxLength(200)] [EmailAddress(ErrorMessage = "Neispravan format e-maila.")] string Email,
    [Required(ErrorMessage = "Lozinka je obavezna.")] [MinLength(6, ErrorMessage = "Lozinka mora imati najmanje 6 znakova.")] string Password,
    [MinLength(1, ErrorMessage = "Potrebno je odabrati barem jednu ulogu.")] IReadOnlyList<int> RoleIds,
    int? EmployeeId);
