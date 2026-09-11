namespace eImovina.Api.Data.Entities;

public class EquipmentRequest
{
    public int Id { get; set; }
    public int RequesterEmployeeId { get; set; }
    public int EquipmentCategoryId { get; set; }
    public int? RelatedEquipmentId { get; set; }
    public int RequestStatusId { get; set; }
    public string Description { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }
    public int? DecisionByUserId { get; set; }
    public DateTime? DecisionAtUtc { get; set; }
    public string? DecisionNote { get; set; }
}
