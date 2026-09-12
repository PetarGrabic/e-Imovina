namespace eImovina.Shared.Equipment;

public sealed record EquipmentDetailDto(
    int Id,
    string InventoryNumber,
    string? SerialNumber,
    string Name,
    int EquipmentCategoryId,
    string CategoryName,
    int EquipmentStatusId,
    string StatusName,
    int CurrentLocationId,
    string LocationName,
    decimal? PurchaseValue,
    string? Currency,
    DateOnly? PurchaseDate,
    string? Notes,
    bool IsArchived,
    DateTime CreatedAtUtc,
    string? CurrentAssigneeName,
    int AssignmentHistoryCount,
    int FileCount,
    int WriteOffRequestCount);
