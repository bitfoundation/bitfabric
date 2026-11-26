using System.Runtime.CompilerServices;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// A project's whole history as a file its team takes with them (bitfabric-internal/specs/monitoring.md, Health history is
/// kept in bit Fabric's database, and it is the customer's): every period of each part, every incident and announcement
/// with its updates, every deployment and restart, and every maintenance window, as long as bit Fabric keeps them. Every
/// time is written in the project's time zone, with its offset, and in UTC alongside; texts bit Fabric writes in every
/// language are in the reader's. It is read a page at a time, by time and then id, and written out as it is read, so a
/// year of history never sits in memory and no query is larger than a page. The project comes through the tenant filter
/// (See MonitoringsService.FindProject), and every row is the project's.
/// </summary>
public partial class ProjectHistoryExport(AppDbContext dbContext, TimeProvider timeProvider)
{
    public enum Format { Csv, Json }

    /// <summary>The rows read at once.</summary>
    public const int PageSize = 500;

    /// <summary>The CSV's columns: one file, a row per record, the columns a record has no use for left empty.</summary>
    public const string CsvHeader = "record,id,incident_id,time_zone,started_at,started_at_utc,ended_at,ended_at_utc,minutes,part_keys,part,status,title,text,by,acknowledged_at,acknowledged_at_utc,during_maintenance";

    /// <summary>"Sales-history-2026-11-13.csv".</summary>
    public static string FileNameOf(ProjectMonitoring project, Format format, DateTimeOffset now)
        => $"{MonitoringFiles.NameOf(project.Name)}-history-{now.UtcDateTime:yyyy-MM-dd}.{(format is Format.Csv ? "csv" : "json")}";

    public async Task Write(ProjectMonitoring project, Format format, Stream output, CancellationToken cancellationToken)
    {
        var zone = ProjectSlaReports.ZoneOf(project);

        if (format is Format.Csv)
        {
            await WriteCsv(project, zone, output, cancellationToken);
        }
        else
        {
            await WriteJson(project, zone, output, cancellationToken);
        }
    }

    private async Task WriteCsv(ProjectMonitoring project, TimeZoneInfo zone, Stream output, CancellationToken cancellationToken)
    {
        // With a byte order mark, so a spreadsheet reads a part's name in any script as UTF-8, as the SLA's CSV does.
        await using var csv = new StreamWriter(output, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), bufferSize: 16 * 1024, leaveOpen: true);
        var culture = CultureInfo.CurrentUICulture.Name;
        var rows = 0;

        await csv.WriteAsync($"{CsvHeader}\n");

        async Task Row(params string?[] cells)
        {
            await csv.WriteAsync($"{string.Join(',', cells.Select(MonitoringFiles.CsvCell))}\n");

            // Out to the reader a page at a time, rather than when the whole file is done.
            if (++rows % PageSize is 0)
            {
                await csv.FlushAsync(cancellationToken);
            }
        }

        await foreach (var period in Periods(project.Id, cancellationToken))
        {
            await Row(["period", $"{period.Id}", null, zone.Id, Local(period.StartedAt, zone), Utc(period.StartedAt), Local(period.EndedAt, zone), Utc(period.EndedAt), Minutes(period.StartedAt, period.EndedAt),
                       period.CheckKey, period.Title, $"{period.Status}", null, period.Description, null, null, null, null]);
        }

        await foreach (var incident in Incidents(project.Id, cancellationToken))
        {
            await Row([incident.Kind is ProjectIncidentKind.Announcement ? "announcement" : "incident", $"{incident.Id}", null, zone.Id,
                       Local(incident.StartedAt, zone), Utc(incident.StartedAt), Local(incident.EndedAt, zone), Utc(incident.EndedAt), Minutes(incident.StartedAt, incident.EndedAt),
                       string.Join(' ', incident.CheckKeys), null, incident.Kind is ProjectIncidentKind.Announcement ? null : $"{incident.Impact}", ProjectTexts.In(incident.Title, culture), null,
                       incident.AcknowledgedBy, Local(incident.AcknowledgedAt, zone), Utc(incident.AcknowledgedAt), incident.DuringMaintenance ? "true" : "false"]);

            foreach (var update in incident.Updates)
            {
                await Row(["update", null, $"{incident.Id}", zone.Id, Local(update.At, zone), Utc(update.At), null, null, null,
                           null, null, $"{update.Phase}", null, ProjectTexts.In(update.Text, culture), update.Author ?? "bit Fabric", null, null, null]);
            }
        }

        await foreach (var start in Starts(project.Id, cancellationToken))
        {
            await Row([start.IsDeploy ? "deployment" : "restart", $"{start.Id}", null, zone.Id, Local(start.At, zone), Utc(start.At), null, null, null,
                       null, start.InstanceName, null, null, start.ApplicationVersion, null, null, null, null]);
        }

        await foreach (var window in Windows(project.Id, cancellationToken))
        {
            await Row(["maintenance", $"{window.Id}", null, zone.Id, Local(window.StartsAt, zone), Utc(window.StartsAt), Local(window.EndsAt, zone), Utc(window.EndsAt), Minutes(window.StartsAt, window.EndsAt),
                       null, null, window.RepeatsWeekly ? "weekly" : "once", window.Title, null, null, null, null, null]);
        }

        await csv.FlushAsync(cancellationToken);
    }

    private async Task WriteJson(ProjectMonitoring project, TimeZoneInfo zone, Stream output, CancellationToken cancellationToken)
    {
        // Never flushed by itself: every FlushAsync below hands what it holds to the response, a page at a time.
        await using var json = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true });
        var culture = CultureInfo.CurrentUICulture.Name;
        var rows = 0;

        async Task Written()
        {
            if (++rows % PageSize is 0)
            {
                await json.FlushAsync(cancellationToken);
            }
        }

        json.WriteStartObject();

        json.WriteStartObject("project");
        json.WriteString("id", project.Id);
        json.WriteString("name", project.Name);
        json.WriteString("address", project.Address);
        json.WriteString("serverAddress", project.ServerAddress);
        json.WriteString("timeZone", zone.Id);
        Time(json, "addedAt", project.CreatedOn, zone);
        json.WriteEndObject();

        Time(json, "exportedAt", timeProvider.GetUtcNow(), zone);

        json.WriteStartArray("periods");
        await foreach (var period in Periods(project.Id, cancellationToken))
        {
            json.WriteStartObject();
            json.WriteString("id", period.Id);
            json.WriteString("partKey", period.CheckKey);
            json.WriteString("part", period.Title);
            json.WriteString("status", $"{period.Status}");
            Time(json, "startedAt", period.StartedAt, zone);
            Time(json, "endedAt", period.EndedAt, zone);
            json.WriteString("description", period.Description);
            json.WriteEndObject();
            await Written();
        }
        json.WriteEndArray();

        json.WriteStartArray("incidents");
        await foreach (var incident in Incidents(project.Id, cancellationToken))
        {
            json.WriteStartObject();
            json.WriteString("id", incident.Id);
            json.WriteString("kind", $"{incident.Kind}");
            if (incident.Kind is ProjectIncidentKind.Incident)
            {
                json.WriteString("impact", $"{incident.Impact}");
            }
            json.WriteString("title", ProjectTexts.In(incident.Title, culture));
            json.WriteBoolean("isAutomatic", incident.IsAutomatic);
            json.WriteStartArray("partKeys");
            foreach (var key in incident.CheckKeys)
            {
                json.WriteStringValue(key);
            }
            json.WriteEndArray();
            Time(json, "startedAt", incident.StartedAt, zone);
            Time(json, "endedAt", incident.EndedAt, zone);
            Time(json, "afterDeploymentAt", incident.AfterDeployAt, zone);
            json.WriteBoolean("duringMaintenance", incident.DuringMaintenance);
            json.WriteString("acknowledgedBy", incident.AcknowledgedBy);
            Time(json, "acknowledgedAt", incident.AcknowledgedAt, zone);
            json.WriteStartArray("updates");
            foreach (var update in incident.Updates)
            {
                json.WriteStartObject();
                Time(json, "at", update.At, zone);
                json.WriteString("phase", $"{update.Phase}");
                json.WriteString("author", update.Author ?? "bit Fabric");
                json.WriteString("text", ProjectTexts.In(update.Text, culture));
                json.WriteEndObject();
            }
            json.WriteEndArray();
            json.WriteEndObject();
            await Written();
        }
        json.WriteEndArray();

        json.WriteStartArray("deploymentsAndRestarts");
        await foreach (var start in Starts(project.Id, cancellationToken))
        {
            json.WriteStartObject();
            json.WriteString("id", start.Id);
            json.WriteString("kind", start.IsDeploy ? "deployment" : "restart");
            Time(json, "at", start.At, zone);
            json.WriteString("applicationVersion", start.ApplicationVersion);
            json.WriteString("instance", start.InstanceName);
            json.WriteEndObject();
            await Written();
        }
        json.WriteEndArray();

        json.WriteStartArray("maintenanceWindows");
        await foreach (var window in Windows(project.Id, cancellationToken))
        {
            json.WriteStartObject();
            json.WriteString("id", window.Id);
            json.WriteString("title", window.Title);
            Time(json, "startsAt", window.StartsAt, zone);
            Time(json, "endsAt", window.EndsAt, zone);
            json.WriteBoolean("repeatsWeekly", window.RepeatsWeekly);
            json.WriteEndObject();
            await Written();
        }
        json.WriteEndArray();

        json.WriteEndObject();

        await json.FlushAsync(cancellationToken);
    }

    private IAsyncEnumerable<ProjectCheckPeriod> Periods(Guid monitoringId, CancellationToken cancellationToken)
        => Paged((after, seen) => dbContext.ProjectCheckPeriods.AsNoTracking()
                                           .Where(period => period.MonitoringId == monitoringId && (after == null || period.StartedAt >= after) && seen.Contains(period.Id) == false)
                                           .OrderBy(period => period.StartedAt).ThenBy(period => period.Id),
                 period => period.StartedAt, period => period.Id, cancellationToken);

    private IAsyncEnumerable<ProjectIncident> Incidents(Guid monitoringId, CancellationToken cancellationToken)
        => Paged((after, seen) => dbContext.ProjectIncidents.AsNoTracking()
                                           .Where(incident => incident.MonitoringId == monitoringId && (after == null || incident.StartedAt >= after) && seen.Contains(incident.Id) == false)
                                           .OrderBy(incident => incident.StartedAt).ThenBy(incident => incident.Id),
                 incident => incident.StartedAt, incident => incident.Id, cancellationToken);

    private IAsyncEnumerable<ProjectStart> Starts(Guid monitoringId, CancellationToken cancellationToken)
        => Paged((after, seen) => dbContext.ProjectStarts.AsNoTracking()
                                           .Where(start => start.MonitoringId == monitoringId && (after == null || start.At >= after) && seen.Contains(start.Id) == false)
                                           .OrderBy(start => start.At).ThenBy(start => start.Id),
                 start => start.At, start => start.Id, cancellationToken);

    private IAsyncEnumerable<ProjectMaintenanceWindow> Windows(Guid monitoringId, CancellationToken cancellationToken)
        => Paged((after, seen) => dbContext.ProjectMaintenanceWindows.AsNoTracking()
                                           .Where(window => window.MonitoringId == monitoringId && (after == null || window.StartsAt >= after) && seen.Contains(window.Id) == false)
                                           .OrderBy(window => window.StartsAt).ThenBy(window => window.Id),
                 window => window.StartsAt, window => window.Id, cancellationToken);

    /// <summary>
    /// Rows a page at a time, each page starting at the last one's time: the rows of that very time already written are
    /// left out by their ids, since the parts that fail in one round all start their periods at that round's time.
    /// </summary>
    private static async IAsyncEnumerable<T> Paged<T>(Func<DateTimeOffset?, Guid[], IQueryable<T>> page,
        Func<T, DateTimeOffset> timeOf,
        Func<T, Guid> idOf,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        DateTimeOffset? after = null;
        List<Guid> seenAtLast = [];

        while (true)
        {
            var rows = await page(after, [.. seenAtLast]).Take(PageSize).ToListAsync(cancellationToken);

            foreach (var row in rows)
            {
                yield return row;
            }

            if (rows.Count < PageSize)
                yield break;

            var last = timeOf(rows[^1]);
            if (last != after)
            {
                seenAtLast.Clear();
            }

            after = last;
            seenAtLast.AddRange(rows.Where(row => timeOf(row) == last).Select(idOf));
        }
    }

    /// <summary>"2026-11-13T10:02:00+03:30": the project's own clock, with its offset, so it reads unambiguously.</summary>
    private static string? Local(DateTimeOffset? at, TimeZoneInfo zone)
        => at is { } value ? TimeZoneInfo.ConvertTime(value, zone).ToString("yyyy-MM-dd'T'HH:mm:sszzz", CultureInfo.InvariantCulture) : null;

    private static string? Utc(DateTimeOffset? at)
        => at is { } value ? value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture) : null;

    /// <summary>How long it lasted, in whole minutes; empty while it lasts.</summary>
    private static string? Minutes(DateTimeOffset from, DateTimeOffset? to)
        => to is { } end ? ((long)Math.Round((end - from).TotalMinutes)).ToString(CultureInfo.InvariantCulture) : null;

    /// <summary>A time as two properties, <c>name</c> in the project's zone and <c>nameUtc</c>; nothing when there is none.</summary>
    private static void Time(Utf8JsonWriter json, string name, DateTimeOffset? at, TimeZoneInfo zone)
    {
        if (at is null)
            return;

        json.WriteString(name, Local(at, zone));
        json.WriteString($"{name}Utc", Utc(at));
    }
}
