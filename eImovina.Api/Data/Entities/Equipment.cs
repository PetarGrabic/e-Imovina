namespace eImovina.Api.Data.Entities;

public class Equipment
{
    public int Id { get; set; }
    public string InventoryNumber { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string Name { get; set; } = null!;
    public int EquipmentCategoryId { get; set; }
    public int EquipmentStatusId { get; set; }
    public int CurrentLocationId { get; set; }
    public decimal? PurchaseValue { get; set; }
    public string? Currency { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public string? Notes { get; set; }
    public bool IsArchived { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
