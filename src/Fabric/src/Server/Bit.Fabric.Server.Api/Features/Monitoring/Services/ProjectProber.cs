using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Medallion.Threading;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// One round of checking a project from this server (bitfabric-internal/specs/monitoring.md, Checks): times what a visitor
/// waits for, reads the health report with the project's token, and records what changed: each check's period, a
/// deployment or restart, and the project's state. It runs in background jobs, so every query ignores the tenant filter
/// and names its rows' tenant.
/// </summary>
public partial class ProjectProber
{
    /// <summary>A deployment rolled out to several instances is one deployment, not one per instance.</summary>
    private static readonly TimeSpan sameDeploymentWindow = TimeSpan.FromMinutes(30);

    /// <summary>How long a round waits for another one of the same project; past it, that round's requests have all timed out.</summary>
    private static readonly TimeSpan roundWait = TimeSpan.FromMinutes(2);

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private IHttpClientFactory httpClientFactory = default!;
    [AutoInject] private MonitoringOAuthClient oauthClient = default!;
    [AutoInject] private ProjectAddressProbe addressProbe = default!;
    [AutoInject] private ProjectIncidentTracker incidentTracker = default!;
    [AutoInject] private ProjectAlerter alerter = default!;
    [AutoInject] private StatusPageService statusPages = default!;
    [AutoInject] private ProjectScanScheduler scanScheduler = default!;
    [AutoInject] private ProjectIcons icons = default!;
    [AutoInject] private ProjectLandings landings = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private DistributedLockFactory distributedLockFactory = default!;
    [AutoInject] private ILogger<ProjectProber> logger = default!;

    /// <summary>
    /// A round of the project, after the one on its way, if any. A round reads the project, waits seconds for its app's
    /// answers and then saves it, so two at once, as the first round CompleteConnect runs and the job's, would fail the later
    /// save on the project's Version. This one reads the project once the other is saved.
    /// </summary>
    public async Task<ProjectProbe> Probe(Guid projectId, CancellationToken cancellationToken)
    {
        await using (await RoundLock(projectId).AcquireAsync(roundWait, cancellationToken))
        {
            return await Round(projectId, cancellationToken);
        }
    }

    /// <summary>The job's round of the project every minute, unless one is on its way, which is this minute's.</summary>
    public async Task<ProjectProbe?> ProbeUnlessUnderway(Guid projectId, CancellationToken cancellationToken)
    {
        await using var round = await RoundLock(projectId).TryAcquireAsync(TimeSpan.Zero, cancellationToken);

        return round is null ? null : await Round(projectId, cancellationToken);
    }

    private IDistributedLock RoundLock(Guid projectId) => distributedLockFactory($"bit-fabric:monitoring-probe:{projectId}");

    private async Task<ProjectProbe> Round(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.IgnoreQueryFilters()
                                              .Include(p => p.Connection)
                                              .FirstAsync(p => p.Id == projectId, cancellationToken);

        var probe = new ProjectProbe
        {
            Id = Guid.CreateSequentialGuid(),
            ProjectId = project.Id,
            TenantId = project.TenantId,
            At = timeProvider.GetUtcNow(),
            Region = settings.Monitoring.Region
        };

        await TimeFrontDoor(project, probe, cancellationToken);
        await TimeForVisitors(project, probe, cancellationToken);
        await ReadReport(project, probe, cancellationToken);

        await dbContext.ProjectProbes.AddAsync(probe, cancellationToken);

        var deployed = false;

        if (probe.Outcome is ProjectProbeOutcome.Read)
        {
            await MovePeriods(project, probe, cancellationToken);
            deployed = await RecordStart(project, probe, cancellationToken);
        }

        if (deployed)
        {
            // A new build may land its front door on another page, while the old one still answers.
            landings.Forget(project);
        }

        var change = await incidentTracker.Track(project, probe, cancellationToken);

        project.LastCheckedAt = probe.At;
        project.Health = HealthOf(probe);

        // Nothing is read until someone reconnects, so the team hears it once, when it happens; one the team disconnected
        // themselves has no approval to lose and stays disconnected.
        var connectionLost = probe.Outcome is ProjectProbeOutcome.Unauthorized && project.ConnectionStatus is not (ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected);

        if (probe.Outcome is ProjectProbeOutcome.Unauthorized && project.ConnectionStatus is not ProjectConnectionStatus.Disconnected)
        {
            project.ConnectionStatus = ProjectConnectionStatus.ReconnectNeeded;
            project.ConnectionProblem = probe.Problem;
        }
        else if (probe.Outcome is ProjectProbeOutcome.Read && project.ConnectionStatus is ProjectConnectionStatus.FirstChecks)
        {
            project.ConnectionStatus = ProjectConnectionStatus.Connected;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        await RollUp(probe, cancellationToken);

        // After the round is saved, so an alert never names an incident that isn't.
        if (change is not null)
        {
            await alerter.Notify(project, change.Incident, change.Kind, cancellationToken);

            // The status page's followers hear when it begins and when it's over, in their language.
            await statusPages.NotifyFollowers(project, change.Incident, "", cancellationToken);
        }

        if (connectionLost)
        {
            await alerter.NotifyConnectionLost(project, cancellationToken);
        }

        if (deployed)
        {
            await ScanTheDeployment(project, probe, cancellationToken);
        }

        if (deployed || ProjectIcons.IsDue(project, probe.At))
        {
            await RefreshIcon(project, cancellationToken);
        }

        return probe;
    }

    /// <summary>
    /// The app's own icon, on the first round, after each deployment and once a day (See ProjectIcons). A round that
    /// can't read it is still a round, so a failure here is only logged.
    /// </summary>
    private async Task RefreshIcon(Project project, CancellationToken cancellationToken)
    {
        try
        {
            await icons.Refresh(project, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            logger.LogWarning(exp, "Reading the icon of project {ProjectId} failed.", project.Id);
        }
    }

    /// <summary>
    /// A new build seen in the health check gets its scans when the owner allows it. A round that can't ask for them is
    /// still a round, so a failure here is only logged.
    /// </summary>
    private async Task ScanTheDeployment(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        try
        {
            await scanScheduler.AfterDeployment(project, probe.ApplicationVersion, cancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            logger.LogError(exp, "Asking for the scans of project {ProjectId}'s deployment of {ApplicationVersion} failed.", project.Id, probe.ApplicationVersion);
        }
    }

    /// <summary>Adds the round to its 15 minutes, a row per part (See ProjectRollup and ProjectRollupWriter).</summary>
    private async Task RollUp(ProjectProbe probe, CancellationToken cancellationToken)
    {
        List<ProjectRollupPart> parts =
        [
            // A visitor's wait counts even when the report can't be read.
            HomePagePart(probe)
        ];

        if (probe.VisitorMs is { } visitorMs)
        {
            parts.Add(Part(probe, ProjectRollup.HomePageForVisitorsKey, false, false, visitorMs));
        }

        // A refused token reads nothing, so it is no evidence either way.
        if (probe.Outcome is not ProjectProbeOutcome.Unauthorized)
        {
            var read = probe.Outcome is ProjectProbeOutcome.Read;
            parts.Add(Part(probe, ProjectRollup.ReportKey, read is false || probe.Status is HealthCheckStatus.Unhealthy, probe.Status is HealthCheckStatus.Degraded, read ? probe.ReportMs : null));
        }

        // A reused result's time was counted with the round that saw it run.
        parts.AddRange(probe.Checks.Where(check => check.IsNotConfigured is false)
                                   .Select(check => Part(probe, check.Key!, check.Status is HealthCheckStatus.Unhealthy, check.Status is HealthCheckStatus.Degraded, check.IsReused ? null : check.DurationMs)));

        await ProjectRollupWriter.Add(dbContext, parts, cancellationToken);
    }

    /// <summary>
    /// What a visitor waits for, from where the round ran: bit Fabric's own rounds and the bitplatform Network's readings
    /// count it alike, so every region's home page row adds up the same way.
    /// </summary>
    public static ProjectRollupPart HomePagePart(ProjectProbe probe) => Part(probe, ProjectRollup.HomePageKey, probe.ResponseMs is null, false, probe.ResponseMs);

    private static ProjectRollupPart Part(ProjectProbe probe, string key, bool failed, bool degraded, int? ms)
        => new(probe.ProjectId, probe.TenantId, probe.Region!, probe.At, key, failed, degraded, ms);

    /// <summary>
    /// How long a visitor waits for the home page, asked for with a query string no one else uses, so any cache has to
    /// miss; an answer that still says it came from one means that cache ignores the query string. A static front end
    /// says nothing about the app, so its server's <c>/health</c> is timed instead. One request is timed: the page the front
    /// door lands on (See ProjectLandings). A round that is redirected within the app, as the template sends <c>/</c> to
    /// <c>/en-US/</c>, follows it and times only the last request, and later rounds ask for that page straight away.
    /// </summary>
    private async Task TimeFrontDoor(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var uri = new Uri(landings.AddressOf(project));

        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);

            for (var redirects = 0; ; redirects++)
            {
                var asked = project.HasStaticFrontEnd ? uri : PastAnyCache(uri);
                var stopwatch = Stopwatch.StartNew();
                using var response = await http.GetAsync(asked, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                var elapsed = (int)stopwatch.ElapsedMilliseconds;

                if (redirects < MonitoringHttp.MaxRedirects && MonitoringHttp.SameOriginRedirect(asked, response) is { } next)
                {
                    uri = next;
                    continue;
                }

                probe.ResponseMs = elapsed;
                probe.HasCacheWarning = project.HasStaticFrontEnd is false
                    && (response.Headers.Age is not null
                        || IsHit(response, "cf-cache-status")
                        || IsHit(response, "X-Cache"));

                landings.Learn(project, uri);
                return;
            }
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException)
        {
            // The report says what's wrong; a front door that doesn't answer leaves the time empty.
            logger.LogInformation("{Url} didn't answer: {Reason}", uri, exp.Message);
        }
    }

    /// <summary>
    /// What a visitor waits for, through whatever cache is in front of the app, such as Cloudflare's: the same page asked
    /// for as a visitor asks for it, without the query string that gets past the cache. Only an answer a cache gave counts,
    /// since otherwise it is the server's time again, so the pages show one number (bitfabric-internal/specs/monitoring.md,
    /// Response time). A static front end's server's <c>/health</c> has no cache to look at.
    /// </summary>
    private async Task TimeForVisitors(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        if (project.HasStaticFrontEnd || probe.ResponseMs is null)
            return;

        var url = landings.AddressOf(project);

        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
            var stopwatch = Stopwatch.StartNew();
            using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var elapsed = (int)stopwatch.ElapsedMilliseconds;

            if (response.IsSuccessStatusCode && (IsHit(response, "cf-cache-status") || IsHit(response, "X-Cache") || response.Headers.Age is not null))
            {
                probe.VisitorMs = elapsed;
            }
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException)
        {
            // The server's time already says whether the page answers.
            logger.LogInformation("{Url} didn't answer as a visitor asks for it: {Reason}", url, exp.Message);
        }
    }

    /// <summary>The address with a <c>bf</c> query no one else uses, a new one for every request, so any cache that honors the query string misses.</summary>
    private static Uri PastAnyCache(Uri uri)
    {
        var query = QueryHelpers.ParseQuery(uri.Query);
        query["bf"] = Guid.NewGuid().ToString("N");

        return new Uri(QueryHelpers.AddQueryString(uri.GetLeftPart(UriPartial.Path), query));
    }

    /// <summary>
    /// What a visitor's wait is timed on, from here and from the bitplatform Network alike: the home page, which each asks
    /// for with a query string of its own, or a static front end's server's <c>/health</c>, as is.
    /// </summary>
    public static string FrontDoorOf(Project project) => project.HasStaticFrontEnd ? $"{project.ServerAddress}/health" : $"{project.Address}/";

    private static bool IsHit(HttpResponseMessage response, string header)
        => response.Headers.TryGetValues(header, out var values) && values.Any(value => value.Contains("HIT", StringComparison.OrdinalIgnoreCase));

    private async Task ReadReport(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var connection = project.Connection;
        if (connection is null)
        {
            probe.Outcome = ProjectProbeOutcome.Unauthorized;
            probe.Problem = ProjectConnectionProblems.Of(ProjectConnectionProblems.NoApproval);
            return;
        }

        var (accessToken, problem) = await oauthClient.GetAccessToken(connection, connection.ClientId!, forceRefresh: false, cancellationToken);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (accessToken is null)
            {
                probe.Outcome = ProjectProbeOutcome.Unauthorized;
                probe.Problem = problem;
                return;
            }

            var stopwatch = Stopwatch.StartNew();

            try
            {
                var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
                // The resource the token was issued for, so the request always matches its audience.
                using var request = new HttpRequestMessage(HttpMethod.Get, $"{connection.Resource!.TrimEnd('/')}/v1");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
                // A request built by hand starts at HTTP/1.1; this one goes as the home page's does.
                (request.Version, request.VersionPolicy) = (http.DefaultRequestVersion, http.DefaultVersionPolicy);

                using var response = await http.SendAsync(request, cancellationToken);
                probe.ReportMs = (int)stopwatch.ElapsedMilliseconds;

                if (response.StatusCode is HttpStatusCode.Unauthorized && attempt is 0)
                {
                    // Revoked early, or its clock and ours disagree: one refresh before giving up on it.
                    (accessToken, problem) = await oauthClient.GetAccessToken(connection, connection.ClientId!, forceRefresh: true, cancellationToken);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Forbidden)
                {
                    // The token is good, the feature behind /healthz is gone from whoever approved it.
                    probe.Outcome = ProjectProbeOutcome.Unauthorized;
                    probe.Problem = ProjectConnectionProblems.Of(ProjectConnectionProblems.ApproverLostAccess);
                    return;
                }

                if (response.StatusCode is HttpStatusCode.Unauthorized)
                {
                    probe.Outcome = ProjectProbeOutcome.Unauthorized;
                    probe.Problem = ProjectConnectionProblems.Of(ProjectConnectionProblems.Refused);
                    return;
                }

                // An unhealthy report may come with 503; its body is the report all the same.
                var report = response.Content.Headers.ContentType?.MediaType is "application/json"
                    ? await response.Content.ReadFromJsonAsync(AppJsonContext.Default.HealthReportDto, cancellationToken)
                    : null;

                if (report is null)
                {
                    probe.Outcome = ProjectProbeOutcome.BadResponse;
                    probe.Problem = $"Your app's server answered HTTP {(int)response.StatusCode} without a health report.";
                    return;
                }

                Fill(probe, report);
                return;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested is false)
            {
                probe.Outcome = ProjectProbeOutcome.Unreachable;
                probe.Problem = $"Your app's server didn't answer within {(int)settings.Monitoring.RequestTimeout.TotalSeconds} seconds.";
                return;
            }
            catch (HttpRequestException exp)
            {
                probe.Outcome = ProjectProbeOutcome.Unreachable;
                probe.Problem = exp.HttpRequestError switch
                {
                    HttpRequestError.NameResolutionError => "Your app's server address doesn't resolve.",
                    HttpRequestError.SecureConnectionError => "Your app's server has a certificate problem.",
                    _ => "Your app's server didn't answer."
                };
                return;
            }
            catch (JsonException)
            {
                probe.Outcome = ProjectProbeOutcome.BadResponse;
                probe.Problem = "Your app's server answered with something that isn't a health report.";
                return;
            }
        }
    }

    /// <summary>
    /// A result the app measured longer ago than this, by its own clock, was seen by an earlier round already. A round a
    /// minute, so a run is counted once even when another caller of <c>/healthz</c> started it between two rounds.
    /// </summary>
    private static readonly TimeSpan roundInterval = TimeSpan.FromMinutes(1);

    private static void Fill(ProjectProbe probe, HealthReportDto report)
    {
        probe.Outcome = ProjectProbeOutcome.Read;
        probe.ApplicationVersion = Truncate(report.ApplicationVersion, 200);
        probe.InstanceName = Truncate(report.InstanceName, 200);
        probe.StartedAt = report.StartedAt;
        probe.Checks = [.. report.Entries.Select(entry => new ProjectProbeCheck
        {
            Key = Truncate(entry.Key, 100),
            Status = entry.Value.Status,
            Description = Truncate(entry.Value.Description, 1000),
            DurationMs = (int)entry.Value.Duration.TotalMilliseconds,
            TimeoutMs = entry.Value.Timeout is { } timeout ? (int)timeout.TotalMilliseconds : null,
            Category = Truncate(entry.Value.Category, 50),
            Tags = entry.Value.Tags,
            // An empty title or icon is none, so the part is named by its key and drawn with its category's icon.
            Title = Truncate(NullIfBlank(entry.Value.Title), 100),
            IconName = Truncate(NullIfBlank(entry.Value.IconName), 50),
            About = Truncate(entry.Value.About, 300),
            // What lies behind the part, as the app names it; nothing when it doesn't.
            Provider = Truncate(NullIfBlank(entry.Value.Provider), 100),
            Host = Truncate(NullIfBlank(entry.Value.Host), 255),
            Model = Truncate(NullIfBlank(entry.Value.Model), 100),
            IsNotConfigured = entry.Value.IsNotConfigured,
            VerifiedAt = entry.Value.VerifiedAt,
            // Both times by the app's clock, so ours being off doesn't matter.
            IsReused = entry.Value.VerifiedAt is { } verifiedAt && report.CheckedAt - verifiedAt >= roundInterval
        })];

        // What the report's status would be without the checks that only miss their settings.
        probe.Status = probe.Checks.Where(check => check.IsNotConfigured is false)
                                   .Select(check => check.Status)
                                   .DefaultIfEmpty(HealthCheckStatus.Healthy)
                                   .Min();
    }

    private static string? Truncate(string? value, int length) => value is null || value.Length <= length ? value : value[..length];

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Closes the period of each check whose status changed and opens the next; a check gone from the report closes too.</summary>
    private async Task MovePeriods(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var open = await dbContext.ProjectCheckPeriods.IgnoreQueryFilters()
                                                      .Where(period => period.ProjectId == project.Id && period.EndedAt == null)
                                                      .ToListAsync(cancellationToken);

        // A check that only misses its settings is no outage: it has no period, and its open one closes as if it were gone.
        var configured = probe.Checks.Where(check => check.IsNotConfigured is false).ToArray();

        foreach (var check in configured)
        {
            var current = open.FirstOrDefault(period => period.CheckKey == check.Key);

            if (current?.Status == check.Status)
                continue;

            if (current is not null)
            {
                current.EndedAt = probe.At;
            }

            await dbContext.ProjectCheckPeriods.AddAsync(new()
            {
                Id = Guid.CreateSequentialGuid(),
                ProjectId = project.Id,
                TenantId = project.TenantId,
                CheckKey = check.Key,
                Title = check.Title,
                Status = check.Status,
                StartedAt = probe.At,
                Description = check.Description
            }, cancellationToken);
        }

        foreach (var gone in open.Where(period => configured.Any(check => check.Key == period.CheckKey) is false))
        {
            gone.EndedAt = probe.At;
        }
    }

    /// <summary>
    /// A start time later than the one this instance reported last is a start of its process: a deployment when the build
    /// changed, a restart when it didn't. A new instance on a new build is a deployment too; on the same build it's only
    /// the app scaling out.
    /// </summary>
    /// <returns>Whether it was a deployment, recorded for the first time.</returns>
    private async Task<bool> RecordStart(Project project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        if (probe.StartedAt is not { } startedAt || probe.ApplicationVersion is null)
            return false; // A round that read no report.

        var previous = await dbContext.ProjectProbes.IgnoreQueryFilters()
            .Where(p => p.ProjectId == project.Id && p.StartedAt != null && p.ApplicationVersion != null)
            .OrderByDescending(p => p.At)
            .Select(p => new { p.InstanceName, p.StartedAt, p.ApplicationVersion })
            .Take(20)
            .ToListAsync(cancellationToken);

        if (previous.Count is 0)
            return false; // The first round knows nothing of what came before it.

        var sameInstance = previous.FirstOrDefault(p => p.InstanceName == probe.InstanceName);
        bool isDeploy;

        if (sameInstance is null)
        {
            if (previous[0].ApplicationVersion == probe.ApplicationVersion)
                return false;

            isDeploy = true;
        }
        else
        {
            if (startedAt <= sameInstance.StartedAt!.Value.AddSeconds(1))
                return false;

            isDeploy = sameInstance.ApplicationVersion != probe.ApplicationVersion;
        }

        if (isDeploy && await dbContext.ProjectStarts.IgnoreQueryFilters().AnyAsync(start => start.ProjectId == project.Id
                && start.IsDeploy && start.ApplicationVersion == probe.ApplicationVersion
                && start.At >= startedAt - sameDeploymentWindow, cancellationToken))
            return false;

        await dbContext.ProjectStarts.AddAsync(new()
        {
            Id = Guid.CreateSequentialGuid(),
            ProjectId = project.Id,
            TenantId = project.TenantId,
            At = startedAt,
            ApplicationVersion = probe.ApplicationVersion,
            InstanceName = probe.InstanceName,
            IsDeploy = isDeploy
        }, cancellationToken);

        // A deployment can be an upgrade of the template, so the version its metadata names is read again.
        // A failed read keeps the one known.
        if (isDeploy && await addressProbe.ReadResourceMetadata(project.ServerAddress!, cancellationToken) is { BitTemplateVersion: { Length: > 0 } templateVersion })
        {
            project.TemplateVersion = templateVersion;
        }

        return isDeploy;
    }

    /// <summary>
    /// The project's state from one round. Unhealthy is what the template's critical checks report, which takes the app out
    /// of rotation, so it means down; the others fail as Degraded.
    /// </summary>
    public static ProjectHealth HealthOf(ProjectProbe probe) => probe.Outcome switch
    {
        ProjectProbeOutcome.Read => probe.Status switch
        {
            HealthCheckStatus.Unhealthy => ProjectHealth.Down,
            HealthCheckStatus.Degraded => ProjectHealth.Degraded,
            _ => ProjectHealth.Operational
        },
        ProjectProbeOutcome.Unauthorized => ProjectHealth.NoData,
        _ => ProjectHealth.Down
    };
}
