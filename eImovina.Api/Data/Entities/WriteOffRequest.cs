namespace eImovina.Api.Data.Entities;

public class WriteOffRequest
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int SubmittedByUserId { get; set; }
    public int WriteOffRequestStatusId { get; set; }
    public string Reason { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public int? DecisionByUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime? ExecutedAtUtc { get; set; }
    public int? ExecutedByUserId { get; set; }
}
