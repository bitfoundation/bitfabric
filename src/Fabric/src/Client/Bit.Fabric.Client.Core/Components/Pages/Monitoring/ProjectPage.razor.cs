using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

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
    private ProjectDetailsDto? details;

    [Parameter] public Guid Id { get; set; }

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

    private void Renamed(string? name)
    {
        if (details?.Project is { } project)
        {
            project.Name = name;
        }
    }

    [AutoInject] private IProjectsController projectsController = default!;

    private static string BackIcon => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitIconName.Forward : BitIconName.Back;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();
        await Load();
    }

    private async Task Load()
    {
        isLoading = true;

        try
        {
            details = await projectsController.GetProject(Id, CurrentCancellationToken);
            isLoadFailed = false;
        }
        catch (ResourceNotFoundException)
        {
            // Only the server saying so means the project is gone.
            details = null;
            isLoadFailed = false;
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            // Didn't load: the page offers to try again, the project it showed stays, and the handler still reports it.
            isLoadFailed = details is null;
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
