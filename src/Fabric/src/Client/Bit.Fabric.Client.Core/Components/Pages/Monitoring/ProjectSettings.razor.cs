using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's settings, as the approved design has them (bitfabric-internal#1's Workbench design, bb36f0b;
/// bitfabric-internal/specs/monitoring.md): the approval it runs on, reconnecting with someone else and disconnecting, how
/// often and from where it is checked, its name and the time zone its SLA months follow, its maintenance windows, its whole
/// history to take away as CSV or JSON, and removing it.
/// </summary>
public partial class ProjectSettings
{
    /// <summary>The separator between the parts of a line, which in Persian isn't a middle dot (See MonitoringWords.Dot).</summary>
    private string Dot => MonitoringWords.Dot(Localizer);

    [Parameter, EditorRequired] public MonitoringDetailsDto Details { get; set; } = default!;

    [Parameter] public EventCallback<string> OnRenamed { get; set; }

    /// <summary>The project as it is once disconnected, for the page's head and banner.</summary>
    [Parameter] public EventCallback<MonitoringDto> OnDisconnected { get; set; }

    /// <summary>Opens another tab of the project's page, such as Locations.</summary>
    [Parameter] public EventCallback<string> OnOpenTab { get; set; }

    private const string LocationsTab = "locations";

    private static readonly (string Path, string ContentType) CsvFormat = ("csv", "text/csv");

    private static readonly (string Path, string ContentType) JsonFormat = ("json", "application/json");

    [AutoInject] private IProjectIncidentsController incidentsController = default!;
    [AutoInject] private IMonitoringsController monitoringsController = default!;
    [AutoInject] private HttpClient httpClient = default!;
    [AutoInject] private FileSaveService fileSaveService = default!;

    private ProjectSettingsDto? settings;
    private ProjectMaintenanceWindowDto[] windows = [];
    private bool isSaving;
    private bool isAddingWindow;
    private bool isConfirmingRemove;
    private bool isRemoving;
    private bool isConfirmingDisconnect;
    private bool isDisconnecting;

    private string? windowTitle;
    private BitDateRangePickerValue? windowRange;
    private bool windowRepeatsWeekly;

    private Guid MonitoringId => Details.Monitoring!.Id;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        settings = await incidentsController.GetSettings(MonitoringId, CurrentCancellationToken);
        windows = await incidentsController.GetMaintenanceWindows(MonitoringId, CurrentCancellationToken);
    }

    private async Task Save()
    {
        if (isSaving || settings is null) return;
        isSaving = true;

        try
        {
            settings = await incidentsController.SaveSettings(settings, CurrentCancellationToken);
            await OnRenamed.InvokeAsync(settings.Name);
            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringSaved)]);
        }
        finally
        {
            isSaving = false;
        }
    }

    private bool CanAddWindow => string.IsNullOrWhiteSpace(windowTitle) is false
                                 && windowRange is { StartDate: { } start, EndDate: { } end } && end > start
                                 && (windowRepeatsWeekly is false || end - start < TimeSpan.FromDays(7));

    private async Task AddWindow()
    {
        if (isAddingWindow || CanAddWindow is false) return;
        isAddingWindow = true;

        try
        {
            var added = await incidentsController.AddMaintenanceWindow(new()
            {
                MonitoringId = MonitoringId,
                Title = windowTitle,
                StartsAt = windowRange!.StartDate!.Value,
                EndsAt = windowRange.EndDate!.Value,
                RepeatsWeekly = windowRepeatsWeekly
            }, CurrentCancellationToken);

            windows = [.. windows.Append(added).OrderBy(window => window.StartsAt)];
            (windowTitle, windowRange, windowRepeatsWeekly) = (null, null, false);
        }
        finally
        {
            isAddingWindow = false;
        }
    }

    private async Task DeleteWindow(ProjectMaintenanceWindowDto window)
    {
        await incidentsController.DeleteMaintenanceWindow(window.Id, window.Version, CurrentCancellationToken);
        windows = [.. windows.Where(w => w.Id != window.Id)];
    }

    private async Task Disconnect()
    {
        if (isDisconnecting) return;
        isDisconnecting = true;

        try
        {
            var project = await monitoringsController.Disconnect(MonitoringId, CurrentCancellationToken);
            isConfirmingDisconnect = false;
            settings = await incidentsController.GetSettings(MonitoringId, CurrentCancellationToken);
            await OnDisconnected.InvokeAsync(project);
        }
        finally
        {
            isDisconnecting = false;
        }
    }

    /// <summary>
    /// The whole history as a file. The HttpClient rather than the generated proxy, which speaks json, as the SLA's exports
    /// do: the handler chain still sends the access token and the viewer's language, which the texts are written in.
    /// </summary>
    private async Task Export((string Path, string ContentType) format)
    {
        using var response = await httpClient.GetAsync($"{IMonitoringsController.ExportHistoryUri}/{MonitoringId}/{format.Path}", CurrentCancellationToken);

        var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                       ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                       ?? $"history.{format.Path}";

        await fileSaveService.Save(fileName, format.ContentType, await response.Content.ReadAsByteArrayAsync(CurrentCancellationToken));
    }

    /// <summary>"Germany, Eastern North America and Oceania", each in the reader's language.</summary>
    private string CheckedFrom(MonitoringDto project)
        => MonitoringWords.Parts(Localizer, [Localizer[nameof(AppStrings.MonitoringLocationGermany)].Value,
                                             .. NetworkLocations.All.Where(location => project.Locations.Contains(location.Code)).Select(location => Localizer[location.NameKey].Value)]);

    private async Task Remove()
    {
        if (isRemoving) return;
        isRemoving = true;

        try
        {
            await monitoringsController.Delete(MonitoringId, CurrentCancellationToken);
            NavigationManager.NavigateTo(PageUrls.Projects);
        }
        finally
        {
            isRemoving = false;
        }
    }

    private string WhenOf(ProjectMaintenanceWindowDto window)
    {
        var start = TimeZoneService.ToLocalTime(window.StartsAt);
        var end = TimeZoneService.ToLocalTime(window.EndsAt);

        return window.RepeatsWeekly
            ? Localizer[nameof(AppStrings.MonitoringEveryWeekday), start.ToString("dddd", CultureInfo.CurrentUICulture), start.ToString("t", CultureInfo.CurrentUICulture), end.ToString("t", CultureInfo.CurrentUICulture)]
            : $"{start.ToString("g", CultureInfo.CurrentUICulture)} – {end.ToString(start.Date == end.Date ? "t" : "g", CultureInfo.CurrentUICulture)}";
    }

    private string LocalTime(DateTimeOffset at) => TimeZoneService.ToLocalTime(at).ToString("g", CultureInfo.CurrentUICulture);
}
