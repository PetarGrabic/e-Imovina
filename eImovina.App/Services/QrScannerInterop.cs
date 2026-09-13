using Microsoft.JSInterop;

namespace eImovina.App.Services;

/// <summary>
/// Thin wrapper around the global window.qrScanner JS object (wwwroot/js/qrScanner.js) - the
/// first JS interop in this app, kept as a small dedicated class rather than scattering
/// IJSRuntime calls across the scan page.
/// </summary>
public sealed class QrScannerInterop : IAsyncDisposable
{
    private readonly IJSRuntime _js;

    public QrScannerInterop(IJSRuntime js)
    {
        _js = js;
    }

    public async Task StartAsync(string videoElementId, object dotNetRef, CancellationToken ct = default)
    {
        await _js.InvokeVoidAsync("qrScanner.start", ct, videoElementId, dotNetRef);
    }

    public async ValueTask StopAsync()
    {
        try
        {
            await _js.InvokeVoidAsync("qrScanner.stop");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (e.g. tab closed) - nothing left to clean up client-side.
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }
}
