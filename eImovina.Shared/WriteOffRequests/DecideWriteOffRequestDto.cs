using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.WriteOffRequests;

/// <summary>
/// Restricted to Odobreno(3)/Odbijeno(4) - the guidelines explicitly don't require a
/// multi-level approval flow here, unlike equipment requests' wider status set.
/// </summary>
public sealed record DecideWriteOffRequestDto(
    [Range(3, 4, ErrorMessage = "Odluka mora biti Odobreno ili Odbijeno.")] int WriteOffRequestStatusId,
    [MaxLength(1000)] string? DecisionNote);
