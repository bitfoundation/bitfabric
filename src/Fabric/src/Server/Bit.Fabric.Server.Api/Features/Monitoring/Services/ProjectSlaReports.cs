using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using static Bit.Fabric.Server.Api.Features.Monitoring.Services.ProjectSlaCalculator;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// A project's SLA report for a month (bitfabric-internal/specs/monitoring.md, SLA reports): reads what the month needs,
/// counts it with <see cref="ProjectSlaCalculator"/> and the owner's settings, and writes it as the tab's data, a CSV or
/// a PDF.
/// </summary>
public partial class ProjectSlaReports
{
    /// <summary>The months a report can be asked for, this one included: the rollups that say when the app was read are kept 13.</summary>
    public const int MonthCount = 12;

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;
    [AutoInject] private IWebHostEnvironment environment = default!;

    public async Task<ProjectSlaSettingsDto> SettingsOf(Guid projectId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.ProjectSlaSettings.AsNoTracking().FirstOrDefaultAsync(settings => settings.ProjectId == projectId, cancellationToken);

        return settings is null
            ? new() { ProjectId = projectId }
            : new()
            {
                ProjectId = settings.ProjectId,
                TargetPercent = settings.TargetPercent,
                PartialWeight = settings.PartialWeight,
                DegradedWeight = settings.DegradedWeight,
                Credits = [.. settings.Credits.OrderByDescending(credit => credit.BelowPercent).Select(credit => new ProjectSlaCreditDto { BelowPercent = credit.BelowPercent, CreditPercent = credit.CreditPercent })],
                Version = settings.Version
            };
    }

    public async Task<ProjectSlaReportDto> Build(Project project, int? year, int? month, CancellationToken cancellationToken)
    {
        var zone = ZoneOf(project);
        var now = timeProvider.GetUtcNow();
        var current = ProjectSlaMonths.MonthOf(now, zone);
        var first = ProjectSlaMonths.Add(current.Year, current.Month, -(MonthCount - 1));
        var selected = year is { } y && month is { } m ? (Year: y, Month: m) : current;

        if ((year is null) != (month is null) || selected.Month is < 1 or > 12 || Before(current, selected) || Before(selected, first))
            throw new BadRequestException(localizer[nameof(AppStrings.MonitoringSlaMonthOutOfRange), MonthCount]);

        var monthFrom = ProjectSlaMonths.StartOf(selected.Year, selected.Month, zone);
        var next = ProjectSlaMonths.Add(selected.Year, selected.Month, 1);
        var monthTo = ProjectSlaMonths.StartOf(next.Year, next.Month, zone);
        var spanFrom = ProjectSlaMonths.StartOf(first.Year, first.Month, zone);
        var afterCurrent = ProjectSlaMonths.Add(current.Year, current.Month, 1);
        var spanTo = ProjectSlaMonths.StartOf(afterCurrent.Year, afterCurrent.Month, zone);

        var settings = await SettingsOf(project.Id, cancellationToken);
        var readings = await ReadingsOf(project.Id, spanFrom, spanTo, cancellationToken);

        var incidents = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.ProjectId == project.Id && incident.StartedAt < spanTo && (incident.EndedAt == null || incident.EndedAt > spanFrom))
            .Select(incident => new ProjectIncident { Id = incident.Id, Impact = incident.Impact, StartedAt = incident.StartedAt, EndedAt = incident.EndedAt, DuringMaintenance = incident.DuringMaintenance })
            .ToListAsync(cancellationToken);

        var windows = await dbContext.ProjectMaintenanceWindows.AsNoTracking()
            .Where(window => window.ProjectId == project.Id && window.StartsAt < spanTo)
            .ToListAsync(cancellationToken);

        var periods = await dbContext.ProjectCheckPeriods.AsNoTracking()
            .Where(period => period.ProjectId == project.Id && period.StartedAt < monthTo && (period.EndedAt == null || period.EndedAt > monthFrom))
            .Select(period => new ProjectCheckPeriod { CheckKey = period.CheckKey, Title = period.Title, Status = period.Status, StartedAt = period.StartedAt, EndedAt = period.EndedAt })
            .ToListAsync(cancellationToken);

        Inputs InputsOf((int Year, int Month) of, IReadOnlyList<ProjectCheckPeriod> monthPeriods)
        {
            var after = ProjectSlaMonths.Add(of.Year, of.Month, 1);
            return new()
            {
                From = ProjectSlaMonths.StartOf(of.Year, of.Month, zone),
                To = ProjectSlaMonths.StartOf(after.Year, after.Month, zone),
                Now = now,
                WatchedSince = project.CreatedOn,
                Readings = readings,
                Maintenance = windows,
                Incidents = incidents,
                Periods = monthPeriods
            };
        }

        var result = Count(InputsOf(selected, periods));
        var (partialWeight, degradedWeight) = (settings.PartialWeight, settings.DegradedWeight);

        // What the app calls each part, as of its newest period of the month.
        var titles = periods.Where(period => period.CheckKey is not null && string.IsNullOrWhiteSpace(period.Title) is false)
                            .GroupBy(period => period.CheckKey!)
                            .ToDictionary(group => group.Key, group => group.MaxBy(period => period.StartedAt)!.Title!);

        var app = RowOf(null, project.Name, result.App, partialWeight, degradedWeight);
        var budget = result.ExpectedMinutes * (100 - settings.TargetPercent) / 100;

        return new()
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Address = project.Address,
            TimeZoneId = zone.Id,
            Year = selected.Year,
            Month = selected.Month,
            From = monthFrom,
            To = monthTo,
            IsCurrent = selected == current,
            GeneratedAt = now,
            Settings = settings,
            App = app,
            Parts = [.. result.Parts.Select(part => RowOf(part.Key, titles.GetValueOrDefault(part.Key, part.Key), part.Figure, partialWeight, degradedWeight))
                                    .OrderBy(part => part.Title, StringComparer.Create(CultureInfo.CurrentUICulture, ignoreCase: true))],
            BudgetMinutes = budget,
            LeftMinutes = Math.Max(0, budget - app.WeightedMinutes),
            Credit = CreditOf(app.Availability, settings.Credits),
            Months = [.. Enumerable.Range(0, MonthCount).Select(index =>
            {
                var of = ProjectSlaMonths.Add(first.Year, first.Month, index);
                var figure = of == selected ? result.App : Count(InputsOf(of, [])).App;
                return new ProjectSlaMonthDto { Year = of.Year, Month = of.Month, Availability = figure.Availability(partialWeight, degradedWeight), IsCurrent = of == current };
            })],
            Maintenance = [.. result.Maintenance.Select(occurrence => new ProjectSlaMaintenanceDto
            {
                Title = occurrence.Window.Title,
                StartsAt = occurrence.Span.From,
                EndsAt = occurrence.Span.To,
                RepeatsWeekly = occurrence.Window.RepeatsWeekly,
                Minutes = occurrence.CountedOutMinutes
            })],
            MaintenanceMinutes = result.MaintenanceMinutes,
            NoDataMinutes = result.NoDataMinutes,
            NotWatchedMinutes = result.NotWatchedMinutes
        };
    }

    /// <summary>The month as raw CSV: a row for the app and one per part, each with the weights it was counted with.</summary>
    public static byte[] Csv(ProjectSlaReportDto report)
    {
        var csv = new StringBuilder();
        csv.AppendLine("month,time_zone,part_key,part,availability_percent,target_percent,counted_minutes,down_minutes,partial_minutes,degraded_minutes,partial_weight,degraded_weight,weighted_minutes,maintenance_minutes,no_data_minutes");

        var month = $"{report.Year:D4}-{report.Month:D2}";

        foreach (var row in report.Parts.Prepend(report.App))
        {
            string[] cells =
            [
                month,
                Cell(report.TimeZoneId),
                Cell(row.Key ?? ProjectRollupKeys.Report),
                Cell(row.Title),
                Number(row.Availability, "0.####"),
                Number(report.Settings.TargetPercent, "0.####"),
                Number(row.CountedMinutes, "0.##"),
                Number(row.DownMinutes, "0.##"),
                Number(row.PartialMinutes, "0.##"),
                Number(row.DegradedMinutes, "0.##"),
                Number(report.Settings.PartialWeight, "0.##"),
                Number(report.Settings.DegradedWeight, "0.##"),
                Number(row.WeightedMinutes, "0.##"),
                Number(report.MaintenanceMinutes, "0.##"),
                Number(report.NoDataMinutes, "0.##")
            ];

            csv.Append(string.Join(',', cells)).Append('\n');
        }

        // With a byte order mark, so a spreadsheet reads a part's name in any script as UTF-8.
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())];

        static string Number(double? value, string format) => value?.ToString(format, CultureInfo.InvariantCulture) ?? "";

        static string Cell(string? value) => MonitoringFiles.CsvCell(value);
    }

    /// <summary>The month as a PDF in the viewer's language, in bit Fabric's look (See SlaPdfWriter).</summary>
    public byte[] Pdf(ProjectSlaReportDto report)
    {
        var logo = environment.WebRootFileProvider.GetFileInfo("images/icon.png");
        byte[]? logoBytes = null;

        if (logo.Exists)
        {
            using var stream = logo.CreateReadStream();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            logoBytes = memory.ToArray();
        }

        return new SlaPdfWriter(report, localizer, CultureInfo.CurrentUICulture, logoBytes).Write();
    }

    /// <summary>"Sales-sla-2026-11.pdf": the project's name in any script, without what a file name can't hold.</summary>
    public static string FileNameOf(ProjectSlaReportDto report, string extension)
        => $"{MonitoringFiles.NameOf(report.ProjectName)}-sla-{report.Year:D4}-{report.Month:D2}.{extension}";

    public static TimeZoneInfo ZoneOf(Project project)
        => TimeZoneInfo.TryFindSystemTimeZoneById(project.TimeZoneId ?? "UTC", out var zone) ? zone : TimeZoneInfo.Utc;

    private static ProjectSlaRowDto RowOf(string? key, string? title, Figure figure, double partialWeight, double degradedWeight) => new()
    {
        Key = key,
        Title = title,
        Availability = figure.Availability(partialWeight, degradedWeight),
        CountedMinutes = figure.CountedMinutes,
        DownMinutes = figure.DownMinutes,
        PartialMinutes = figure.PartialMinutes,
        DegradedMinutes = figure.DegradedMinutes,
        WeightedMinutes = figure.Weighted(partialWeight, degradedWeight)
    };

    private static bool Before((int Year, int Month) a, (int Year, int Month) b) => a.Year < b.Year || (a.Year == b.Year && a.Month < b.Month);

    /// <summary>
    /// When bit Fabric had a reading of the app, from the 15 minute rollups of its report, which a round writes even when
    /// the app doesn't answer but not when its token is refused (See ProjectProber.RollUp): consecutive quarters as one span.
    /// </summary>
    private async Task<List<Span>> ReadingsOf(Guid projectId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var islands = await dbContext.Database.SqlQuery<ReadingIsland>($"""
            SELECT MIN(reading."BucketStart") AS "From", MAX(reading."BucketStart") AS "Last"
            FROM (
                SELECT DISTINCT "BucketStart",
                       "BucketStart" - DENSE_RANK() OVER (ORDER BY "BucketStart") * make_interval(mins => {ProjectRollup.BucketMinutes}) AS "Island"
                FROM "ProjectRollups"
                WHERE "ProjectId" = {projectId} AND "CheckKey" = {ProjectRollup.ReportKey} AND "BucketStart" >= {from} AND "BucketStart" < {to}
            ) AS reading
            GROUP BY reading."Island"
            """).ToListAsync(cancellationToken);

        return Normalize(islands.Select(island => new Span(island.From, island.Last.AddMinutes(ProjectRollup.BucketMinutes))));
    }

    internal sealed class ReadingIsland
    {
        public DateTimeOffset From { get; set; }

        public DateTimeOffset Last { get; set; }
    }
}
