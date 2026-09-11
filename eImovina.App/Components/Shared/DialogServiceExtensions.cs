using MudBlazor;

namespace eImovina.App.Components.Shared;

/// <summary>
/// Ergonomic wrapper over IDialogService + ConfirmDialog so call sites (archive/delete/lock
/// actions, from Section 6 on) are a one-liner: `if (await DialogService.ConfirmAsync(...))`.
/// </summary>
public static class DialogServiceExtensions
{
    public static async Task<bool> ConfirmAsync(
        this IDialogService dialogService,
        string title,
        string contentText,
        string confirmText = "Potvrdi",
        Color color = Color.Primary)
    {
        var parameters = new DialogParameters<ConfirmDialog>
        {
            { x => x.ContentText, contentText },
            { x => x.ConfirmText, confirmText },
            { x => x.Color, color },
        };

        var dialogReference = await dialogService.ShowAsync<ConfirmDialog>(title, parameters);
        var result = await dialogReference.Result;
        return result is { Canceled: false };
    }
}
