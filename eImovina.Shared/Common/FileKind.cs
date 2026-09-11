namespace eImovina.Shared.Common;

/// <summary>
/// DTO-level file-kind enum for upload requests/responses. Deliberately separate from
/// eImovina.Api.Data.Entities.EquipmentFileKind - entity types never leak into Shared.
/// </summary>
public enum FileKind
{
    Image,
    Invoice,
    Warranty,
    ServiceDoc,
}
