namespace eImovina.Api.Data.Entities;

public class EquipmentStatusHistory
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int? FromStatusId { get; set; }
    public int ToStatusId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public int ChangedByUserId { get; set; }
}
