using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's page (bitfabric-internal/specs/monitoring.md, A project's operations page), built only from what its
/// health report returns: its Overview (See ProjectOverview), History, Incidents, Performance (See ProjectPerformance),
/// Scans (See ProjectScans), SLA (See ProjectSla), the Locations it is checked from (See ProjectNetwork), its Status page
/// (See ProjectStatusPage), Alerts and Settings.
/// </summary>
public partial class ProjectPage
{
    private bool isLoading = true;
    private bool isLoadFailed;
    private bool isTroubleshootOpen;
    private ProjectDto? parent;
    private MonitoringDetailsDto? details;

    /// <summary>What the page shows now, so a switch to another project or environment loads it.</summary>
    private (Guid ProjectId, Guid? MonitoringId)? loaded;

    /// <summary>The project (bitfabric-internal/specs/monitoring.md, Projects, environments and their people).</summary>
    [Parameter] public Guid ProjectId { get; set; }

    /// <summary>Its environment the page shows: its Production, or its first, when the address names none.</summary>
    [Parameter] public Guid? MonitoringId { get; set; }

    /// <summary>The open tab, in the address so it can be shared and survives a refresh.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "tab")] public string? Tab { get; set; }

    /// <summary>The one part History and Performance show, by its report key, such as the map opens them on (See ProjectTabLink).</summary>
    [Parameter, SupplyParameterFromQuery(Name = "check")] public string? Check { get; set; }

    private const string OverviewTab = "overview";
    private const string MapTab = "map";
    private const string HistoryTab = "history";
    private const string IncidentsTab = "incidents";
    private const string PerformanceTab = "performance";
    private const string ScansTab = "scans";
    private const string SlaTab = "sla";
    private const string LocationsTab = "locations";
    private const string AlertsTab = "alerts";
    private const string StatusTab = "status";
    private const string SettingsTab = "settings";

    private static readonly string[] tabs = [OverviewTab, MapTab, HistoryTab, IncidentsTab, PerformanceTab, ScansTab, SlaTab, LocationsTab, StatusTab, AlertsTab, SettingsTab];

    private string SelectedTab => tabs.Contains(Tab) ? Tab! : OverviewTab;

    /// <summary>Another tab, on every part.</summary>
    private void OpenTab(string? key) => Open(new(key ?? OverviewTab));

    /// <summary>A tab, on one part when the link names it: the address says both, so it can be shared.</summary>
    private void Open(ProjectTabLink link)
    {
        NavigationManager.NavigateTo(NavigationManager.GetUriWithQueryParameters(new Dictionary<string, object?>
        {
            ["tab"] = link.Tab is OverviewTab ? null : link.Tab,
            ["check"] = link.Tab is HistoryTab or PerformanceTab ? link.Check : null
        }), replace: true);
    }

    /// <summary>The project's new name, and with it what its environments are called.</summary>
    private async Task Renamed(string? name)
    {
        if (parent is not null)
        {
            parent.Name = name;
        }

        await Load();
    }

    [AutoInject] private IMonitoringsController monitoringsController = default!;

    [AutoInject] private IProjectsController projectsController = default!;

    /// <summary>The separator between the parts of a line, which in Persian isn't a middle dot (See MonitoringWords.Dot).</summary>
    private string Dot => MonitoringWords.Dot(Localizer);

    /// <summary>An environment's page, on the tab this one shows.</summary>
    private string EnvironmentHref(MonitoringDto environment)
        => string.IsNullOrEmpty(Tab) ? PageUrls.ProjectEnvironment(environment.ProjectId, environment.Id) : $"{PageUrls.ProjectEnvironment(environment.ProjectId, environment.Id)}?tab={Uri.EscapeDataString(Tab)}";

    /// <summary>One that needs reconnecting is still checked through its public /health, so its health still shows.</summary>
    private static string ToneOf(MonitoringDto environment) => (environment.ConnectionStatus, environment.Health) switch
    {
        (ProjectConnectionStatus.Disconnected, _) => "tone-nodata",
        (_, ProjectHealth.Down) => "tone-down",
        (_, ProjectHealth.Degraded) => "tone-degraded",
        (_, ProjectHealth.Operational) => "tone-ok",
        _ => "tone-nodata"
    };

    private static string BackIcon => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitIconName.Forward : BitIconName.Back;

    protected override async Task OnParamsSetAsync()
    {
        await base.OnParamsSetAsync();

        // The switcher and the environments lead here again with other ids: the same page, another project.
        if (loaded != (ProjectId, MonitoringId))
        {
            await Load();
        }
    }

    private async Task Load()
    {
        isLoading = true;

        try
        {
            if (parent?.Id != ProjectId)
            {
                parent = null;
                details = null;
            }

            parent = await projectsController.GetProject(ProjectId, TimeZoneService.CurrentTimeZone.Id, CurrentCancellationToken);
            var environmentId = MonitoringId ?? parent.Monitorings.FirstOrDefault()?.Id;
            details = environmentId is { } id ? await monitoringsController.GetMonitoring(id, CurrentCancellationToken) : null;
            loaded = (ProjectId, MonitoringId);
            isLoadFailed = false;
        }
        catch (ResourceNotFoundException)
        {
            // Only the server saying so means the project, or the environment, is gone.
            parent = null;
            details = null;
            loaded = (ProjectId, MonitoringId);
            isLoadFailed = false;
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            // Didn't load: the page offers to try again, the project it showed stays, and the handler still reports it.
            isLoadFailed = parent is null;
            ExceptionHandler.Handle(exp);
        }
        finally
        {
            isLoading = false;
        }
    }

    private static string Host(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Authority : url ?? "";

    /// <summary>Where the last rounds ran from, each in the reader's language, with the language's own comma.</summary>
    private string RegionsText(string[] regions)
        => string.Join(Localizer[nameof(AppStrings.MonitoringPartsSeparator)], regions.Select(region => MonitoringWords.Region(Localizer, region)));
}
