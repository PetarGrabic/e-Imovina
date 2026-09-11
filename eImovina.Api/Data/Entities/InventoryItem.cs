namespace eImovina.Api.Data.Entities;

public class InventoryItem
{
    public int Id { get; set; }
    public int InventoryId { get; set; }
    public int EquipmentId { get; set; }
    public int ExpectedLocationId { get; set; }
    public int? FoundLocationId { get; set; }
    public bool? IsFound { get; set; }
    public bool? IsDamaged { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int? ProcessedByUserId { get; set; }
    public string? Note { get; set; }

    // Snapshot fields, captured by value when the inventory is opened - see docs/database.dbml.
    public string SnapshotInventoryNumber { get; set; } = null!;
    public string SnapshotEquipmentName { get; set; } = null!;
    public string SnapshotCategoryName { get; set; } = null!;
    public string SnapshotStatusName { get; set; } = null!;
}
