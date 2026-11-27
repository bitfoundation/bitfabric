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

    /// <summary>How long a problem that may pass, such as a server error while renewing, lasts before the team hears of it.</summary>
    public static readonly TimeSpan TemporaryProblemGrace = TimeSpan.FromHours(1);

    /// <summary>While an environment needs reconnecting, how often its round tries the token again.</summary>
    public static readonly TimeSpan TokenRetryInterval = TimeSpan.FromMinutes(15);

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
    [AutoInject] private Projects.ProjectLinker linker = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private DistributedLockFactory distributedLockFactory = default!;
    [AutoInject] private ILogger<ProjectProber> logger = default!;

    /// <summary>
    /// A round of the project, after the one on its way, if any. A round reads the project, waits seconds for its app's
    /// answers and then saves it, so two at once, as the first round CompleteConnect runs and the job's, would fail the later
    /// save on the project's Version. This one reads the project once the other is saved.
    /// </summary>
    public async Task<ProjectProbe> Probe(Guid monitoringId, CancellationToken cancellationToken)
    {
        await using (await RoundLock(monitoringId).AcquireAsync(roundWait, cancellationToken))
        {
            return await Round(monitoringId, cancellationToken);
        }
    }

    /// <summary>The job's round of the project every minute, unless one is on its way, which is this minute's.</summary>
    public async Task<ProjectProbe?> ProbeUnlessUnderway(Guid monitoringId, CancellationToken cancellationToken)
    {
        await using var round = await RoundLock(monitoringId).TryAcquireAsync(TimeSpan.Zero, cancellationToken);

        return round is null ? null : await Round(monitoringId, cancellationToken);
    }

    private IDistributedLock RoundLock(Guid monitoringId) => distributedLockFactory($"bit-fabric:monitoring-probe:{monitoringId}");

    private async Task<ProjectProbe> Round(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = await dbContext.Monitorings.IgnoreQueryFilters()
                                              .Include(p => p.Connection)
                                              .FirstAsync(p => p.Id == monitoringId, cancellationToken);

        var probe = new ProjectProbe
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
            TenantId = project.TenantId,
            At = timeProvider.GetUtcNow(),
            Region = settings.Monitoring.Region
        };

        await TimeFrontDoor(project, probe, cancellationToken);
        await TimeForVisitors(project, probe, cancellationToken);
        var problem = await ReadReport(project, probe, cancellationToken);

        if (problem is not null)
        {
            // The token can't read the report, so the round falls back on what needs none.
            await ReadPublicHealth(project, probe, cancellationToken);
        }

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

        var connectionLost = TrackConnection(project, probe, problem);

        await dbContext.SaveChangesAsync(cancellationToken);

        // The project the app says it is, once its report names one (bitfabric-internal/specs/monitoring.md, The approval
        // links an app to a project).
        if (probe.Outcome is ProjectProbeOutcome.Read && project.ReportedProjectId is { } reportedProjectId)
        {
            await linker.Link(project, reportedProjectId, cancellationToken);
        }

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
    private async Task RefreshIcon(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        try
        {
            await icons.Refresh(project, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            logger.LogWarning(exp, "Reading the icon of project {MonitoringId} failed.", project.Id);
        }
    }

    /// <summary>
    /// A new build seen in the health check gets its scans when the owner allows it. A round that can't ask for them is
    /// still a round, so a failure here is only logged.
    /// </summary>
    private async Task ScanTheDeployment(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        try
        {
            await scanScheduler.AfterDeployment(project, probe.ApplicationVersion, cancellationToken);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            logger.LogError(exp, "Asking for the scans of project {MonitoringId}'s deployment of {ApplicationVersion} failed.", project.Id, probe.ApplicationVersion);
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

        // A round whose token was refused, from before the public /health took the report's place, read nothing: no
        // evidence either way. /health counts as the report does, except that it can't tell a degraded part from one that
        // only misses its settings.
        if (probe.Outcome is not ProjectProbeOutcome.Unauthorized)
        {
            var answered = probe.Outcome is ProjectProbeOutcome.Read or ProjectProbeOutcome.PublicHealth or ProjectProbeOutcome.Reached;
            var timed = probe.Outcome is ProjectProbeOutcome.Read or ProjectProbeOutcome.PublicHealth;
            parts.Add(Part(probe, ProjectRollup.ReportKey, answered is false || probe.Status is HealthCheckStatus.Unhealthy,
                           probe.Outcome is ProjectProbeOutcome.Read && probe.Status is HealthCheckStatus.Degraded, timed ? probe.ReportMs : null));
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
        => new(probe.MonitoringId, probe.TenantId, probe.Region!, probe.At, key, failed, degraded, ms);

    /// <summary>
    /// How long a visitor waits for the home page, asked for with a query string no one else uses, so any cache has to
    /// miss; an answer that still says it came from one means that cache ignores the query string. A static front end
    /// says nothing about the app, so its server's <c>/health</c> is timed instead. One request is timed: the page the front
    /// door lands on (See ProjectLandings). A round that is redirected within the app, as the template sends <c>/</c> to
    /// <c>/en-US/</c>, follows it and times only the last request, and later rounds ask for that page straight away.
    /// </summary>
    private async Task TimeFrontDoor(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
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
    private async Task TimeForVisitors(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
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
    public static string FrontDoorOf(ProjectMonitoring project) => project.HasStaticFrontEnd ? $"{project.ServerAddress}/health" : $"{project.Address}/";

    private static bool IsHit(HttpResponseMessage response, string header)
        => response.Headers.TryGetValues(header, out var values) && values.Any(value => value.Contains("HIT", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Why the token couldn't read the report, as a ProjectConnectionProblems code (See TrackConnection).
    /// </summary>
    /// <param name="IsTemporary">It may pass, so it is told only once it has lasted <see cref="TemporaryProblemGrace"/>.</param>
    /// <param name="IsWaiting">The environment needs reconnecting and its next try of the token isn't due yet.</param>
    private sealed record ReportProblem(string? Code, bool IsTemporary, bool IsWaiting = false);

    /// <summary>
    /// Moves the environment's connection by the round (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching
    /// when the approval breaks): a report read connects it again, with no click; a refusal needs reconnecting at once, and a
    /// problem that may pass once it has lasted an hour; while it needs reconnecting, the token is tried again every 15
    /// minutes. One the team disconnected themselves stays so. True when the connection was lost in this round, which the
    /// team hears once.
    /// </summary>
    private static bool TrackConnection(ProjectMonitoring project, ProjectProbe probe, ReportProblem? problem)
    {
        if (project.ConnectionStatus is ProjectConnectionStatus.Disconnected || problem?.IsWaiting is true)
            return false;

        var connection = project.Connection;

        if (problem is null)
        {
            // Read with the token. An app that didn't answer says nothing of the approval.
            if (probe.Outcome is ProjectProbeOutcome.Read)
            {
                if (connection is not null)
                {
                    connection.ReportFailingSince = null;
                    connection.NextTokenAttemptAt = null;
                }

                if (project.ConnectionStatus is ProjectConnectionStatus.FirstChecks or ProjectConnectionStatus.ReconnectNeeded)
                {
                    project.ConnectionStatus = ProjectConnectionStatus.Connected;
                    project.ConnectionProblem = null;
                }
            }

            return false;
        }

        if (problem.IsTemporary && connection is not null)
        {
            connection.ReportFailingSince ??= probe.At;

            if (probe.At - connection.ReportFailingSince < TemporaryProblemGrace)
            {
                if (project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded)
                {
                    connection.NextTokenAttemptAt = probe.At + TokenRetryInterval;
                }

                return false;
            }
        }

        var lost = project.ConnectionStatus is not ProjectConnectionStatus.ReconnectNeeded;

        project.ConnectionStatus = ProjectConnectionStatus.ReconnectNeeded;
        project.ConnectionProblem = problem.Code;

        if (connection is not null)
        {
            connection.NextTokenAttemptAt = probe.At + TokenRetryInterval;
        }

        return lost;
    }

    /// <summary>
    /// The base that needs no token, for a round whose report the token couldn't read: the server's public <c>/health</c>,
    /// which the template answers 200 with Healthy or Degraded and 503 with Unhealthy. An app without one is up as long
    /// as its front door answered (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching when the approval
    /// breaks).
    /// </summary>
    private async Task ReadPublicHealth(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var url = $"{(project.ServerAddress ?? project.Address)!.TrimEnd('/')}/health";

        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
            var stopwatch = Stopwatch.StartNew();
            using var response = await http.GetAsync(url, cancellationToken);
            var elapsed = (int)stopwatch.ElapsedMilliseconds;
            var said = (await response.Content.ReadAsStringAsync(cancellationToken)).Trim();

            HealthCheckStatus? status = response.StatusCode switch
            {
                HttpStatusCode.ServiceUnavailable => HealthCheckStatus.Unhealthy,
                HttpStatusCode.OK when Enum.TryParse<HealthCheckStatus>(said, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) => parsed,
                _ => null
            };

            if (status is { } known)
            {
                probe.Outcome = ProjectProbeOutcome.PublicHealth;
                probe.Status = known;
                probe.ReportMs = elapsed;
                return;
            }
        }
        catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested is false)
        {
            // As one without /health: the front door says whether it answers.
        }
        catch (HttpRequestException)
        {
            // As above.
        }

        probe.Outcome = probe.ResponseMs is null ? ProjectProbeOutcome.Unreachable : ProjectProbeOutcome.Reached;
        probe.Problem = probe.ResponseMs is null ? "Your app's server didn't answer." : null;
    }

    /// <summary>
    /// Reads the health report with the project's token. Returns why the token couldn't read it, or null when it did, or
    /// when the app failed to answer at all, which <paramref name="probe"/> then says. While the environment needs
    /// reconnecting, the token is tried every <see cref="TokenRetryInterval"/>.
    /// </summary>
    private async Task<ReportProblem?> ReadReport(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var connection = project.Connection;
        if (connection is null)
            return new(ProjectConnectionProblems.Of(ProjectConnectionProblems.NoApproval), IsTemporary: false);

        if (project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded && connection.NextTokenAttemptAt > probe.At)
            return new(project.ConnectionProblem, IsTemporary: false, IsWaiting: true);

        var token = await oauthClient.GetAccessToken(connection, connection.ClientId!, forceRefresh: false, cancellationToken);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            if (token.AccessToken is not { } accessToken)
                return new(token.Problem, token.IsTemporary);

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
                    token = await oauthClient.GetAccessToken(connection, connection.ClientId!, forceRefresh: true, cancellationToken);
                    continue;
                }

                if (response.StatusCode is HttpStatusCode.Forbidden)
                    // The token is good, the feature behind /healthz is gone from whoever approved it.
                    return new(ProjectConnectionProblems.Of(ProjectConnectionProblems.ApproverLostAccess), IsTemporary: false);

                if (response.StatusCode is HttpStatusCode.Unauthorized)
                    return new(ProjectConnectionProblems.Of(ProjectConnectionProblems.Refused), IsTemporary: false);

                if (response.StatusCode is HttpStatusCode.NotFound)
                    // No report there: its code changed, or a deployment is on its way.
                    return new(ProjectConnectionProblems.Of(ProjectConnectionProblems.ReportGone), IsTemporary: true);

                // An unhealthy report may come with 503; its body is the report all the same.
                var report = response.Content.Headers.ContentType?.MediaType is "application/json"
                    ? await response.Content.ReadFromJsonAsync(AppJsonContext.Default.HealthReportDto, cancellationToken)
                    : null;

                if (report is null)
                {
                    probe.Outcome = ProjectProbeOutcome.BadResponse;
                    probe.Problem = $"Your app's server answered HTTP {(int)response.StatusCode} without a health report.";
                    return null;
                }

                Fill(probe, report);
                project.ReportedProjectId = report.ProjectId is { } reported && reported != Guid.Empty ? reported : null;
                return null;
            }
            catch (TaskCanceledException) when (cancellationToken.IsCancellationRequested is false)
            {
                probe.Outcome = ProjectProbeOutcome.Unreachable;
                probe.Problem = $"Your app's server didn't answer within {(int)settings.Monitoring.RequestTimeout.TotalSeconds} seconds.";
                return null;
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
                return null;
            }
            catch (JsonException)
            {
                probe.Outcome = ProjectProbeOutcome.BadResponse;
                probe.Problem = "Your app's server answered with something that isn't a health report.";
                return null;
            }
        }

        return null;
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
    private async Task MovePeriods(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        var open = await dbContext.ProjectCheckPeriods.IgnoreQueryFilters()
                                                      .Where(period => period.MonitoringId == project.Id && period.EndedAt == null)
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
                MonitoringId = project.Id,
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
    private async Task<bool> RecordStart(ProjectMonitoring project, ProjectProbe probe, CancellationToken cancellationToken)
    {
        if (probe.StartedAt is not { } startedAt || probe.ApplicationVersion is null)
            return false; // A round that read no report.

        var previous = await dbContext.ProjectProbes.IgnoreQueryFilters()
            .Where(p => p.MonitoringId == project.Id && p.StartedAt != null && p.ApplicationVersion != null)
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

        if (isDeploy && await dbContext.ProjectStarts.IgnoreQueryFilters().AnyAsync(start => start.MonitoringId == project.Id
                && start.IsDeploy && start.ApplicationVersion == probe.ApplicationVersion
                && start.At >= startedAt - sameDeploymentWindow, cancellationToken))
            return false;

        await dbContext.ProjectStarts.AddAsync(new()
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
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
        // The public /health: Degraded there may only be a part missing its settings, which the report would leave out.
        ProjectProbeOutcome.PublicHealth => probe.Status is HealthCheckStatus.Unhealthy ? ProjectHealth.Down : ProjectHealth.Operational,
        ProjectProbeOutcome.Reached => ProjectHealth.Operational,
        ProjectProbeOutcome.Unauthorized => ProjectHealth.NoData,
        _ => ProjectHealth.Down
    };
}
