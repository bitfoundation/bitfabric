using System.Security.Cryptography;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// The current tenant's monitored projects (bitfabric-internal/specs/monitoring.md). A selected tenant is required: without
/// one a write would land in the fallback tenant every tenant-less user shares.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class MonitoringsController : AppControllerBase, IMonitoringsController
{
    /// <summary>Far more than a team watches; it stops a script from making this server check thousands of addresses.</summary>
    private const int MaxProjectsPerTenant = 100;

    private static readonly TimeSpan pendingConnectionLifetime = TimeSpan.FromMinutes(15);

    [AutoInject] private ProjectAddressProbe addressProbe = default!;
    [AutoInject] private MonitoringOAuthClient oauthClient = default!;
    [AutoInject] private ProjectProber prober = default!;
    [AutoInject] private ProjectScanScheduler scanScheduler = default!;
    [AutoInject] private IFusionCache cache = default!;
    [AutoInject] private MonitoringsService monitoringsService = default!;
    [AutoInject] private ProjectHistoryExport historyExport = default!;
    [AutoInject] private Projects.ProjectsService projectsService = default!;

    /// <summary>Read the same way by the AI tools (See MonitoringsService).</summary>
    [HttpGet]
    public Task<List<MonitoringDto>> GetMonitorings(string? timeZone, CancellationToken cancellationToken)
        => monitoringsService.GetMonitorings(ZoneOf(timeZone), cancellationToken);

    /// <summary>The viewer's zone, by an IANA or a Windows id; UTC for one this server doesn't know.</summary>
    internal static TimeZoneInfo ZoneOf(string? timeZone)
        => string.IsNullOrWhiteSpace(timeZone) is false && TimeZoneInfo.TryFindSystemTimeZoneById(timeZone, out var zone) ? zone : TimeZoneInfo.Utc;

    [HttpGet("{id}")]
    public Task<MonitoringDetailsDto> GetMonitoring(Guid id, CancellationToken cancellationToken)
        => monitoringsService.GetMonitoring(id, cancellationToken);

    /// <summary>Read the same way by the AI tools (See MonitoringsService.GetMap).</summary>
    [HttpGet("{id}")]
    public Task<ProjectMapDto> GetMap(Guid id, CancellationToken cancellationToken)
        => monitoringsService.GetMap(id, cancellationToken);

    /// <summary>The widths a bar may have, in minutes: a round, then the rollup and its multiples.</summary>
    private static readonly int[] performanceSteps = [1, ProjectRollup.BucketMinutes, 60, 360, 1440];

    /// <summary>More bars than this in a row would be narrower than a pixel; the step grows instead.</summary>
    private const int MaxPointsPerSeries = 1500;

    [HttpPost]
    public async Task<ProjectPerformanceDto> GetPerformance(ProjectPerformanceRequestDto request, CancellationToken cancellationToken)
    {
        var id = request.MonitoringId;

        if (await DbContext.Monitorings.AnyAsync(project => project.Id == id, cancellationToken) is false)
            throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        var from = request.From.ToUniversalTime();
        var to = request.To.ToUniversalTime();

        if (to <= from || to - from > TimeSpan.FromDays(400))
            throw new BadRequestException();

        var step = performanceSteps.FirstOrDefault(step => step >= request.StepMinutes && (to - from).TotalMinutes / step <= MaxPointsPerSeries, performanceSteps[^1]);

        List<PerformancePoint> points = [];

        if (step is 1)
        {
            // Every round, a bar a minute; a reconnect's round and the job's in the same minute show as the later one.
            var rounds = await DbContext.ProjectProbes.AsNoTracking()
                .Where(probe => probe.MonitoringId == id && probe.At >= from && probe.At < to)
                .OrderBy(probe => probe.At)
                .ToListAsync(cancellationToken);

            foreach (var round in rounds.GroupBy(probe => (probe.Region, Minute: Floor(probe.At, 1))).Select(group => group.Last()))
            {
                var minute = Floor(round.At, 1);
                points.Add(PerformancePoint.Of(ProjectRollup.HomePageKey, round.Region!, minute, round.ResponseMs is null, false, round.ResponseMs));

                // Through the cache in front of the app, in the rounds a cache answered (See ProjectProbe.VisitorMs).
                if (round.VisitorMs is { } visitorMs)
                {
                    points.Add(PerformancePoint.Of(ProjectRollup.HomePageForVisitorsKey, round.Region!, minute, false, false, visitorMs));
                }

                // The bitplatform Network times the front door alone (See ProjectProbe.Colo).
                if (round.Outcome is not ProjectProbeOutcome.Unauthorized && round.Colo is null)
                {
                    var read = round.Outcome is ProjectProbeOutcome.Read;
                    points.Add(PerformancePoint.Of(ProjectRollup.ReportKey, round.Region!, minute, read is false || round.Status is HealthCheckStatus.Unhealthy, round.Status is HealthCheckStatus.Degraded, read ? round.ReportMs : null));
                }

                // A reused result shows its status but no new time (See ProjectProbeCheck.IsReused).
                points.AddRange(round.Checks.Where(check => check.IsNotConfigured is false)
                                            .Select(check => PerformancePoint.Of(check.Key!, round.Region!, minute, check.Status is HealthCheckStatus.Unhealthy, check.Status is HealthCheckStatus.Degraded, check.IsReused ? null : check.DurationMs)));
            }
        }
        else
        {
            var fromBucket = ProjectRollup.BucketOf(from);
            var rollups = await DbContext.ProjectRollups.AsNoTracking()
                .Where(rollup => rollup.MonitoringId == id && rollup.BucketStart >= fromBucket && rollup.BucketStart < to)
                .ToListAsync(cancellationToken);

            points.AddRange(rollups.GroupBy(rollup => (Key: rollup.CheckKey!, Region: rollup.Region!, Start: Floor(rollup.BucketStart, step)))
                                   .Select(group => new PerformancePoint(group.Key.Key, group.Key.Region, group.Key.Start, group.Sum(r => r.Count), group.Sum(r => r.FailedCount),
                                                                         group.Sum(r => r.DegradedCount), group.Sum(r => r.TimedCount), group.Sum(r => r.TotalMs), group.Max(r => r.MaxMs))));
        }

        var starts = await DbContext.ProjectStarts
            .Where(start => start.MonitoringId == id && start.At >= from && start.At < to)
            .OrderBy(start => start.At)
            .Select(start => new ProjectStartDto { At = start.At, IsDeploy = start.IsDeploy, ApplicationVersion = start.ApplicationVersion })
            .ToArrayAsync(cancellationToken);

        return new()
        {
            StepMinutes = step,
            Series = [.. points.GroupBy(point => (point.Key, point.Region)).Select(series => new ProjectPerformanceSeriesDto
            {
                Key = series.Key.Key,
                Region = series.Key.Region,
                Points = [.. series.OrderBy(point => point.Start).Select(point => new ProjectPerformancePointDto
                {
                    Start = point.Start,
                    Count = point.Count,
                    Failed = point.Failed,
                    Degraded = point.Degraded,
                    AverageMs = point.Timed > 0 ? (int)(point.TotalMs / point.Timed) : null,
                    MaxMs = point.Timed > 0 ? point.MaxMs : null
                })]
            })],
            Starts = starts
        };
    }

    /// <summary>The start of the step a time falls in, in UTC; steps divide a day, so they line up with the rollups.</summary>
    private static DateTimeOffset Floor(DateTimeOffset at, int stepMinutes)
        => new(at.UtcTicks - at.UtcTicks % TimeSpan.FromMinutes(stepMinutes).Ticks, TimeSpan.Zero);

    private sealed record PerformancePoint(string Key, string Region, DateTimeOffset Start, int Count, int Failed, int Degraded, int Timed, long TotalMs, int MaxMs)
    {
        public static PerformancePoint Of(string key, string region, DateTimeOffset start, bool failed, bool degraded, int? ms)
            => new(key, region, start, 1, failed ? 1 : 0, degraded ? 1 : 0, ms is null ? 0 : 1, ms ?? 0, ms ?? 0);
    }

    [HttpPost]
    public async Task<AddressProbeResultDto> ProbeAddress(ProbeAddressRequestDto request, CancellationToken cancellationToken)
    {
        var result = await addressProbe.Probe(request.Address, cancellationToken);

        if (result.Address is not null)
        {
            result.IsAlreadyAdded = await DbContext.Monitorings.AnyAsync(project => project.Address == result.Address, cancellationToken);
        }

        return result;
    }

    [HttpPost]
    public async Task<StartConnectResponseDto> StartConnect(StartConnectRequestDto request, CancellationToken cancellationToken)
    {
        var found = await addressProbe.Probe(request.Address, cancellationToken);

        if (found.Outcome is not AddressProbeOutcome.Found)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringHealthReportNotFoundAtAddress)]);

        var existing = await DbContext.Monitorings.AsNoTracking().FirstOrDefaultAsync(project => project.Address == found.Address, cancellationToken);

        // A new environment of a project of the team's own, by its page's "Add your server".
        if (request.ProjectId is { } projectId && await DbContext.Projects.AnyAsync(project => project.Id == projectId, cancellationToken) is false)
            throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        var environment = string.IsNullOrWhiteSpace(request.Environment) ? ProjectMonitoring.Production : request.Environment.Trim();

        if (existing is null && request.ProjectId is { } joining
            && await DbContext.Monitorings.AnyAsync(monitoring => monitoring.ProjectId == joining && monitoring.Environment!.ToLower() == environment.ToLower(), cancellationToken))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringEnvironmentTaken), environment]);

        if (existing is null && await DbContext.Monitorings.CountAsync(cancellationToken) >= MaxProjectsPerTenant)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringProjectsLimit), MaxProjectsPerTenant]);

        var resource = await addressProbe.ReadResourceMetadata(found.ServerAddress!, cancellationToken)
            ?? throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringHealthReportNotFoundAtAddress)]);

        var issuer = resource.AuthorizationServers![0].TrimEnd('/');
        var authorizationServer = await oauthClient.ReadAuthorizationServer(issuer, cancellationToken)
            ?? throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringAppDoesNotAcceptApprovals)]);

        if (authorizationServer.CodeChallengeMethodsSupported?.Contains("S256") is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringAppDoesNotAcceptApprovals)]);

        var (verifier, challenge) = MonitoringOAuthClient.CreatePkce();
        var state = Microsoft.AspNetCore.WebUtilities.Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        var redirectUri = $"{Request.GetIssuer().TrimEnd('/')}{MonitoringOAuthClient.CallbackPath}";
        var clientId = oauthClient.ClientIdFor(Request.GetIssuer());
        var scopes = MonitoringOAuthClient.HealthzScope;

        PendingConnection pending = new()
        {
            TenantId = User.GetTenantId()!.Value,
            UserId = User.GetUserId(),
            Address = found.Address,
            ServerAddress = found.ServerAddress,
            Name = string.IsNullOrWhiteSpace(request.Name) ? found.AppTitle ?? new Uri(found.Address!).Host : request.Name.Trim(),
            ProjectId = request.ProjectId,
            Environment = environment,
            TemplateVersion = found.TemplateVersion,
            HasStaticFrontEnd = found.HasStaticFrontEnd,
            Issuer = issuer,
            TokenEndpoint = authorizationServer.TokenEndpoint,
            Resource = resource.Resource,
            Scopes = scopes,
            ClientId = clientId,
            RedirectUri = redirectUri,
            CodeVerifier = verifier,
            LocalHttpPort = request.LocalHttpPort is > 0 and <= 65535 ? request.LocalHttpPort : null,
            LocalHttpNonce = request.LocalHttpNonce
        };

        // As text: whatever serializer the distributed cache uses, a string round-trips.
        await cache.SetAsync(PendingKey(state), JsonSerializer.Serialize(pending, MonitoringJsonContext.Default.PendingConnection),
                             pendingConnectionLifetime, token: cancellationToken);

        Dictionary<string, string?> authorize = new()
        {
            ["response_type"] = "code",
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scopes,
            ["state"] = state,
            ["code_challenge"] = challenge,
            ["code_challenge_method"] = "S256",
            ["resource"] = resource.Resource
        };

        return new() { AuthorizeUrl = QueryHelpers.AddQueryString(authorizationServer.AuthorizationEndpoint!, authorize) };
    }

    [HttpPost]
    public async Task<MonitoringDto> CompleteConnect(CompleteConnectRequestDto request, CancellationToken cancellationToken)
    {
        var key = PendingKey(request.State!);
        var json = await cache.GetOrDefaultAsync<string>(key, token: cancellationToken);

        if (json is null)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringApprovalExpired)]);

        // Once: a state that comes back twice is a replay.
        await cache.RemoveAsync(key, token: cancellationToken);

        var pending = JsonSerializer.Deserialize(json, MonitoringJsonContext.Default.PendingConnection)!;

        if (pending.UserId != User.GetUserId() || pending.TenantId != User.GetTenantId())
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringApprovalStartedBySomeoneElse)]);

        if (request.Error is not null || request.Code is null)
            throw new BadRequestException(request.Error is "access_denied"
                ? Localizer[nameof(AppStrings.MonitoringApprovalDeclined)]
                : Localizer[nameof(AppStrings.MonitoringAppDidNotApprove), request.Error ?? "no code"]);

        // RFC 9207: an answer naming another issuer came from a different authorization server than the one asked.
        if (request.Iss is not null && string.Equals(request.Iss.TrimEnd('/'), pending.Issuer, StringComparison.OrdinalIgnoreCase) is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringApprovalFromDifferentApp)]);

        var token = await oauthClient.RequestToken(pending.TokenEndpoint!, new()
        {
            ["grant_type"] = "authorization_code",
            ["code"] = request.Code,
            ["redirect_uri"] = pending.RedirectUri!,
            ["client_id"] = pending.ClientId!,
            ["code_verifier"] = pending.CodeVerifier!,
            ["resource"] = pending.Resource!
        }, cancellationToken);

        if (token.Answer is null)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringAppDidNotAcceptApproval), token.Error ?? ""]);

        var now = TimeProvider.GetUtcNow();

        var project = await DbContext.Monitorings.Include(p => p.Connection).FirstOrDefaultAsync(p => p.Address == pending.Address, cancellationToken);

        if (project is null)
        {
            // The project it joins: the one its page added it to, or a new one; the first round moves it to the team's
            // project of the id its app reports, when there is one (See ProjectLinker).
            var parent = pending.ProjectId is { } parentId
                ? await DbContext.Projects.FirstOrDefaultAsync(p => p.Id == parentId, cancellationToken)
                  ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)])
                : null;

            if (parent is null)
            {
                parent = new() { Id = Guid.CreateSequentialGuid(), TenantId = pending.TenantId, Name = pending.Name, CreatedOn = now };
                await DbContext.Projects.AddAsync(parent, cancellationToken);
            }

            project = new()
            {
                Id = Guid.CreateSequentialGuid(),
                TenantId = pending.TenantId,
                ProjectId = parent.Id,
                Environment = pending.Environment ?? ProjectMonitoring.Production,
                Address = pending.Address,
                Name = ProjectMonitoring.NameOf(parent.Name, pending.Environment),
                CreatedOn = now,
                Health = ProjectHealth.NoData,
                // An SLA month follows the zone of whoever added the project until someone changes it in Settings.
                TimeZoneId = request.TimeZoneId is { } zone && TimeZoneInfo.TryFindSystemTimeZoneById(zone, out _) ? zone : "UTC",
                // Besides Germany, which reads the report; a reconnect keeps the locations the customer picked.
                Locations = [.. NetworkLocations.Defaults]
            };
            await DbContext.Monitorings.AddAsync(project, cancellationToken);

            // Whoever adds it works on the project.
            await projectsService.EnsureMember(parent.Id, pending.TenantId, pending.UserId, cancellationToken);

            // Whoever adds it hears about it first: a push at once, an email 10 minutes later if nobody is on it yet.
            // The Alerts tab changes them.
            var culture = CultureInfo.CurrentUICulture.Name;
            await DbContext.ProjectAlertRoutes.AddRangeAsync(
            [
                new() { Id = Guid.CreateSequentialGuid(), MonitoringId = project.Id, TenantId = project.TenantId, Step = 0, Channel = ProjectAlertChannel.Push, UserId = pending.UserId, Culture = culture },
                new() { Id = Guid.CreateSequentialGuid(), MonitoringId = project.Id, TenantId = project.TenantId, Step = 1, DelayMinutes = 10, Channel = ProjectAlertChannel.Email, UserId = pending.UserId, Culture = culture }
            ], cancellationToken);
        }

        project.ServerAddress = pending.ServerAddress;
        project.TemplateVersion = pending.TemplateVersion;
        project.HasStaticFrontEnd = pending.HasStaticFrontEnd;
        project.ConnectionStatus = ProjectConnectionStatus.FirstChecks;
        project.ConnectionProblem = null;

        var connection = project.Connection;
        if (connection is null)
        {
            // A new project's, or a disconnected one's, which kept none (See Disconnect): added as new, since its key, the
            // project's id, is already set and would otherwise read as a row to update.
            connection = project.Connection = new() { MonitoringId = project.Id, TenantId = project.TenantId };
            await DbContext.ProjectConnections.AddAsync(connection, cancellationToken);
        }
        connection.Issuer = pending.Issuer;
        connection.TokenEndpoint = pending.TokenEndpoint;
        connection.Resource = pending.Resource;
        connection.Scopes = pending.Scopes;
        connection.ClientId = pending.ClientId;
        connection.ConnectedByUserId = pending.UserId;
        connection.ConnectedOn = now;
        oauthClient.Store(connection, token.Answer);

        await DbContext.SaveChangesAsync(cancellationToken);

        // The first round right away, so the customer sees their app's parts as soon as they're back.
        await prober.Probe(project.Id, cancellationToken);

        // And its first scans, once.
        await scanScheduler.AfterConnecting(project, pending.UserId, cancellationToken);

        return (await DbContext.Monitorings.AsNoTracking().Include(p => p.Project).FirstAsync(p => p.Id == project.Id, cancellationToken)).Map();
    }

    [HttpPost]
    public async Task<MonitoringDto> SetLocations(SetProjectLocationsRequestDto request, CancellationToken cancellationToken)
    {
        if (request.Locations.Any(code => NetworkLocations.Find(code) is null))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringNoSuchNetworkLocation)]);

        // In the order the page lists them, each once.
        string[] locations = [.. NetworkLocations.All.Select(location => location.Code).Where(request.Locations.Contains)];

        // Without the Version check the other updates make: the probe job writes the row every minute, so the Version the
        // page read would be stale within a minute, and the locations are a whole set the request replaces.
        var updated = await DbContext.Monitorings.Where(project => project.Id == request.MonitoringId)
                                              .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.Locations, locations), cancellationToken);

        if (updated is 0)
            throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        return (await DbContext.Monitorings.AsNoTracking().FirstAsync(project => project.Id == request.MonitoringId, cancellationToken)).Map();
    }

    [HttpPost("{id}")]
    public async Task<MonitoringDto> Disconnect(Guid id, CancellationToken cancellationToken)
    {
        // Without the Version check the other updates make, as SetLocations: the probe job writes the row every minute.
        // Nothing reads the app from here on, so whatever it was is no data now.
        var updated = await DbContext.Monitorings.Where(project => project.Id == id)
                                              .ExecuteUpdateAsync(setters => setters.SetProperty(project => project.ConnectionStatus, ProjectConnectionStatus.Disconnected)
                                                                                    .SetProperty(project => project.ConnectionProblem, (string?)null)
                                                                                    .SetProperty(project => project.Health, ProjectHealth.NoData), cancellationToken);

        if (updated is 0)
            throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        // The approval's tokens go: bit Fabric keeps no key to an app it no longer watches. The app has no way to be told
        // (the template has no RFC 7009 revocation endpoint), so its session of bit Fabric ends when it expires, or at once
        // from the app's own Settings, Sessions.
        await DbContext.ProjectConnections.Where(connection => connection.MonitoringId == id).ExecuteDeleteAsync(cancellationToken);

        // An incident still open stays open, since no round will tell when it ended, but stops escalating: nobody is to be
        // woken for an app nobody watches.
        await DbContext.ProjectIncidents.Where(incident => incident.MonitoringId == id && incident.EndedAt == null)
                                        .ExecuteUpdateAsync(setters => setters.SetProperty(incident => incident.NextEscalationAt, (DateTimeOffset?)null), cancellationToken);

        return (await DbContext.Monitorings.AsNoTracking().FirstAsync(project => project.Id == id, cancellationToken)).Map();
    }

    /// <inheritdoc cref="IMonitoringsController.ExportHistoryUri"/>
    [HttpGet("{monitoringId}/{format}")]
    public async Task ExportHistory(Guid monitoringId, string format, CancellationToken cancellationToken)
    {
        var project = await monitoringsService.FindProject(monitoringId, cancellationToken);

        var kind = format.ToLowerInvariant() switch
        {
            "csv" => ProjectHistoryExport.Format.Csv,
            "json" => ProjectHistoryExport.Format.Json,
            _ => throw new BadRequestException()
        };

        Response.ContentType = kind is ProjectHistoryExport.Format.Csv ? "text/csv; charset=utf-8" : "application/json; charset=utf-8";
        // filename* too, so a name in any script survives (RFC 6266), as File() writes it for the SLA's exports.
        var disposition = new Microsoft.Net.Http.Headers.ContentDispositionHeaderValue("attachment");
        disposition.SetHttpFileName(ProjectHistoryExport.FileNameOf(project, kind, TimeProvider.GetUtcNow()));
        Response.Headers.ContentDisposition = disposition.ToString();

        // Written as it is read, a page of rows at a time, so a year of history never sits in memory (See ProjectHistoryExport).
        await historyExport.Write(project, kind, Response.Body, cancellationToken);
    }

    [HttpDelete("{id}")]
    public async Task Delete(Guid id, CancellationToken cancellationToken)
    {
        var project = await DbContext.Monitorings.FirstOrDefaultAsync(project => project.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        // Its connection, rounds, periods and starts go with it (cascade).
        DbContext.Monitorings.Remove(project);
        await DbContext.SaveChangesAsync(cancellationToken);

        // A project goes with its last environment, unless it holds a license, which names it.
        await DbContext.Projects.Where(parent => parent.Id == project.ProjectId
                                                 && parent.Monitorings.Any() == false
                                                 && parent.Licenses.Any() == false)
                                .ExecuteDeleteAsync(cancellationToken);
    }

    internal static string PendingKey(string state) => $"monitoring-connect:{state}";
}
