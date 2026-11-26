using Bit.Fabric.Shared.Features.Attachments;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Status;

/// <summary>
/// A project's public status page (bitfabric-internal/specs/monitoring.md, Status page), rendered on the server as a plain
/// page, so a visitor on a slow phone during an outage gets it at once, without the app. The banner's words come from the
/// components' states, so the page can't stay green while a part is down. Following it by push is the only script it runs.
/// </summary>
public partial class StatusPublicPage
{
    /// <summary>The separator between the parts of a line, which in Persian isn't a middle dot (See MonitoringWords.Dot).</summary>
    private string Dot => MonitoringWords.Dot(Localizer);

    [Parameter] public string? Slug { get; set; }

    [AutoInject] private IPublicStatusController publicStatusController = default!;

    private PublicStatusDto? status;
    private bool notFound;
    private (Guid Id, string Name, StatusDays.Day[] Days, double? Uptime)[] rows = [];
    private StatusDays.Day[] appDays = [];

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        try
        {
            status = await publicStatusController.Get(Slug!, CultureInfo.CurrentUICulture.Name, CurrentCancellationToken);
        }
        catch (ResourceNotFoundException)
        {
            notFound = true;
            return;
        }

        var zone = TimeZoneService.CurrentTimeZone;

        rows = [.. status.Components.Select(component =>
        {
            var days = StatusDays.Of(component.Stretches, status.Maintenance, status.WatchedSince, zone);
            return (component.Id, component.Name ?? "", days, StatusDays.UptimeOf(days));
        })];

        appDays = StatusDays.Of([.. status.Components.SelectMany(component => component.Stretches)], status.Maintenance, status.WatchedSince, zone);
    }

    private string Banner => status!.State switch
    {
        ProjectHealth.Down => Localizer[nameof(AppStrings.MonitoringSomePartsAreDown)],
        ProjectHealth.Degraded => Localizer[nameof(AppStrings.MonitoringSomePartsAreNotWorkingProperly)],
        ProjectHealth.NoData => Localizer[nameof(AppStrings.MonitoringWeCannotCheckRightNow)],
        _ => status.InMaintenance ? Localizer[nameof(AppStrings.MonitoringUnderMaintenance)] : Localizer[nameof(AppStrings.MonitoringAllSystemsOperational)]
    };

    private string BannerTone => status!.State switch
    {
        ProjectHealth.Down => "tone-down",
        ProjectHealth.Degraded => "tone-degraded",
        ProjectHealth.NoData => "tone-nodata",
        _ => status.InMaintenance ? "tone-maintenance" : "tone-ok"
    };

    private string BannerIcon => status!.State switch
    {
        ProjectHealth.Down => BitIconName.StatusErrorFull,
        ProjectHealth.Degraded => BitIconName.WarningSolid,
        ProjectHealth.NoData => BitIconName.Unknown,
        _ => status.InMaintenance ? BitIconName.ConstructionCone : BitIconName.CompletedSolid
    };

    private string StateText(ProjectHealth state) => state switch
    {
        ProjectHealth.Down => Localizer[nameof(AppStrings.MonitoringOutage)],
        ProjectHealth.Degraded => Localizer[nameof(AppStrings.MonitoringDegraded)],
        ProjectHealth.NoData => Localizer[nameof(AppStrings.MonitoringNoData)],
        _ => Localizer[nameof(AppStrings.MonitoringOperational)]
    };

    private static string StateTone(ProjectHealth state) => state switch
    {
        ProjectHealth.Down => "tone-down",
        ProjectHealth.Degraded => "tone-degraded",
        ProjectHealth.NoData => "tone-nodata",
        _ => "tone-ok"
    };

    private static string DayTone(StatusDays.Tone tone) => tone switch
    {
        StatusDays.Tone.Down => "tone-down",
        StatusDays.Tone.Degraded => "tone-degraded",
        StatusDays.Tone.Maintenance => "tone-maintenance",
        StatusDays.Tone.NoData => "tone-nodata",
        _ => "tone-ok"
    };

    private string DayLabel(string name, StatusDays.Day day) => day.Tone switch
    {
        StatusDays.Tone.NoData => Localizer[nameof(AppStrings.MonitoringDayNoData), name, DateText(day.Date)],
        StatusDays.Tone.Down => Localizer[nameof(AppStrings.MonitoringDayOutageMinutes), name, DateText(day.Date), Math.Max(1, day.DownMinutes)],
        StatusDays.Tone.Degraded => Localizer[nameof(AppStrings.MonitoringDayDegradedMinutes), name, DateText(day.Date), Math.Max(1, day.DegradedMinutes)],
        StatusDays.Tone.Maintenance => Localizer[nameof(AppStrings.MonitoringDayMaintenance), name, DateText(day.Date)],
        _ => Localizer[nameof(AppStrings.MonitoringDayNoIncidents), name, DateText(day.Date)]
    };

    private string PhaseText(ProjectIncidentPhase phase) => phase switch
    {
        ProjectIncidentPhase.Identified => Localizer[nameof(AppStrings.MonitoringIdentified)],
        ProjectIncidentPhase.Monitoring => Localizer[nameof(AppStrings.MonitoringMonitoringAFix)],
        ProjectIncidentPhase.Resolved => Localizer[nameof(AppStrings.MonitoringResolved)],
        _ => Localizer[nameof(AppStrings.MonitoringInvestigating)]
    };

    private string ComponentNames(PublicIncidentDto incident)
        => string.Join(Localizer[nameof(AppStrings.MonitoringPartsSeparator)], incident.ComponentIds.Select(id => status!.Components.FirstOrDefault(component => component.Id == id)?.Name).Where(name => name is not null));

    private IEnumerable<(DateOnly Day, PublicIncidentDto[] Incidents)> PastDays()
    {
        var zone = TimeZoneService.CurrentTimeZone;
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, zone).DateTime);
        var byDay = status!.History.ToLookup(incident => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(incident.StartedAt, zone).DateTime));

        // The last week day by day, quiet days included, then the days with something to tell.
        return Enumerable.Range(0, 7).Select(index => today.AddDays(-index))
                         .Concat(byDay.Select(group => group.Key).Where(day => day < today.AddDays(-6)).OrderDescending())
                         .Select(day => (day, byDay[day].ToArray()));
    }

    private string LocalTime(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);

    private string Duration(PublicIncidentDto incident)
    {
        var duration = (incident.EndedAt ?? DateTimeOffset.UtcNow) - incident.StartedAt;
        return duration.TotalHours < 1 ? Localizer[nameof(AppStrings.MonitoringDurationMinutes), Math.Max(1, (int)duration.TotalMinutes)] : Localizer[nameof(AppStrings.MonitoringDurationHoursMinutes), (int)duration.TotalHours, duration.Minutes];
    }

    private static string DateText(DateOnly date) => date.ToString("d", CultureInfo.CurrentUICulture);

    private static string Percent(double? value) => value is { } percent ? $"{percent.ToString(percent >= 99.995 ? "0" : "0.00", CultureInfo.CurrentUICulture)}%" : "—";

    /// <summary>The app's own icon, from bit Fabric's server (See ProjectIcons); the monogram stands in for none and for one that fails to load.</summary>
    private string? IconUrl => status?.IconId is { } id ? new Uri(AbsoluteServerAddress, $"api/v1/Attachment/GetAttachment/{id}/{AttachmentKind.ProjectIcon}").ToString() : null;

    private string Monogram => string.Concat((status?.Name ?? "?").Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(word => char.ToUpperInvariant(word[0])));

    /// <summary>The page in another language, by the app's culture segment.</summary>
    private string LanguageUrl(string culture) => $"/{culture}/status/{Slug}";

    private string Feed(string kind) => new Uri(AbsoluteServerAddress, $"api/v1/PublicStatus/{kind}/{Slug}?language={CultureInfo.CurrentUICulture.Name}").ToString();

    private string FollowApi => new Uri(AbsoluteServerAddress, "api/v1/PublicStatus/").ToString();
}
