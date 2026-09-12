namespace eImovina.Shared.Common;

/// <summary>
/// Single source of truth for equipment-file upload constraints, referenced by both Api
/// (server-side validation) and App (MudFileUpload's Accept/MaxFileSize, client-side hint text,
/// and the maxAllowedSize passed to IBrowserFile.OpenReadStream) so the two projects can never
/// drift on what "too big" or "wrong type" means.
/// </summary>
public static class UploadLimits
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AllowedImageExtensions = new Dictionary<string, string>
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
    };

    public static readonly IReadOnlyDictionary<string, string> AllowedDocumentExtensions = new Dictionary<string, string>
    {
        [".pdf"] = "application/pdf",
    };
}
