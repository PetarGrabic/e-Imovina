namespace eImovina.Api.Data.Entities;

public class Inventory
{
    public int Id { get; set; }
    public int LocationId { get; set; }
    public int ResponsibleEmployeeId { get; set; }
    public int InventoryStatusId { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string? Note { get; set; }
}
