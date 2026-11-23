using Bit.Fabric.Server.Api.Features.Monitoring.Components;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// The weekly scans email (bitfabric-internal/specs/scanners.md, Decisions): once a week, every member of a team gets one
/// short email listing the team's projects' latest scans, their verdict, grade or score and the urgent findings, each
/// project with a link to its Scans tab, in the member's own language. A project whose team turned the email off on its
/// Scans tab is left out, and a team without a scan in the last 8 days gets none.
/// <para>
/// At most one a week a member of a team, however often the job runs or on however many servers: each email is claimed
/// in <see cref="ProjectScansEmail"/> by its team, member and week before it is handed to the email service, which sends
/// the sign-in codes too, so it must never look like a flood to the mail provider.
/// </para>
/// </summary>
public partial class ProjectScansEmailJobRunner
{
    public const string RecurringJobId = nameof(ProjectScansEmailJobRunner);

    /// <summary>A scan older than this is last week's news, and doesn't make an email on its own.</summary>
    private static readonly TimeSpan freshFor = TimeSpan.FromDays(8);

    /// <summary>The weeks whose emails are remembered, so the table stays a few rows a member.</summary>
    private const int WeeksRemembered = 8;

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IEnumerable<IProjectScanner> scanners = default!;
    [AutoInject] private HtmlRenderer htmlRenderer = default!;
    [AutoInject] private IBackgroundJobClient backgroundJobClient = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;
    [AutoInject] private ILogger<ProjectScansEmailJobRunner> logger = default!;

    /// <summary>Every team with a scan to report, on Monday morning (See Program.Middlewares).</summary>
    [DisableConcurrentExecution(timeoutInSeconds: 600), AutomaticRetry(Attempts = 0)]
    public async Task SendWeekly(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var since = now - freshFor;

        var forgotten = WeekOf(now).AddDays(-7 * WeeksRemembered);
        await dbContext.ProjectScansEmails.IgnoreQueryFilters().Where(email => email.WeekOf < forgotten).ExecuteDeleteAsync(cancellationToken);

        var tenantIds = await Reported(dbContext.Projects.IgnoreQueryFilters(), since)
            .Select(project => project.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var tenantId in tenantIds)
        {
            try
            {
                await SendTo(tenantId, now, cancellationToken);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogError(exp, "Sending the weekly scans email of team {TenantId} failed.", tenantId);
            }
        }
    }

    /// <summary>One team's emails for the week <paramref name="now"/> falls in; returns how many members it went to.</summary>
    public async Task<int> SendTo(Guid tenantId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var since = now - freshFor;

        var projects = await Reported(dbContext.Projects.IgnoreQueryFilters().AsNoTracking().Where(project => project.TenantId == tenantId), since)
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);

        if (projects.Count is 0)
            return 0;

        Guid[] projectIds = [.. projects.Select(project => project.Id)];

        // The cards' own summaries, so the email says what the projects page says.
        ProjectScanKind[] kindsRun = [.. scanners.Where(scanner => scanner.IsAvailable).Select(scanner => scanner.Kind)];
        var summaries = await ProjectScanReader.SummariesOf(dbContext, projectIds, kindsRun, now, cancellationToken, acrossTenants: true);

        Guid[] scanIds = [.. summaries.Values.SelectMany(scans => scans).Where(scan => scan.ScanId is not null).Select(scan => scan.ScanId!.Value)];

        var urgent = (await dbContext.ProjectScanFindings.IgnoreQueryFilters().AsNoTracking()
            .Where(finding => scanIds.Contains(finding.ScanId) && finding.Severity == ScanFindingSeverity.Urgent)
            .OrderBy(finding => finding.Id)
            .ToListAsync(cancellationToken))
            .ToLookup(finding => finding.ScanId);

        var team = await dbContext.Tenants.Where(tenant => tenant.Id == tenantId).Select(tenant => tenant.Title ?? tenant.Name).FirstOrDefaultAsync(cancellationToken) ?? "";

        // Everyone of the team sees every project: there are no roles on a team yet.
        var members = await dbContext.TenantMemberships
            .Where(member => member.TenantId == tenantId && member.AcceptedOn != null && member.User!.Email != null && member.User.EmailConfirmed)
            .Select(member => new { member.User!.Id, member.User.Email, member.User.DisplayName })
            .ToListAsync(cancellationToken);

        var weekOf = WeekOf(now);
        var sent = 0;

        foreach (var member in members)
        {
            // Claimed first: a second run, a retry or another server finds the week taken and sends nothing.
            var claimed = await dbContext.Database.ExecuteSqlAsync($"""
                INSERT INTO "ProjectScansEmails" ("TenantId", "UserId", "WeekOf", "SentAt")
                VALUES ({tenantId}, {member.Id}, {weekOf}, {now})
                ON CONFLICT DO NOTHING
                """, cancellationToken);

            if (claimed is 0)
                continue;

            var culture = await CultureOf(member.Id, cancellationToken);

            var (subject, body) = await InCulture(culture, async () =>
            {
                var model = new ProjectScansEmailModel
                {
                    DisplayName = member.DisplayName ?? member.Email!,
                    TeamName = team,
                    WebAppUrl = new Uri(settings.WebAppUrl ?? "https://bitfabric.co/"),
                    Projects = [.. projects.Select(project => new ProjectScansEmailProject(
                        project.Name ?? "",
                        Uri.TryCreate(project.Address, UriKind.Absolute, out var address) ? address.Host : project.Address ?? "",
                        ScansLinkOf(project.Id, culture),
                        [.. summaries[project.Id].Where(scan => scan.FinishedAt is not null).Select(scan => Scan(scan, now))],
                        [.. summaries[project.Id].Where(scan => scan.ScanId is not null)
                                                 .SelectMany(scan => urgent[scan.ScanId!.Value])
                                                 .Select(finding => ScanFindings.Localize(localizer, finding.Map())?.Title ?? finding.Key ?? "")]))]
                };

                return (localizer[nameof(AppStrings.MonitoringScansEmailSubject), team].Value, await Render(model));
            });

            backgroundJobClient.Enqueue<EmailServiceJobsRunner>(runner => runner.SendEmailJob(member.Email!, member.DisplayName ?? member.Email!, subject, body, null!, default));
            sent++;
        }

        if (sent > 0)
        {
            logger.LogInformation("Sent the weekly scans email of team {TenantId} to {Count} member(s).", tenantId, sent);
        }

        return sent;
    }

    /// <summary>The projects the email reports: in it, still watched, and scanned lately.</summary>
    private IQueryable<Models.Project> Reported(IQueryable<Models.Project> projects, DateTimeOffset since)
        => projects.Where(project => project.ConnectionStatus != ProjectConnectionStatus.Disconnected
                                     && dbContext.ProjectScanSettings.IgnoreQueryFilters().Any(row => row.ProjectId == project.Id && row.WeeklyEmail == false) == false
                                     && dbContext.ProjectScans.IgnoreQueryFilters().Any(scan => scan.ProjectId == project.Id && scan.FinishedAt >= since));

    /// <summary>A scan's line: its name, and its grade, score or days left with its verdict, in words.</summary>
    private ProjectScansEmailScan Scan(ProjectScanSummaryDto scan, DateTimeOffset now)
    {
        string name = scan.Kind switch
        {
            ProjectScanKind.Security => localizer[nameof(AppStrings.MonitoringScanSecurity)],
            ProjectScanKind.Seo => localizer[nameof(AppStrings.MonitoringScanSeo)],
            ProjectScanKind.DomainAndCertificate => localizer[nameof(AppStrings.MonitoringScanDomainAndCertificate)],
            ProjectScanKind.Performance => localizer[nameof(AppStrings.MonitoringScanPerformance)],
            _ => localizer[nameof(AppStrings.MonitoringScanAccessibility)]
        };

        if (scan.Outcome is not ProjectScanOutcome.Completed)
            return new(name, localizer[nameof(AppStrings.MonitoringScanCouldNotReach)], "#605E5C");

        string? figure = scan.Kind switch
        {
            ProjectScanKind.Security => scan.Grade,
            ProjectScanKind.DomainAndCertificate => scan.ExpiresAt is { } at
                ? at <= now ? localizer[nameof(AppStrings.MonitoringScanExpired)].Value : localizer[nameof(AppStrings.MonitoringDurationDays), (int)Math.Floor((at - now).TotalDays)].Value
                : null,
            _ => scan.Score?.ToString(CultureInfo.CurrentCulture)
        };

        var (verdict, color) = scan.Verdict switch
        {
            ProjectScanVerdict.Good => (localizer[nameof(AppStrings.MonitoringScanVerdictGood)].Value, "#107C10"),
            ProjectScanVerdict.NeedsWork => (localizer[nameof(AppStrings.MonitoringScanVerdictNeedsWork)].Value, "#8A5A00"),
            _ => (localizer[nameof(AppStrings.MonitoringScanVerdictPoor)].Value, "#A4262C")
        };

        return new(name, figure is null ? verdict : localizer[nameof(AppStrings.MonitoringScansEmailResult), figure, verdict], color);
    }

    /// <summary>The project's Scans tab, under the reader's language, so it opens in the language the email is in.</summary>
    private Uri ScansLinkOf(Guid projectId, CultureInfo culture)
        => new($"{(settings.WebAppUrl ?? "https://bitfabric.co/").TrimEnd('/')}/{culture.Name}{PageUrls.Projects}/{projectId}?tab=scans");

    private async Task<string> Render(ProjectScansEmailModel model)
    {
        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await htmlRenderer.RenderComponentAsync<ProjectScansEmailTemplate>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ProjectScansEmailTemplate.Model)] = model
            }));

            return rendered.ToHtmlString();
        });
    }

    /// <summary>The language the member last used bit Fabric in, as the team invitation's email takes it; English when none is known.</summary>
    private async Task<CultureInfo> CultureOf(Guid userId, CancellationToken cancellationToken)
    {
        var cultureName = await dbContext.UserSessions
            .Where(session => session.UserId == userId && session.CultureName != null)
            .OrderByDescending(session => session.RenewedOn ?? session.StartedOn)
            .Select(session => session.CultureName)
            .FirstOrDefaultAsync(cancellationToken);

        return CultureInfoManager.GetCultureInfo(cultureName) ?? CultureInfoManager.DefaultCulture;
    }

    private static async Task<T> InCulture<T>(CultureInfo culture, Func<Task<T>> write)
    {
        var (current, currentFormat) = (CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = culture;
            return await write();
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
            CultureInfo.CurrentCulture = currentFormat;
        }
    }

    /// <summary>The Monday, in UTC, of the week <paramref name="at"/> falls in.</summary>
    public static DateOnly WeekOf(DateTimeOffset at)
    {
        var day = DateOnly.FromDateTime(at.UtcDateTime);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }
}
