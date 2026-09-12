namespace eImovina.Shared.Equipment;

public sealed record EquipmentListItemDto(
    int Id,
    string InventoryNumber,
    string? SerialNumber,
    string Name,
    string CategoryName,
    string StatusName,
    string LocationName,
    decimal? PurchaseValue,
    string? Currency,
    int? CoverFileId);
