using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Inventories;

/// <summary>
/// ResponsibleEmployeeId is required+validated for an Admin/InventoryManager caller; for a
/// LocationResponsible caller it's ignored server-side and force-set to their own EmployeeId
/// (real enforcement, not a hidden UI field - see InventoriesController.CreateInventory).
/// </summary>
public sealed record CreateInventoryDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati lokaciju.")] int LocationId,
    int? ResponsibleEmployeeId,
    [MaxLength(1000)] string? Note);
