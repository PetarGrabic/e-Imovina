using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.EquipmentRequests;

/// <summary>
/// RequesterEmployeeId is deliberately absent - identity always comes from the caller's JWT
/// employeeId claim (see EquipmentRequestsController.CreateMine), never from the client.
/// </summary>
public sealed record CreateEquipmentRequestDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati kategoriju.")] int EquipmentCategoryId,
    [Required(ErrorMessage = "Opis je obavezan.")][MaxLength(1000)] string Description);
