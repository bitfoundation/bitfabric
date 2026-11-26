namespace Bit.Fabric.Client.Core.Components.Layout.Diagnostic;

/// <summary>
/// The app's tab in bit BevTools (<see cref="AppBevTools.AppPanelId"/>). A plain component of the app that names no
/// Bit.BevTools type, so it may be public here while Bit.BevTools.wasm is lazily loaded (See <see cref="AppBevTools"/>).
/// </summary>
public partial class AppDevToolsPanel
{
    [AutoInject] private IUserController userController = default!;
    [AutoInject] private IAppUpdateService appUpdateService = default!;
    [AutoInject] private ClearAppFilesService clearAppFilesService = default!;
    [AutoInject] private IExternalNavigationService externalNavigationService = default!;
    [AutoInject] private NotificationPreferenceService notificationPreferenceService = default!;

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

    /// <summary>
    /// Opens Hangfire's dashboard on this app's own origin, already signed in as this user. A plain browser navigation
    /// carries only a cookie, which <see cref="IUserController.UpdateSession"/> writes with the token's own expiry - so
    /// the token is refreshed first to buy a full lifetime rather than whatever is left of the current one.
    /// </summary>
    private async Task OpenHangfireDashboard()
    {
        await AuthManager.RefreshToken(requestedBy: nameof(OpenHangfireDashboard));

        await userController.UpdateSession(new()
        {
            AppVersion = TelemetryContext.AppVersion,
            DeviceInfo = TelemetryContext.Platform,
            CultureName = CultureInfoManager.InvariantGlobalization ? null : CultureInfo.CurrentUICulture.Name,
            NotificationStatus = await notificationPreferenceService.GetSessionStatus(), // Left out, it would mute the session.
            PlatformType = AppPlatform.Type
        }, CurrentCancellationToken);

        await externalNavigationService.NavigateTo(NavigationManager.ToAbsoluteUri("hangfire").ToString());
    }
}
