using System.ComponentModel.DataAnnotations;

namespace eImovina.Shared.Equipment;

public sealed record EquipmentCreateDto(
    [Required(ErrorMessage = "Inventurni broj je obavezan.")] [MaxLength(50)] string InventoryNumber,
    [MaxLength(100)] string? SerialNumber,
    [Required(ErrorMessage = "Naziv je obavezan.")] [MaxLength(200)] string Name,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati kategoriju.")] int EquipmentCategoryId,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati status.")] int EquipmentStatusId,
    [Range(1, int.MaxValue, ErrorMessage = "Potrebno je odabrati lokaciju.")] int CurrentLocationId,
    decimal? PurchaseValue,
    [MaxLength(3)] string? Currency,
    DateOnly? PurchaseDate,
    [MaxLength(1000)] string? Notes);
