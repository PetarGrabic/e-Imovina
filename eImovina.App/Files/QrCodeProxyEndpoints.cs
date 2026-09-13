using eImovina.App.Services;

namespace eImovina.App.Files;

/// <summary>
/// GET /equipment/{id}/qrcode - proxies the QR PNG from Api's GET /api/equipment/{id}/qrcode
/// through App's own server-side ApiClient, same reasoning as FileProxyEndpoints: the browser only
/// ever talks to App's own origin and never needs the bearer token.
/// </summary>
public static class QrCodeProxyEndpoints
{
    public static void MapQrCodeProxyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/equipment/{id:int}/qrcode", HandleGetQrCodeAsync).RequireAuthorization();
    }

    private static async Task<IResult> HandleGetQrCodeAsync(int id, ApiClient api, CancellationToken ct)
    {
        using var response = await api.GetRawAsync($"api/equipment/{id}/qrcode", ct);
        if (!response.IsSuccessStatusCode)
        {
            return Results.StatusCode((int)response.StatusCode);
        }

        var contentType = response.Content.Headers.ContentType?.ToString() ?? "application/octet-stream";
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        return Results.File(bytes, contentType);
    }
}
