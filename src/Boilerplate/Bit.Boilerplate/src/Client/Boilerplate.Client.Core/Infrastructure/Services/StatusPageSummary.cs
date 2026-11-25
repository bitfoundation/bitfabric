using System.Text.Json.Serialization;

namespace Boilerplate.Client.Core.Infrastructure.Services;

/// <summary>
/// The parts of a status page's <c>summary.json</c> that <see cref="StatusPageNotice"/> shows, in the shape Atlassian
/// Statuspage made common and most status page services serve (See <see cref="ClientCoreSettings.StatusPageSummaryUrl"/>).
/// Every field is optional: a service that leaves one out still gets its notice.
/// </summary>
public class StatusPageSummary
{
    public StatusPageSummaryPage? Page { get; set; }

    /// <summary>The unresolved incidents.</summary>
    public StatusPageSummaryIncident[]? Incidents { get; set; }

    /// <summary>The upcoming and active planned maintenance.</summary>
    public StatusPageSummaryIncident[]? ScheduledMaintenances { get; set; }
}

public class StatusPageSummaryPage
{
    public string? Url { get; set; }
}

/// <summary>An incident, or a planned maintenance, which <c>summary.json</c> writes in the same shape.</summary>
public class StatusPageSummaryIncident
{
    public string? Id { get; set; }

    public string? Name { get; set; }

    /// <summary>
    /// An incident's: investigating, identified, monitoring, resolved or postmortem.
    /// A maintenance's: scheduled, in_progress, verifying or completed.
    /// </summary>
    public string? Status { get; set; }

    /// <summary>none, minor, major, critical or maintenance.</summary>
    public string? Impact { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? ScheduledFor { get; set; }

    public DateTimeOffset? ScheduledUntil { get; set; }

    public string? Shortlink { get; set; }

    public StatusPageSummaryUpdate[]? IncidentUpdates { get; set; }
}

public class StatusPageSummaryUpdate
{
    public string? Status { get; set; }

    public string? Body { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }

    public DateTimeOffset? DisplayAt { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(StatusPageSummary))]
public partial class StatusPageSummaryJsonContext : JsonSerializerContext;

public enum StatusNoticeKind
{
    /// <summary>A major or critical incident.</summary>
    Outage,

    /// <summary>A minor incident, or one without an impact.</summary>
    PartialOutage,

    /// <summary>Planned maintenance under way, or starting within <see cref="StatusNotice.MaintenanceLeadTime"/>.</summary>
    Maintenance
}

/// <summary>What <see cref="StatusPageNotice"/> shows: the most severe unresolved incident, else the next planned maintenance.</summary>
public record StatusNotice
{
    /// <summary>How long before a planned maintenance starts the notice tells about it.</summary>
    public static readonly TimeSpan MaintenanceLeadTime = TimeSpan.FromHours(24);

    public required StatusNoticeKind Kind { get; init; }

    public string? Title { get; init; }

    /// <summary>The latest update's text.</summary>
    public string? Text { get; init; }

    /// <summary>When the incident started, or the maintenance starts.</summary>
    public DateTimeOffset? StartsAt { get; init; }

    /// <summary>The latest update's time, or when the maintenance ends.</summary>
    public DateTimeOffset? UpdatedAt { get; init; }

    public DateTimeOffset? EndsAt { get; init; }

    /// <summary>An incident's step: 0 investigating, 1 identified, 2 monitoring, 3 resolved; null when the service names none of them.</summary>
    public int? Phase { get; init; }

    /// <summary>The maintenance is under way, rather than starting later.</summary>
    public bool InProgress { get; init; }

    /// <summary>The incident's or the status page's address, when it's an http(s) one.</summary>
    public string? Link { get; init; }

    /// <summary>Changes with a new incident, a new update or a maintenance that begins, so a closed notice comes back only then.</summary>
    public required string Key { get; init; }

    public static StatusNotice? From(StatusPageSummary? summary, DateTimeOffset now)
    {
        if (summary is null)
            return null;

        var pageUrl = HttpLink(summary.Page?.Url);

        var incident = (summary.Incidents ?? [])
            .Where(incident => incident is not null && incident.Status?.ToLowerInvariant() is not ("resolved" or "postmortem" or "completed"))
            .OrderByDescending(incident => Severity(incident.Impact))
            .ThenByDescending(incident => incident.StartedAt ?? incident.CreatedAt)
            .FirstOrDefault();

        if (incident is not null)
        {
            var latest = Latest(incident.IncidentUpdates);
            var status = latest?.Status ?? incident.Status;

            return new()
            {
                Kind = Severity(incident.Impact) >= 2 ? StatusNoticeKind.Outage : StatusNoticeKind.PartialOutage,
                Title = Blank(incident.Name),
                Text = Blank(latest?.Body),
                StartsAt = incident.StartedAt ?? incident.CreatedAt,
                UpdatedAt = latest is null ? null : latest.DisplayAt ?? latest.CreatedAt,
                Phase = status?.ToLowerInvariant() switch
                {
                    "investigating" => 0,
                    "identified" => 1,
                    "monitoring" => 2,
                    "resolved" => 3,
                    _ => null
                },
                Link = HttpLink(incident.Shortlink) ?? pageUrl,
                Key = $"{incident.Id ?? incident.Name}|{latest?.CreatedAt?.ToUnixTimeSeconds()}|{status}"
            };
        }

        var maintenance = (summary.ScheduledMaintenances ?? [])
            .Where(maintenance => maintenance is not null && maintenance.Status?.ToLowerInvariant() is not "completed")
            .Select(maintenance => (Item: maintenance, InProgress: maintenance.Status?.ToLowerInvariant() is "in_progress" or "verifying"
                                                                   || (maintenance.ScheduledFor <= now && (maintenance.ScheduledUntil is null || maintenance.ScheduledUntil > now))))
            .Where(maintenance => maintenance.InProgress || (maintenance.Item.ScheduledFor > now && maintenance.Item.ScheduledFor - now <= MaintenanceLeadTime))
            .OrderByDescending(maintenance => maintenance.InProgress)
            .ThenBy(maintenance => maintenance.Item.ScheduledFor)
            .FirstOrDefault();

        if (maintenance.Item is not null)
        {
            var (item, inProgress) = maintenance;

            return new()
            {
                Kind = StatusNoticeKind.Maintenance,
                Title = Blank(item.Name),
                Text = Blank(Latest(item.IncidentUpdates)?.Body),
                StartsAt = item.ScheduledFor,
                EndsAt = item.ScheduledUntil,
                InProgress = inProgress,
                Link = HttpLink(item.Shortlink) ?? pageUrl,
                Key = $"{item.Id ?? item.Name}|{item.ScheduledFor?.ToUnixTimeSeconds()}|{inProgress}"
            };
        }

        return null;
    }

    private static int Severity(string? impact) => impact?.ToLowerInvariant() switch
    {
        "critical" => 3,
        "major" => 2,
        "minor" => 1,
        _ => 0
    };

    private static StatusPageSummaryUpdate? Latest(StatusPageSummaryUpdate[]? updates)
        => (updates ?? []).Where(update => update is not null).OrderByDescending(update => update.DisplayAt ?? update.CreatedAt).FirstOrDefault();

    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();

    private static string? HttpLink(string? url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri.ToString() : null;
}
