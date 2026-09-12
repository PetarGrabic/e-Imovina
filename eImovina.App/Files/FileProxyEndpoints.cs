using eImovina.App.Services;

namespace eImovina.App.Files;

/// <summary>
/// GET /files/{fileId} - proxies file bytes from Api's GET /api/files/{fileId} through App's own
/// server-side ApiClient (which attaches the caller's bearer token via AuthHeaderHandler), so the
/// browser never needs the token to view an image or open a document. The browser only ever talks
/// to App's own origin, riding its normal auth cookie automatically for a same-origin request -
/// same reasoning as AccountEndpoints being minimal APIs rather than Razor components.
/// </summary>
public static class FileProxyEndpoints
{
    public static void MapFileProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/files/{fileId:int}", HandleGetFileAsync).RequireAuthorization();
    }

    private static async Task<IResult> HandleGetFileAsync(int fileId, ApiClient api, CancellationToken ct)
    {
        using var response = await api.GetRawAsync($"api/files/{fileId}", ct);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return Results.File(bytes, contentType);
    }
}
