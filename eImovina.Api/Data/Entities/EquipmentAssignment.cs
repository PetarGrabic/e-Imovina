namespace eImovina.Api.Data.Entities;

public class EquipmentAssignment
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int EmployeeId { get; set; }
    public int AssignmentStatusId { get; set; }
    public DateTime AssignedAtUtc { get; set; }
    public DateTime? ReturnedAtUtc { get; set; }
    public int AssignedByUserId { get; set; }
    public int? ClosedByUserId { get; set; }
    public string? Note { get; set; }
}
