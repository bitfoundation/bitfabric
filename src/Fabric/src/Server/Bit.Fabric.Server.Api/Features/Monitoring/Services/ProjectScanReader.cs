using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// What a project's scans show, for the Scans tab (See ProjectScansController) and for an AI agent on bit Fabric's /mcp (See
/// ProjectScansMcpTools) alike. It reads through the tenant filter, so another team's scans are never found.
/// </summary>
public partial class ProjectScanReader
{
    /// <summary>"Run scans now" at most once this often a project.</summary>
    public static readonly TimeSpan RunNowInterval = TimeSpan.FromMinutes(10);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private TimeProvider timeProvider = default!;

    public async Task<ProjectScansDto> Latest(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var latestIds = await dbContext.ProjectScans
            .Where(scan => scan.MonitoringId == project.Id && scan.FinishedAt != null)
            .GroupBy(scan => scan.Kind)
            .Select(group => group.OrderByDescending(scan => scan.RequestedAt).Select(scan => scan.Id).First())
            .ToArrayAsync(cancellationToken);

        var latest = await WithFindings(await dbContext.ProjectScans.AsNoTracking().Where(scan => latestIds.Contains(scan.Id)).ToListAsync(cancellationToken), cancellationToken);

        var pending = (await dbContext.ProjectScans.AsNoTracking()
            .Where(scan => scan.MonitoringId == project.Id && scan.FinishedAt == null && scan.RequestedAt >= now - ProjectScanScheduler.StaleAfter)
            .OrderBy(scan => scan.RequestedAt)
            .ToListAsync(cancellationToken))
            .DistinctBy(scan => scan.Kind)
            .Select(scan => scan.Map());

        var lastRunNow = await dbContext.ProjectScans
            .Where(scan => scan.MonitoringId == project.Id && scan.Trigger == ProjectScanTrigger.Manual)
            .MaxAsync(scan => (DateTimeOffset?)scan.RequestedAt, cancellationToken);

        return new()
        {
            MonitoringId = project.Id,
            Address = project.Address,
            Latest = [.. latest.OrderBy(scan => Order(scan.Kind))],
            Pending = [.. pending.OrderBy(scan => Order(scan.Kind))],
            Settings = await Settings(project.Id, cancellationToken),
            CanRunAgainAt = lastRunNow + RunNowInterval > now ? lastRunNow + RunNowInterval : null
        };
    }

    /// <summary>Past runs, newest first, with how many findings of each severity each had.</summary>
    public async Task<ProjectScanDto[]> History(Guid monitoringId, ProjectScanKind? kind, int take, CancellationToken cancellationToken)
    {
        var rows = await dbContext.ProjectScans.AsNoTracking()
            .Where(scan => scan.MonitoringId == monitoringId && scan.FinishedAt != null && (kind == null || scan.Kind == kind))
            .OrderByDescending(scan => scan.RequestedAt)
            .Take(take)
            .Select(scan => new
            {
                Scan = scan,
                Urgent = scan.Findings.Count(finding => finding.Severity == ScanFindingSeverity.Urgent),
                Important = scan.Findings.Count(finding => finding.Severity == ScanFindingSeverity.Important),
                Improvement = scan.Findings.Count(finding => finding.Severity == ScanFindingSeverity.Improvement)
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row =>
        {
            var dto = row.Scan.Map();
            dto.UrgentCount = row.Urgent;
            dto.ImportantCount = row.Important;
            dto.ImprovementCount = row.Improvement;
            return dto;
        })];
    }

    /// <summary>One run with its findings; null when the tenant has no such run.</summary>
    public async Task<ProjectScanDto?> Scan(Guid scanId, CancellationToken cancellationToken)
    {
        var scan = await dbContext.ProjectScans.AsNoTracking().FirstOrDefaultAsync(scan => scan.Id == scanId, cancellationToken);

        return scan is null ? null : (await WithFindings([scan], cancellationToken))[0];
    }

    public async Task<ProjectScanSettingsDto> Settings(Guid monitoringId, CancellationToken cancellationToken)
    {
        var settings = await dbContext.ProjectScanSettings.AsNoTracking().FirstOrDefaultAsync(row => row.MonitoringId == monitoringId, cancellationToken);

        return settings is null
            ? new() { MonitoringId = monitoringId }
            : new() { MonitoringId = monitoringId, ScanAfterDeployments = settings.ScanAfterDeployments, WeeklyScans = settings.WeeklyScans, WeeklyEmail = settings.WeeklyEmail, Version = settings.Version };
    }

    /// <summary>
    /// The three settings as the tab or an AI agent sets them, the version they read checked against the row (a project
    /// of the caller's team; See ProjectScansController and ProjectScansMcpTools).
    /// </summary>
    public async Task<ProjectScanSettingsDto> SaveSettings(ProjectMonitoring project, ProjectScanSettingsDto settings, CancellationToken cancellationToken)
    {
        var saved = await dbContext.ProjectScanSettings.FirstOrDefaultAsync(row => row.MonitoringId == project.Id, cancellationToken);

        if (saved is null)
        {
            saved = new() { MonitoringId = project.Id, TenantId = project.TenantId };
            await dbContext.ProjectScanSettings.AddAsync(saved, cancellationToken);
        }
        else
        {
            saved.Version = settings.Version;
        }

        saved.ScanAfterDeployments = settings.ScanAfterDeployments;
        saved.WeeklyScans = settings.WeeklyScans;
        saved.WeeklyEmail = settings.WeeklyEmail;

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Settings(project.Id, cancellationToken);
    }

    private async Task<List<ProjectScanDto>> WithFindings(List<ProjectScan> scans, CancellationToken cancellationToken)
    {
        Guid[] scanIds = [.. scans.Select(scan => scan.Id)];

        var findings = (await dbContext.ProjectScanFindings.AsNoTracking()
            .Where(finding => scanIds.Contains(finding.ScanId))
            .OrderBy(finding => finding.Severity)
            .ThenBy(finding => finding.Id)
            .ToListAsync(cancellationToken))
            .ToLookup(finding => finding.ScanId);

        Guid[] findingIds = [.. findings.SelectMany(group => group).Select(finding => finding.Id)];

        var reported = (await dbContext.ProjectScanFindingReports
            .Where(report => findingIds.Contains(report.FindingId))
            .Select(report => report.FindingId)
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return [.. scans.Select(scan =>
        {
            var dto = scan.Map();
            dto.Findings = [.. findings[scan.Id].Select(finding =>
            {
                var findingDto = finding.Map();
                findingDto.IsReportedWrong = reported.Contains(finding.Id);
                return findingDto;
            })];
            dto.UrgentCount = dto.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Urgent);
            dto.ImportantCount = dto.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Important);
            dto.ImprovementCount = dto.Findings.Count(finding => finding.Severity is ScanFindingSeverity.Improvement);
            return dto;
        })];
    }

    /// <summary>
    /// Each project's newest scan of every kind bit Fabric runs (<paramref name="kindsRun"/>), and of a kind it ran before,
    /// for the projects page's cards and the weekly scans email: the newest finished run with its count and most urgent
    /// finding, and whether another is waiting or running. Narrowed to the tenant's project ids, so it stays on the indexes.
    /// <paramref name="acrossTenants"/> is for a background job, which has no tenant to filter by and names the projects of
    /// one itself (See ProjectScansEmailJobRunner).
    /// </summary>
    public static async Task<Dictionary<Guid, ProjectScanSummaryDto[]>> SummariesOf(AppDbContext dbContext, Guid[] monitoringIds, ProjectScanKind[] kindsRun, DateTimeOffset now, CancellationToken cancellationToken, bool acrossTenants = false)
    {
        var projectScans = acrossTenants ? dbContext.ProjectScans.IgnoreQueryFilters() : dbContext.ProjectScans;
        var projectScanFindings = acrossTenants ? dbContext.ProjectScanFindings.IgnoreQueryFilters() : dbContext.ProjectScanFindings;

        var latestIds = await projectScans
            .Where(scan => monitoringIds.Contains(scan.MonitoringId) && scan.FinishedAt != null)
            .GroupBy(scan => new { scan.MonitoringId, scan.Kind })
            .Select(group => group.OrderByDescending(scan => scan.RequestedAt).Select(scan => scan.Id).First())
            .ToArrayAsync(cancellationToken);

        var latest = await projectScans
            .Where(scan => latestIds.Contains(scan.Id))
            .Select(scan => new { scan.Id, scan.MonitoringId, scan.Kind, scan.FinishedAt, scan.Outcome, scan.Verdict, scan.Score, scan.Grade, scan.ExpiresAt, Findings = scan.Findings.Count() })
            .ToListAsync(cancellationToken);

        var topFindings = (await projectScanFindings
            .Where(finding => latestIds.Contains(finding.ScanId))
            .GroupBy(finding => finding.ScanId)
            .Select(group => group.OrderBy(finding => finding.Severity).ThenBy(finding => finding.Id).First())
            .ToListAsync(cancellationToken))
            .ToDictionary(finding => finding.ScanId);

        var running = (await projectScans
            .Where(scan => monitoringIds.Contains(scan.MonitoringId) && scan.FinishedAt == null && scan.RequestedAt >= now - ProjectScanScheduler.StaleAfter)
            .Select(scan => new { scan.MonitoringId, scan.Kind })
            .Distinct()
            .ToListAsync(cancellationToken))
            .ToHashSet();

        return monitoringIds.ToDictionary(monitoringId => monitoringId, monitoringId => ScanFindings.AvailableKinds.Where(kind => kindsRun.Contains(kind) || latest.Any(row => row.MonitoringId == monitoringId && row.Kind == kind)).Select(kind =>
        {
            var scan = latest.FirstOrDefault(row => row.MonitoringId == monitoringId && row.Kind == kind);

            return new ProjectScanSummaryDto
            {
                ScanId = scan?.Id,
                Kind = kind,
                IsRunning = running.Contains(new { MonitoringId = monitoringId, Kind = kind }),
                FinishedAt = scan?.FinishedAt,
                Outcome = scan?.Outcome,
                Verdict = scan?.Verdict,
                Score = scan?.Score,
                Grade = scan?.Grade,
                ExpiresAt = scan?.ExpiresAt,
                FindingCount = scan?.Findings ?? 0,
                TopFinding = scan is not null && topFindings.TryGetValue(scan.Id, out var top) ? top.Map() : null
            };
        }).ToArray());
    }

    /// <summary>The order a project's page and card show the kinds in.</summary>
    public static int Order(ProjectScanKind kind) => Array.IndexOf(ScanFindings.AvailableKinds, kind) is var index and >= 0 ? index : int.MaxValue;
}
