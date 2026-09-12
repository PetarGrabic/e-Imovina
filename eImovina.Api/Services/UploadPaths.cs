namespace eImovina.Api.Services;

/// <summary>
/// Resolves physical paths under App_Data/uploads. Files are stored flat (no per-equipment
/// subfolders) - EquipmentFile.RelativePath and .StoredFileName are always the same "{guid}{ext}"
/// value; every write/read/delete goes through here so that convention lives in exactly one place.
/// </summary>
public static class UploadPaths
{
    public static string Root(IWebHostEnvironment env) => Path.Combine(env.ContentRootPath, "App_Data", "uploads");

    public static string FullPath(IWebHostEnvironment env, string relativePath) => Path.Combine(Root(env), relativePath);
}
