namespace Bit.Fabric.Client.Core.Infrastructure.Services;

/// <summary>
/// Reads the <c>/index.json</c> a Better Stack status page serves, in JSON:API's shape rather than <c>summary.json</c>'s
/// (https://betterstack.com/docs/uptime/status-pages/subscribing-to-status-updates/subscribing-to-api/), into the
/// <see cref="StatusPageSummary"/> that <see cref="StatusNotice"/> and BevTools' Status tab read: its overall state, its
/// monitors, its incidents (the <c>status_report</c>s a person or a failing monitor opened) and its maintenance, each
/// with its <c>status_update</c>s.
/// </summary>
public static class BetterStackStatusPage
{
    /// <summary>A Better Stack page's answer: a JSON:API document whose <c>data</c> is a <c>status_page</c>.</summary>
    public static bool Is(JsonElement root)
        => root.ValueKind is JsonValueKind.Object
           && root.TryGetProperty("data", out var data)
           && data.ValueKind is JsonValueKind.Object
           && Text(data, "type") is "status_page";

    /// <param name="address">Where the answer came from: the page is that address's site.</param>
    /// <param name="now">Tells a maintenance under way from one to come, and an incident still open from one that ended.</param>
    public static StatusPageSummary Read(JsonElement root, Uri address, DateTimeOffset now)
    {
        var data = root.GetProperty("data");
        var page = Attributes(data);
        var pageUrl = new Uri(address, "/").ToString();

        var included = root.TryGetProperty("included", out var list) && list.ValueKind is JsonValueKind.Array ? list.EnumerateArray().ToArray() : [];
        var updates = included.Where(item => Text(item, "type") is "status_update" && Text(item, "id") is not null)
                              .DistinctBy(item => Text(item, "id"))
                              .ToDictionary(item => Text(item, "id")!);

        var reports = included.Where(item => Text(item, "type") is "status_report").Select(report =>
        {
            var attributes = Attributes(report);
            var startsAt = Time(attributes, "starts_at");
            var endsAt = Time(attributes, "ends_at");
            var isMaintenance = Text(attributes, "report_type") is "maintenance";

            return (IsMaintenance: isMaintenance, Item: new StatusPageSummaryIncident
            {
                Id = Text(report, "id"),
                Name = Text(attributes, "title"),
                Status = isMaintenance
                    ? (endsAt <= now ? "completed" : startsAt <= now ? "in_progress" : "scheduled")
                    // Better Stack names no step of an incident, only whether it ended.
                    : (endsAt <= now ? "resolved" : null),
                Impact = Text(attributes, "aggregate_state") switch
                {
                    "downtime" => "major",
                    "degraded" => "minor",
                    "maintenance" => "maintenance",
                    _ => "none"
                },
                CreatedAt = startsAt,
                StartedAt = startsAt,
                ScheduledFor = isMaintenance ? startsAt : null,
                ScheduledUntil = isMaintenance ? endsAt : null,
                Shortlink = pageUrl,
                IncidentUpdates = [.. Related(report, "status_updates")
                    .Where(updates.ContainsKey)
                    .Select(id => Attributes(updates[id]))
                    .Select(update => new StatusPageSummaryUpdate { Body = Text(update, "message"), CreatedAt = Time(update, "published_at") })]
            });
        }).ToArray();

        return new()
        {
            Page = new() { Name = Text(page, "company_name"), Url = pageUrl, UpdatedAt = Time(page, "updated_at") },
            Status = new()
            {
                Indicator = Text(page, "aggregate_state") switch
                {
                    "operational" => "none",
                    "degraded" => "minor",
                    "downtime" => "major",
                    "maintenance" => "maintenance",
                    _ => null
                }
            },
            Components = [.. included.Where(item => Text(item, "type") is "status_page_resource").Select(Attributes).Select(resource => new StatusPageSummaryComponent
            {
                Name = Text(resource, "public_name"),
                Status = Text(resource, "status") switch
                {
                    "operational" => "operational",
                    "degraded" => "degraded_performance",
                    "downtime" => "major_outage",
                    "maintenance" => "under_maintenance",
                    _ => null
                }
            })],
            Incidents = [.. reports.Where(report => report.IsMaintenance is false).Select(report => report.Item)],
            ScheduledMaintenances = [.. reports.Where(report => report.IsMaintenance).Select(report => report.Item)]
        };
    }

    private static JsonElement Attributes(JsonElement item)
        => item.ValueKind is JsonValueKind.Object && item.TryGetProperty("attributes", out var attributes) && attributes.ValueKind is JsonValueKind.Object ? attributes : default;

    private static IEnumerable<string> Related(JsonElement item, string name)
        => item.ValueKind is JsonValueKind.Object
           && item.TryGetProperty("relationships", out var relationships) && relationships.ValueKind is JsonValueKind.Object
           && relationships.TryGetProperty(name, out var relationship) && relationship.ValueKind is JsonValueKind.Object
           && relationship.TryGetProperty("data", out var data) && data.ValueKind is JsonValueKind.Array
            ? data.EnumerateArray().Select(reference => Text(reference, "id")).OfType<string>()
            : [];

    /// <summary>A string, or a number written as one: JSON:API's ids are strings, though not every field is.</summary>
    private static string? Text(JsonElement item, string name)
        => item.ValueKind is JsonValueKind.Object && item.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null
            }
            : null;

    private static DateTimeOffset? Time(JsonElement item, string name)
        => DateTimeOffset.TryParse(Text(item, name), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal, out var time) ? time : null;
}
