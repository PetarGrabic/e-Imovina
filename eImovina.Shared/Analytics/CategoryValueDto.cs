namespace eImovina.Shared.Analytics;

public sealed record CategoryValueDto(
    int EquipmentCategoryId,
    string CategoryName,
    decimal TotalValue);
