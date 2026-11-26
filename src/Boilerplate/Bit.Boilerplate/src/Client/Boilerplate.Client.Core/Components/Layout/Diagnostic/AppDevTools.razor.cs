//+:cnd:noEmit
using Bit.BevTools;

namespace Boilerplate.Client.Core.Components.Layout.Diagnostic;

/// <summary>
/// Hosts bit BevTools, the app's in-app developer tools, and opens it on <see cref="ClientAppMessages.SHOW_DEV_TOOLS"/>:
/// published by tapping the header's spacer 7 times (DiagnosticSpacer), by Ctrl+Shift+D while BevTools is not attached
/// yet (MainLayout), by the error boundary's button, and by `App.showDevTools()` in the browser's console. Once attached,
/// BevTools answers Ctrl+Shift+D itself. The app's own panel in it is <see cref="AppDevToolsPanel"/>.
/// It stays from the start, since a failure during the start asks it to open (See AppErrorBoundary).
/// </summary>
public partial class AppDevTools
{
    [AutoInject] private BevToolsLoader bevToolsLoader = default!;
    //#if (signalR == true)
    [AutoInject] private ClearAppFilesService clearAppFilesService = default!;
    //#endif

    private readonly List<Action> unsubscribers = [];
    private bool isOpening;


    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        unsubscribers.Add(PubSubService.Subscribe(ClientAppMessages.SHOW_DEV_TOOLS, async payload =>
        {
            await InvokeAsync(() => Open(payload as string));
        }));

        //#if (signalR == true)
        // Here rather than in AppClientCoordinator's handler of the chatbot's message, which answers the server before the
        // app reloads.
        unsubscribers.Add(PubSubService.Subscribe(ClientAppMessages.CLEAR_APP_FILES, async _ =>
        {
            await clearAppFilesService.ClearAppFiles(CurrentCancellationToken);
        }));
        //#endif
    }

    /// <summary>
    /// Opens BevTools, loading and attaching it first where it was not registered at startup (See
    /// IClientCoreServiceCollectionExtensions). Where it cannot be opened - Blazor Server outside Development, or a
    /// download that failed offline - the loader says why, and so does the snack bar.
    /// </summary>
    private async Task Open(string? panel)
    {
        // A second ask while the first still downloads BevTools would overtake it, and the loader drops the first's answer.
        if (isOpening) return;

        isOpening = true;

        try
        {
            var (panelId, query) = ParsePanel(panel);

            if (await bevToolsLoader.OpenAsync(panelId, query) || bevToolsLoader.Problem is null) return;

            SnackBarService.Error("Open dev tools", bevToolsLoader.Problem);
        }
        finally
        {
            isOpening = false;
        }
    }

    /// <summary>
    /// The payload of <see cref="ClientAppMessages.SHOW_DEV_TOOLS"/>: a BevTools panel's id, optionally followed by a space
    /// and a filter for that panel (<c>logs level:error</c>). None opens the app's own panel.
    /// </summary>
    internal static (string PanelId, string? Query) ParsePanel(string? panel)
    {
        if (string.IsNullOrWhiteSpace(panel))
            return (AppBevTools.AppPanelId, null);

        var parts = panel.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return (parts[0], parts.Length > 1 ? parts[1] : null);
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        unsubscribers.ForEach(unsubscriber => unsubscriber());

        await base.DisposeAsync(disposing);
    }
}
