using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.WriteOffRequests;

/// <summary>
/// SubmittedByUserId is deliberately absent - identity always comes from the caller's JWT
/// (ICurrentUser.UserId), never the client. Submission is an InventoryManagement-only action (not
/// a /mine self-service flow like equipment requests), so it's the caller's user id, not an
/// employee claim.
/// </summary>
public sealed record CreateWriteOffRequestDto(
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati opremu.")] int EquipmentId,
    [Required(ErrorMessage = "Razlog je obavezan.")][MaxLength(1000)] string Reason);
