namespace Boilerplate.Client.Core.Components.Layout.Diagnostic;

/// <summary>
/// The app's tab in bit BevTools (<see cref="AppBevTools.AppPanelId"/>). A plain component of the app that names no
/// Bit.BevTools type, so it may be public here while Bit.BevTools.wasm is lazily loaded (See <see cref="AppBevTools"/>).
/// </summary>
public partial class AppDevToolsPanel
{
    [AutoInject] private IAppUpdateService appUpdateService = default!;
    [AutoInject] private ClearAppFilesService clearAppFilesService = default!;

    private static bool showKnownException = true;

    private static async Task ThrowTestException()
    {
        await Task.Delay(250);

        showKnownException = !showKnownException;

        throw showKnownException
            ? new InvalidOperationException("Something critical happened.").WithData("TestData", 1)
            : new DomainLogicException("Something bad happened.").WithData("TestData", 2);
    }

    private async Task ClearAppFiles()
    {
        await clearAppFilesService.ClearAppFiles(CurrentCancellationToken);
    }

    private async Task UpdateApp()
    {
        await appUpdateService.ForceUpdate();
    }
}
