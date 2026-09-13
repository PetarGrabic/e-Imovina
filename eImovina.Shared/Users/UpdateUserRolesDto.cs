using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Users;

public sealed record UpdateUserRolesDto(
    [MinLength(1, ErrorMessage = "Potrebno je odabrati barem jednu ulogu.")] IReadOnlyList<int> RoleIds);
