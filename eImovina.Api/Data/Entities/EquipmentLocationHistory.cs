namespace eImovina.Api.Data.Entities;

public class EquipmentLocationHistory
{
    public int Id { get; set; }
    public int EquipmentId { get; set; }
    public int? FromLocationId { get; set; }
    public int ToLocationId { get; set; }
    public DateTime ChangedAtUtc { get; set; }
    public int ChangedByUserId { get; set; }
}
