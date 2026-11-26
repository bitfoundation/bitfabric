using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// The last step of adding a project, as the approved design draws it (bitfabric-internal#1's Workbench design, bb36f0b,
/// "Connected"; bitfabric-internal/specs/monitoring.md, Connecting a project): once the approval came back, the first
/// checks as they come in from each location, Germany's report at once and the bitplatform Network's within a minute or
/// two, the parts the report named, and the three next steps, each leading to where it is done: alerts on this device, the
/// status page, and inviting the team. The name was asked before the approval, so it isn't asked again here.
/// </summary>
public partial class ProjectConnected
{
    private static readonly TimeSpan pollEvery = TimeSpan.FromSeconds(5);

    /// <summary>A location that hasn't answered by then stays "checking" on the project's Locations tab instead.</summary>
    private static readonly TimeSpan pollFor = TimeSpan.FromMinutes(3);

    [Parameter, EditorRequired] public Guid MonitoringId { get; set; }

    [AutoInject] private IMonitoringsController monitoringsController = default!;
    [AutoInject] private IPushNotificationService pushNotificationService = default!;
    [AutoInject] private NotificationPreferenceService notificationPreferenceService = default!;
    [AutoInject] private IUserController userController = default!;

    private MonitoringDetailsDto? details;
    private bool areAlertsOn;
    private bool isPushBlocked;

    private static string ForwardIcon => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitIconName.Back : BitIconName.Forward;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        details = await monitoringsController.GetMonitoring(MonitoringId, CurrentCancellationToken);
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        // In the browser only: a prerender has nothing to wait for.
        _ = PollUntilEveryLocationAnswered();

        await RefreshAlertsState();
    }

    /// <summary>Looks again every few seconds until every location has a reading, the way the Scans tab waits for its scans.</summary>
    private async Task PollUntilEveryLocationAnswered()
    {
        try
        {
            var until = DateTimeOffset.UtcNow + pollFor;

            while (AllAnswered is false && DateTimeOffset.UtcNow < until)
            {
                await Task.Delay(pollEvery, CurrentCancellationToken);

                details = await monitoringsController.GetMonitoring(MonitoringId, CurrentCancellationToken);

                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException)
        {
            // The page was left.
        }
        catch (Exception exp)
        {
            ExceptionHandler.Handle(exp);
        }
    }

    /// <summary>
    /// Germany, where bit Fabric's own server read the report the moment the approval came back, then each location of the
    /// bitplatform Network the project is timed from, in the Locations tab's order: null while none answered yet.
    /// </summary>
    private (string Name, bool? Answered, string Reading)[] Locations
    {
        get
        {
            if (details?.Monitoring is not { } project)
                return [];

            var checking = Localizer[nameof(AppStrings.MonitoringChecking)].Value;

            (string, bool?, string) germany = details.CheckedAt is null
                ? (Localizer[nameof(AppStrings.MonitoringLocationGermany)], null, checking)
                : (Localizer[nameof(AppStrings.MonitoringLocationGermany)], true, (details.HomePageMs, details.HomePageVisitorMs) switch
                {
                    ({ } ms, { } visitorMs) => Localizer[nameof(AppStrings.MonitoringConnectedVisitorsAndServer), Localizer[nameof(AppStrings.AppHomeMs), visitorMs], Localizer[nameof(AppStrings.AppHomeMs), ms]],
                    ({ } ms, null) => Localizer[nameof(AppStrings.AppHomeMs), ms],
                    _ => Localizer[nameof(AppStrings.MonitoringReportRead)]
                });

            return
            [
                germany,
                .. NetworkLocations.All.Where(location => project.Locations.Contains(location.Code)).Select(location =>
                {
                    var name = Localizer[location.NameKey].Value;

                    return details.NetworkReadings.FirstOrDefault(reading => reading.Location == location.Code) is { } reading
                        ? reading.ResponseMs is { } ms
                            ? (name, (bool?)true, Localizer[nameof(AppStrings.MonitoringConnectedReading), ms, reading.Colo ?? ""].Value)
                            : (name, (bool?)false, Localizer[nameof(AppStrings.MonitoringConnectedNoAnswer), reading.Colo ?? ""].Value)
                        : (name, (bool?)null, checking);
                })
            ];
        }
    }

    private bool AllAnswered => details?.Monitoring is not null && Locations.All(location => location.Answered is not null);

    private static string PartTone(ProjectCheckDto check) => check.IsNotConfigured ? "tone-nodata" : check.Status switch
    {
        HealthCheckStatus.Unhealthy => "tone-down",
        HealthCheckStatus.Degraded => "tone-degraded",
        _ => "tone-ok"
    };

    private string TabHref(string tab) => $"{ProjectHref}?tab={tab}";

    /// <summary>The environment's page, in the project the first round linked it to (See ProjectLinker).</summary>
    private string ProjectHref => details?.Monitoring is { } monitoring ? PageUrls.ProjectEnvironment(monitoring.ProjectId, monitoring.Id) : PageUrls.Projects;

    /// <summary>
    /// The same steps the app menu's notifications switch takes when turned on (See AppMenu.ToggleNotifications): the
    /// permission first, while the click that asked for it is still the browser's last, then this device's subscription,
    /// so the push the project's first alert route sends whoever added it reaches this device too.
    /// </summary>
    private async Task TurnOnAlerts()
    {
        if (AppPlatform.IsWindows is false)
        {
            await pushNotificationService.RequestPermission(CurrentCancellationToken);
        }

        await notificationPreferenceService.SetEnabled(true);
        await pushNotificationService.Subscribe(CurrentCancellationToken);
        await userController.SetNotificationEnabled(true, CurrentCancellationToken);

        await RefreshAlertsState();
    }

    private async Task RefreshAlertsState()
    {
        var isEnabled = await notificationPreferenceService.IsEnabled();
        var isAvailable = AppPlatform.IsWindows || await pushNotificationService.IsAvailable(CurrentCancellationToken);

        areAlertsOn = isEnabled && isAvailable;
        isPushBlocked = isEnabled && isAvailable is false;

        StateHasChanged();
    }
}
