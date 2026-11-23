using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's Locations tab (bitfabric-internal/specs/monitoring.md, Germany, plus the locations the customer picks): bit
/// Fabric's own server in Germany, always on, and a checkbox per location of the bitplatform Network, each with its newest
/// reading and the data center it really ran in. The Network is all a page ever calls it.
/// </summary>
public partial class ProjectNetwork
{
    [Parameter, EditorRequired] public ProjectDto Project { get; set; } = default!;

    /// <summary>Each location's newest reading (See ProjectDetailsDto.NetworkReadings).</summary>
    [Parameter] public ProjectNetworkReadingDto[] Readings { get; set; } = [];

    /// <summary>The project as saved, with its new locations.</summary>
    [Parameter] public EventCallback<ProjectDto> OnSaved { get; set; }

    [AutoInject] private IProjectsController projectsController = default!;

    private ProjectDto? shownProject;
    private HashSet<string> picked = [];
    private bool isSaving;

    private bool IsChanged => picked.SetEquals(Project.Locations) is false;

    protected override async Task OnParamsSetAsync()
    {
        await base.OnParamsSetAsync();

        // A refresh of the page shows what a teammate saved, unless the customer is in the middle of picking.
        if (shownProject?.Id != Project.Id || picked.SetEquals(shownProject.Locations))
        {
            picked = [.. Project.Locations];
        }

        shownProject = Project;
    }

    private void Pick(NetworkLocation location, bool isPicked)
    {
        if (isPicked)
        {
            picked.Add(location.Code);
        }
        else
        {
            picked.Remove(location.Code);
        }
    }

    private async Task Save()
    {
        isSaving = true;

        try
        {
            var saved = await projectsController.SetLocations(new()
            {
                ProjectId = Project.Id,
                Locations = [.. NetworkLocations.All.Select(location => location.Code).Where(picked.Contains)]
            }, CurrentCancellationToken);

            picked = [.. saved.Locations];
            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringLocationsSaved)]);
            await OnSaved.InvokeAsync(saved);
        }
        finally
        {
            isSaving = false;
        }
    }

    /// <summary>What the location last saw, or why it has nothing to show yet.</summary>
    private string? ReadingText(NetworkLocation location)
    {
        var reading = Readings.FirstOrDefault(reading => reading.Location == location.Code);
        var isOn = Project.Locations.Contains(location.Code);

        if (reading is null)
            return isOn ? Localizer[nameof(AppStrings.MonitoringNoReadingInTheLastHour)].Value : null;

        var when = Ago(reading.At);

        return reading.ResponseMs is { } ms
            ? Localizer[nameof(AppStrings.MonitoringReadingFromDataCenter), FormatMs(ms), reading.Colo ?? "", when]
            : Localizer[nameof(AppStrings.MonitoringNoAnswerFromDataCenter), reading.Colo ?? "", when];
    }

    private string Ago(DateTimeOffset at)
    {
        var elapsed = DateTimeOffset.UtcNow - at;

        return elapsed.TotalSeconds switch
        {
            < 60 => Localizer[nameof(AppStrings.MonitoringJustNow)],
            < 3600 => Localizer[nameof(AppStrings.MonitoringMinutesAgo), (int)elapsed.TotalMinutes],
            _ => TimeZoneService.ToLocalTime(at).ToString("t", CultureInfo.CurrentUICulture)
        };
    }

    private string FormatMs(int ms) => ms < 1000
        ? Localizer[nameof(AppStrings.AppHomeMs), ms]
        : Localizer[nameof(AppStrings.AppHomeS), (ms / 1000.0).ToString("N1", CultureInfo.CurrentUICulture)];
}
