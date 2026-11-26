using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringReads
{
    /// <summary>The page's settings with the report entries a component can be made of, and where it is now.</summary>
    public static StatusPageSettingsDto StatusPage(SampleProject project)
    {
        var page = project.StatusPage;
        page.PublicUrl = MonitoringSamples.PublicUrlOf(page);
        page.Checks = [.. project.Checks.Where(check => check.IsNotConfigured is false).Select(check => new StatusCheckDto { Key = check.Key, Title = check.Title, Category = check.Category })];

        return page;
    }

    /// <summary>The status page's components an incident names: its own, or those made of its parts, or all of them for the app as a whole.</summary>
    private static Guid[] ComponentsOf(SampleProject project, SampleIncident incident)
    {
        if (incident.ComponentIds.Length > 0)
            return incident.ComponentIds;

        var components = project.StatusPage.Components;

        return incident.CheckKeys.Length is 0
            ? [.. components.Select(component => component.Id)]
            : [.. components.Where(component => component.CheckKeys.Intersect(incident.CheckKeys).Any()).Select(component => component.Id)];
    }

    /// <summary>
    /// The public page as a visitor sees it in their language, or the page's main one: the components' states and 90 days
    /// from the incidents, what goes on now, the maintenance coming, and what's over. An update not written in the visitor's
    /// language comes in the main one, marked so.
    /// </summary>
    public static PublicStatusDto PublicStatus(SampleProject project, string? language, DateTimeOffset now)
    {
        var page = project.StatusPage;
        var main = page.Languages.FirstOrDefault() ?? MonitoringTexts.MainCulture;
        var culture = page.Languages.FirstOrDefault(candidate => language is not null && (candidate == language || candidate.Split('-')[0] == language.Split('-')[0])) ?? main;
        var counted = project.Incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && incident.DuringMaintenance is false).ToArray();
        var open = counted.Where(incident => incident.EndedAt is null).OrderByDescending(incident => incident.StartedAt).ToArray();
        var health = HealthOf(project);

        PublicIncidentDto Public(SampleIncident incident) => new()
        {
            Id = incident.Id,
            Kind = incident.Kind,
            Impact = incident.Impact,
            Title = MonitoringTexts.In(incident.Title, culture, main),
            StartedAt = incident.StartedAt,
            EndedAt = incident.EndedAt,
            ComponentIds = ComponentsOf(project, incident),
            Updates = [.. incident.Updates.Select(update => new PublicIncidentUpdateDto
            {
                At = update.At,
                Phase = update.Phase,
                Text = MonitoringTexts.In(update.Text, culture, main),
                IsFallback = MonitoringTexts.Has(update.Text, culture) is false
            })]
        };

        return new()
        {
            Slug = page.Slug,
            Name = project.Name,
            AppAddress = project.Address,
            IconId = project.IconId,
            State = health is ProjectHealth.NoData && project.ConnectionStatus is ProjectConnectionStatus.FirstChecks ? ProjectHealth.Operational : health,
            InMaintenance = project.Maintenance.Any(window => IsOn(window, now)),
            Culture = culture,
            Languages = page.Languages,
            CheckedAt = project.CheckedAt,
            WatchedSince = project.CreatedOn,
            MedianResponseMs = page.ShowsResponseTime ? Project(project, TimeZoneInfo.Utc, now).MedianMs : null,
            MedianVisitorMs = page.ShowsResponseTime ? Project(project, TimeZoneInfo.Utc, now).MedianVisitorMs : null,
            SupportAddress = page.SupportAddress,
            PushPublicKey = MonitoringSamples.PushPublicKey,
            Components = [.. page.Components.Where(component => component.IsShown).Select(component =>
            {
                var touching = counted.Where(incident => ComponentsOf(project, incident).Contains(component.Id)).ToArray();
                var ongoingImpacts = touching.Where(incident => incident.EndedAt is null).Select(incident => incident.Impact).ToArray();

                return new PublicComponentDto
                {
                    Id = component.Id,
                    Name = MonitoringTexts.In(component.Name, culture, main),
                    Description = component.Description.Count is 0 ? null : MonitoringTexts.In(component.Description, culture, main),
                    State = project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected ? ProjectHealth.NoData
                          : ongoingImpacts.Contains(ProjectHealth.Down) ? ProjectHealth.Down
                          : ongoingImpacts.Length > 0 ? ProjectHealth.Degraded
                          : ProjectHealth.Operational,
                    Stretches = [.. touching.Where(incident => (incident.EndedAt ?? now) > now.AddDays(-90))
                                            .Select(incident => new PublicStretchDto { From = incident.StartedAt, To = incident.EndedAt, Impact = incident.Impact })]
                };
            })],
            Active = [.. open.Select(Public)],
            Maintenance = [.. project.Maintenance.Select(window => Upcoming(window, now)).OfType<PublicMaintenanceDto>().OrderBy(window => window.StartsAt)],
            History = [.. project.Incidents.Where(incident => incident.EndedAt is { } ended && ended > now.AddDays(-90) && (incident.Kind is ProjectIncidentKind.Announcement || incident.DuringMaintenance is false))
                                           .OrderByDescending(incident => incident.StartedAt)
                                           .Select(Public)]
        };
    }

    private static bool IsOn(ProjectMaintenanceWindowDto window, DateTimeOffset now)
    {
        var length = window.EndsAt - window.StartsAt;

        for (var start = window.StartsAt; start <= now; start = start.AddDays(7))
        {
            if (start + length > now)
                return true;

            if (window.RepeatsWeekly is false)
                break;
        }

        return false;
    }

    /// <summary>The window's occurrence going on or next within 30 days; null when there is none.</summary>
    private static PublicMaintenanceDto? Upcoming(ProjectMaintenanceWindowDto window, DateTimeOffset now)
    {
        var length = window.EndsAt - window.StartsAt;
        var start = window.StartsAt;

        while (window.RepeatsWeekly && start + length <= now)
        {
            start = start.AddDays(7);
        }

        return start + length > now && start < now.AddDays(30)
            ? new() { Title = window.Title, StartsAt = start, EndsAt = start + length, RepeatsWeekly = window.RepeatsWeekly }
            : null;
    }
}
