using System.ComponentModel;
using System.Reflection;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// Project monitoring for AI apps (bitfabric-internal/specs/monitoring.md, AI apps): what the projects page and a
/// project's tabs show, and the few things those tabs let the team do, as tools. They are served on <c>/mcp</c>, which
/// grok.com or any other MCP client adds as a connector once the user approves it through the app's OAuth, and handed to
/// the in-app assistant as well (See <see cref="AIFunctions"/>).
/// <para>
/// Guarded as the api is: a signed-in user with a team selected (<see cref="AuthPolicies.TENANT_SELECTED"/>), and every
/// query through the tenant filter of the team the caller's token is for: the app's own token's, or the one an OAuth
/// approval was given in (See OAuthController.ResolveTenantId). They read and write through the services the pages'
/// controllers use (<see cref="ProjectsService"/>, <see cref="ProjectIncidentsService"/>, <see cref="ProjectSlaReports"/>,
/// <see cref="StatusPageService"/>), so a tool can do nothing a page can't, and returns compact JSON: times in UTC ISO
/// 8601, the project's time zone where a page uses it, durations with their unit in the name, and a message for the model
/// rather than an error when a request can't be answered.
/// </para>
/// </summary>
[McpServerToolType]
public partial class ProjectMonitoringMcpTools(AppDbContext dbContext,
    ProjectsService projectsService,
    ProjectIncidentsService incidentsService,
    ProjectSlaReports slaReports,
    StatusPageService statusPages,
    ServerApiSettings settings,
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor,
    TimeProvider timeProvider) : IAsyncDisposable
{
    private const string ProjectParameter = "The project: its id, or its name or address as the user says it, such as \"Todo\" or \"todo.bitplatform.dev\". A name or address is matched case-insensitively among the projects of the user's team.";

    private const string ConfirmFirst = "It changes the project for the whole team, so first tell the user exactly what you are about to do and call it only once they have confirmed in the conversation.";

    [McpServerTool(Name = nameof(GetProjects), Title = "List the monitored projects", ReadOnly = true, OpenWorld = false)]
    [Description("Lists every project the user's team monitors in bit Fabric (each project is one of their live apps), failing ones first, with its health right now: Operational; Degraded, some parts don't work while the app itself does; Down; NeedsReconnecting, bit Fabric's approval in the app stopped working, so nothing is read until a team member reconnects it on the project's Settings tab; Disconnected, a team member disconnected it on its Settings tab, so nothing is read until someone reconnects it; NotCheckedYet, its first checks are running; or NoData. Each also has its uptime over the last 30 days, the days of those 30 a check found it down or a part failing, as its card's day bars show them, the typical (median) and the slowest 5% response time of its home page over the last 24 hours past any cache (the server's own time), the typical time a visitor waits for it through the cache in front of the app when one answers it, the parts not working right now by the app's own names, its ongoing incident, a cache warning, and when it was last checked. Call it first for any question about the user's projects or apps, such as \"are my projects healthy?\" or \"how did they do this month?\".")]
    public async Task<JsonElement> GetProjects(
        [Description("The IANA time zone the last 30 days are counted in, such as \"Asia/Tehran\": the user's own when they say it. UTC when left out.")] string? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var zone = ProjectsController.ZoneOf(timeZone);
        var projects = await projectsService.GetProjects(zone, cancellationToken);

        return Result(new ProjectList(await TeamName(cancellationToken), zone.Id, [.. projects.Select(project => new ProjectSummary(
            project.Id,
            project.Name,
            project.Address,
            HealthOf(project),
            project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded ? project.ConnectionProblem : null,
            Round(project.Uptime30Days, 3),
            project.MedianMs,
            project.P95Ms,
            project.MedianVisitorMs,
            project.FailingChecks,
            OngoingOf(project),
            project.HasCacheWarning ? CacheWarning : null,
            project.LastCheckedAt,
            [.. project.Last30Days.Where(day => day.Tone is ProjectDayTone.Down or ProjectDayTone.Degraded)
                                  .Select(day => new DayWithProblems(day.Date, $"{day.Tone}", day.DownMinutes, day.DegradedMinutes))]))]));
    }

    [McpServerTool(Name = nameof(GetProject), Title = "Show a project in detail", ReadOnly = true, OpenWorld = false)]
    [Description("Shows one project in detail, as its Overview does: its health, every part of the app from its last health report grouped by category, each with its status (Healthy, Degraded, Unhealthy, or NotConfigured: its settings are missing, which is a setup finding rather than an outage), the app's own message about it, what it proves, how long it took against its limit in milliseconds, when a reused result was measured, and since when it has been in that status; the build it runs, its last deployment and restart, a maintenance window it is in right now, and where it is checked from with each location's newest reading.")]
    public async Task<JsonElement> GetProject([Description(ProjectParameter)] string project, CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var (found, problem) = await FindProject(project, cancellationToken);
        if (found is null)
            return Result(problem!);

        var details = await projectsService.GetProject(found.Id, cancellationToken);
        var dto = details.Project!;
        var now = timeProvider.GetUtcNow();

        var maintenance = (await incidentsService.GetMaintenanceWindows(found.Id, cancellationToken))
            .FirstOrDefault(window => new ProjectMaintenanceWindow { StartsAt = window.StartsAt, EndsAt = window.EndsAt, RepeatsWeekly = window.RepeatsWeekly }.Covers(now));

        // The worst group first, and in each the worst part first, as the Overview puts what needs attention on top.
        PartGroup[] parts = [.. details.Checks
            .Select(check => (Category: string.IsNullOrWhiteSpace(check.Category) ? "Other" : check.Category.Trim(),
                              Part: new Part(check.Key,
                                             string.IsNullOrWhiteSpace(check.Title) ? check.Key ?? "" : check.Title,
                                             check.IsNotConfigured ? NotConfigured : $"{check.Status}",
                                             check.Description,
                                             check.About,
                                             check.DurationMs,
                                             check.TimeoutMs,
                                             check.VerifiedAt,
                                             check.Since)))
            .GroupBy(entry => entry.Category, entry => entry.Part)
            .OrderBy(group => group.Min(part => StatusRank(part.Status)))
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new PartGroup(group.Key, [.. group.OrderBy(part => StatusRank(part.Status)).ThenBy(part => part.Title, StringComparer.OrdinalIgnoreCase)]))];

        // bit Fabric's own server reads the report; the bitplatform Network's locations the customer picked time the home page.
        CheckedFrom[] checkedFrom =
        [
            new(settings.Monitoring.Region, null, "The health report with every part of the app, and the home page", details.CheckedAt, null, details.HomePageMs, details.HomePageVisitorMs, details.ReportMs, null),
            .. dto.Locations.Select(NetworkLocations.Find).OfType<NetworkLocation>().Select(location =>
                details.NetworkReadings.FirstOrDefault(reading => reading.Location == location.Code) is { } reading
                    ? new CheckedFrom(location.Name, location.Code, "The home page", reading.At, reading.Colo, reading.ResponseMs, null, null, reading.ResponseMs is not null)
                    : new CheckedFrom(location.Name, location.Code, "The home page", null, null, null, null, null, null))
        ];

        return Result(new ProjectDetail(dto.Id,
            dto.Name,
            dto.Address,
            dto.ServerAddress,
            HealthOf(dto),
            dto.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded ? dto.ConnectionProblem : null,
            found.TimeZoneId,
            OngoingOf(dto),
            maintenance?.Title,
            details.CheckedAt,
            details.ReportMs,
            details.ApplicationVersion,
            details.LastDeployedAt,
            details.LastRestartedAt,
            parts,
            checkedFrom));
    }

    [McpServerTool(Name = nameof(GetIncidents), Title = "List incidents", ReadOnly = true, OpenWorld = false)]
    [Description("Lists incidents, ongoing ones first, then the newest: what broke, its impact (Down, or Degraded when some parts failed while the app worked), when it began and ended and how long it took, the parts it was about by the app's own names, whether a deployment shortly before it is its likely cause, whether it happened during maintenance, who acknowledged it, the story of its updates, who was alerted and whether each alert arrived, and once it is over a prompt for the team's AI agent to find the cause in the app's logs. Incidents bit Fabric opened from the checks close by themselves once the checks pass again.")]
    public async Task<JsonElement> GetIncidents(
        [Description(ProjectParameter + " Leave it out for every project of the team.")] string? project = null,
        [Description("How many days back ended incidents go, 1 to 90; ongoing ones always come. 30 when left out.")] int days = 30,
        [Description("True for only the incidents still going on.")] bool ongoingOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var since = timeProvider.GetUtcNow().AddDays(-Math.Clamp(days, 1, 90));

        List<Project> projects;

        if (string.IsNullOrWhiteSpace(project))
        {
            projects = await dbContext.Projects.AsNoTracking()
                .Where(candidate => dbContext.ProjectIncidents.Any(incident => incident.ProjectId == candidate.Id && (incident.EndedAt == null || incident.StartedAt >= since)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            var (found, problem) = await FindProject(project, cancellationToken);
            if (found is null)
                return Result(problem!);

            projects = [found];
        }

        List<Incident> incidents = [];

        foreach (var each in projects)
        {
            incidents.AddRange((await incidentsService.IncidentsOf(each, since, cancellationToken))
                .Where(incident => ongoingOnly is false || incident.EndedAt is null)
                .Select(incident => IncidentOf(incident, new(each.Id, each.Name))));
        }

        return Result(new IncidentList([.. incidents.OrderByDescending(incident => incident.Ongoing).ThenByDescending(incident => incident.StartedAt)]));
    }

    [McpServerTool(Name = nameof(GetHistory), Title = "Show a project's history", ReadOnly = true, OpenWorld = false)]
    [Description("Shows how a project did day by day, as its History tab does: for each day the app's uptime and its minutes down and degraded, from its incidents, and whether maintenance was planned that day; each part of the app with its uptime over the span and the days it had trouble; the incidents, deployments, restarts and maintenance windows of the span. Days run midnight to midnight in the time zone given, the project's own when none is.")]
    public async Task<JsonElement> GetHistory(
        [Description(ProjectParameter)] string project,
        [Description("How many days back from today, 1 to 90. 7 when left out.")] int days = 7,
        [Description("The IANA time zone the days are counted in, such as \"Europe/Berlin\"; the user's own when you know it. The project's time zone when left out.")] string? timeZone = null,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var (found, problem) = await FindProject(project, cancellationToken);
        if (found is null)
            return Result(problem!);

        var zoneId = string.IsNullOrWhiteSpace(timeZone) ? found.TimeZoneId ?? "UTC" : timeZone.Trim();
        if (TimeZoneInfo.TryFindSystemTimeZoneById(zoneId, out var zone) is false)
            return Result(new Problem($"\"{zoneId}\" is no time zone bit Fabric knows. Use an IANA time zone such as \"Europe/Berlin\", or leave it out for the project's own."));

        var now = timeProvider.GetUtcNow();
        var span = ProjectHistoryDays.Last(Math.Clamp(days, 1, 90), zone, now);

        var history = await incidentsService.GetHistory(new() { ProjectId = found.Id, From = span[0].Start, To = now }, cancellationToken);

        // The rows History draws, as the server lists and names them: a part counts only while the app reports it.
        var partsOfTheApp = history.Parts.OrderBy(part => part.Title, StringComparer.OrdinalIgnoreCase);

        var appDays = ProjectHistoryDays.OfApp(history, span, now);

        return Result(new History(new(found.Id, found.Name),
            zone.Id,
            span[0].Start,
            now,
            history.WatchedSince,
            Round(ProjectHistoryDays.UptimeOf(appDays), 3),
            [.. appDays.Select(day => new HistoryDay(day.Date, $"{day.Tone}", Round(day.Uptime, 3), day.DownMinutes, day.DegradedMinutes, day.Maintenance))],
            [.. partsOfTheApp.Select(part =>
            {
                var partDays = ProjectHistoryDays.OfPart(history, part, span, now);
                return new HistoryPart(part.Key!, part.Title ?? part.Key!, Round(ProjectHistoryDays.UptimeOf(partDays), 3),
                    [.. partDays.Where(day => day.Tone is ProjectDayTone.Down or ProjectDayTone.Degraded)
                                .Select(day => new HistoryDay(day.Date, $"{day.Tone}", Round(day.Uptime, 3), day.DownMinutes, day.DegradedMinutes, day.Maintenance))],
                    part.ReportedFrom, part.ReportedTo);
            })],
            [.. history.Incidents.Select(incident => new HistoryIncident(incident.Id, incident.Title, $"{incident.Impact}", incident.StartedAt, incident.EndedAt,
                MinutesOf(incident.StartedAt, incident.EndedAt ?? now), incident.CheckTitles, incident.AfterDeployAt, incident.DuringMaintenance))],
            [.. history.Starts.Select(start => new Start(start.At, start.IsDeploy ? "Deployment" : "Restart", start.ApplicationVersion))],
            [.. history.Maintenance.Select(window => new Maintenance(window.Id, window.Title, window.StartsAt, window.EndsAt, window.RepeatsWeekly))]));
    }

    [McpServerTool(Name = nameof(GetSlaReport), Title = "Show a project's SLA report", ReadOnly = true, OpenWorld = false)]
    [Description("Shows a month's SLA report, as the SLA tab does: the app's availability against the target the owner set, the downtime the target allows that month and how much of it is still affordable, how partial outages are weighed, the credit tier of the owner's contract the month falls into, each part of the app with its own availability, what was left out of the count (maintenance, time bit Fabric couldn't read the app, time before it watched it) and the last 12 months' figures. A month runs from midnight of its first day in the project's time zone; this month, so far, when none is given.")]
    public async Task<JsonElement> GetSlaReport(
        [Description(ProjectParameter)] string project,
        [Description("The year of the month, with month; leave both out for this month.")] int? year = null,
        [Description("The month, 1 to 12, with year; leave both out for this month.")] int? month = null,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var (found, problem) = await FindProject(project, cancellationToken);
        if (found is null)
            return Result(problem!);

        ProjectSlaReportDto report;

        try
        {
            report = await slaReports.Build(found, year, month, cancellationToken);
        }
        catch (BadRequestException)
        {
            return Result(new Problem($"Give both a year and a month among the last {ProjectSlaReports.MonthCount} months, this one included, or neither for this month."));
        }

        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(report.TimeZoneId ?? "UTC", out var projectZone) ? projectZone : TimeZoneInfo.Utc;

        return Result(new SlaReport(new(found.Id, found.Name),
            report.TimeZoneId,
            report.Year,
            report.Month,
            report.From,
            report.To,
            TimeZoneInfo.ConvertTime(report.From, zone),
            TimeZoneInfo.ConvertTime(report.To, zone),
            report.IsCurrent,
            report.Settings.TargetPercent,
            Round(report.App.Availability, 3),
            report.App.Availability is { } availability ? availability >= report.Settings.TargetPercent : null,
            Round(report.BudgetMinutes, 1),
            Round(report.LeftMinutes, 1),
            Round(report.App.WeightedMinutes, 1),
            Round(report.App.DownMinutes, 1),
            Round(report.App.PartialMinutes, 1),
            Round(report.App.CountedMinutes, 1),
            report.Settings.PartialWeight,
            report.Settings.DegradedWeight,
            report.Credit is { } credit ? new(credit.BelowPercent, credit.CreditPercent) : null,
            [.. report.Settings.Credits.Select(tier => new SlaCredit(tier.BelowPercent, tier.CreditPercent))],
            [.. report.Parts.Select(part => new SlaPart(part.Key, part.Title, Round(part.Availability, 3), Round(part.DownMinutes, 1), Round(part.PartialMinutes, 1), Round(part.CountedMinutes, 1)))],
            Round(report.MaintenanceMinutes, 1),
            Round(report.NoDataMinutes, 1),
            Round(report.NotWatchedMinutes, 1),
            [.. report.Maintenance.Select(window => new SlaMaintenance(window.Title, window.StartsAt, window.EndsAt, window.RepeatsWeekly, Round(window.Minutes, 1)))],
            [.. report.Months.Select(each => new SlaMonth(each.Year, each.Month, Round(each.Availability, 3), each.IsCurrent))]));
    }

    [McpServerTool(Name = nameof(GetStatusPage), Title = "Show a project's status page", ReadOnly = true, OpenWorld = false)]
    [Description("Shows a project's public status page, as its Status page tab does: whether it is published and its address, the team's own host name for it and whether it is active yet, its languages, the overall state and each component (a part of the app as its users know it) with its state, the incidents it shows right now, whether maintenance is going on, and how many visitors follow it by push.")]
    public async Task<JsonElement> GetStatusPage([Description(ProjectParameter)] string project, CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var (found, problem) = await FindProject(project, cancellationToken);
        if (found is null)
            return Result(problem!);

        if (await statusPages.Find(found.Id, cancellationToken) is not { } page)
            return Result(new Problem($"{found.Name} has no status page yet: one is made, unpublished, the first time a team member opens the project's Status page tab."));

        var status = await statusPages.BuildPublic(page, found, CultureInfo.CurrentUICulture.Name, cancellationToken);

        return Result(new StatusPageReport(new(found.Id, found.Name),
            page.IsPublished,
            statusPages.PublicUrlOf(page),
            page.CustomHostname,
            page.CustomHostname is null ? null : page.CustomHostnameState ?? "pending",
            page.Languages,
            $"{status.State}",
            status.InMaintenance,
            [.. status.Components.Select(component => new PageComponent(component.Name, $"{component.State}"))],
            [.. status.Active.Select(incident => new PageIncident(incident.Id, incident.Title, $"{incident.Impact}", incident.StartedAt))],
            await statusPages.FollowerCountOf(page.Id, cancellationToken),
            status.CheckedAt));
    }

    [McpServerTool(Name = nameof(AcknowledgeIncident), Title = "Acknowledge an incident (I'm on it)", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false)]
    [Description("Says \"I'm on it\" for an ongoing incident in the signed-in user's name, as the button on a project's Incidents tab does: the escalation stops, so nobody else is alerted about it, and an update tells the team who has it. " + ConfirmFirst + " An incident that is over, or that someone already acknowledged, stays as it is.")]
    public async Task<JsonElement> AcknowledgeIncident(
        [Description("The incident's id, from GetProjects or GetIncidents.")] Guid incidentId,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        var before = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.Id == incidentId)
            .Select(incident => new { incident.ProjectId, ProjectName = incident.Project!.Name, incident.EndedAt, incident.AcknowledgedBy, incident.AcknowledgedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (before is null)
            return Result(new Problem("No incident of the user's team has that id: GetIncidents lists them."));

        var incident = await incidentsService.Acknowledge(incidentId, User.GetUserId(), await AuthorName(cancellationToken), cancellationToken);

        var message = before.EndedAt is not null ? "Nothing changed: the incident is already over."
            : before.AcknowledgedAt is not null ? $"Nothing changed: {before.AcknowledgedBy} acknowledged it already."
            : "Acknowledged: the escalation stopped, and the incident's updates say who is on it.";

        return Result(new Done(message, Incident: IncidentOf(incident, new(before.ProjectId, before.ProjectName))));
    }

    [McpServerTool(Name = nameof(PostIncidentUpdate), Title = "Post a team update to an incident", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Adds the team's own update to an incident in the signed-in user's name, as a project's Incidents tab does, such as what they found or what they did. " + ConfirmFirst + " Show them the text you will post. An incident bit Fabric opened from the checks can't be marked Resolved: it closes by itself once the checks pass again.")]
    public async Task<JsonElement> PostIncidentUpdate(
        [Description("The incident's id, from GetProjects or GetIncidents.")] Guid incidentId,
        [Description("Where the team is: Investigating, Identified, Monitoring (a fix is in and they watch it) or Resolved.")] ProjectIncidentPhase phase,
        [Description("The update, in the user's words and language, at most 2000 characters.")] string text,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 2000)
            return Result(new Problem("The update needs a text of at most 2000 characters."));

        var before = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.Id == incidentId)
            .Select(incident => new { incident.ProjectId, ProjectName = incident.Project!.Name, incident.IsAutomatic, incident.Version })
            .FirstOrDefaultAsync(cancellationToken);

        if (before is null)
            return Result(new Problem("No incident of the user's team has that id: GetIncidents lists them."));

        if (before.IsAutomatic && phase is ProjectIncidentPhase.Resolved)
            return Result(new Problem("bit Fabric opened this incident from the checks, so it closes it by itself once they pass again: post Monitoring instead."));

        try
        {
            var incident = await incidentsService.PostUpdate(new() { IncidentId = incidentId, Phase = phase, Text = text.Trim(), Version = before.Version },
                                                             await AuthorName(cancellationToken), cancellationToken);

            return Result(new Done("Posted.", Incident: IncidentOf(incident, new(before.ProjectId, before.ProjectName))));
        }
        catch (ConflictException)
        {
            return Result(new Problem("The incident changed while the update was being written, so nothing was posted: call again."));
        }
    }

    [McpServerTool(Name = nameof(ScheduleMaintenance), Title = "Schedule a maintenance window", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = false)]
    [Description("Schedules a maintenance window for a project, as its Settings tab does: bit Fabric goes on checking the app, but an incident that begins in the window alerts nobody, and the window doesn't count against the SLA. A weekly one comes again at the same time every week. " + ConfirmFirst + " Confirm the start and end with their time zone too.")]
    public async Task<JsonElement> ScheduleMaintenance(
        [Description(ProjectParameter)] string project,
        [Description("What the work is, such as \"Moving the database\", at most 200 characters.")] string title,
        [Description("When it starts, in ISO 8601 with its offset, such as 2026-11-14T02:00:00+01:00.")] DateTimeOffset startsAt,
        [Description("When it ends, in ISO 8601 with its offset; after the start, and less than a week after it for a weekly one.")] DateTimeOffset endsAt,
        [Description("True when it comes again every week at the same time.")] bool repeatsWeekly = false,
        CancellationToken cancellationToken = default)
    {
        if (await Refusal() is { } refusal)
            return Result(refusal);

        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            return Result(new Problem("The window needs a title of at most 200 characters."));

        if (endsAt <= startsAt || (repeatsWeekly && endsAt - startsAt >= TimeSpan.FromDays(7)))
            return Result(new Problem("The window has to end after it starts, and a weekly one less than a week after it."));

        var (found, problem) = await FindProject(project, cancellationToken);
        if (found is null)
            return Result(problem!);

        var window = await incidentsService.AddMaintenanceWindow(new() { ProjectId = found.Id, Title = title.Trim(), StartsAt = startsAt, EndsAt = endsAt, RepeatsWeekly = repeatsWeekly }, cancellationToken);

        return Result(new Done("Scheduled: nobody is alerted about an incident that begins in it, and it doesn't count against the SLA.",
            Maintenance: new(window.Id, window.Title, window.StartsAt.ToUniversalTime(), window.EndsAt.ToUniversalTime(), window.RepeatsWeekly)));
    }

    /// <summary>
    /// The tools for the in-app assistant, each run in a scope of its own, as the assistant's other tools are: the
    /// conversation outlives any one turn, and a DbContext must not be shared by two of its turns.
    /// </summary>
    public static IEnumerable<AIFunction> AIFunctions(IServiceProvider services)
    {
        return typeof(ProjectMonitoringMcpTools).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Tool is not null)
            .Select(tool => AIFunctionFactory.Create(tool.Method, _ => InScopeOfItsOwn(services), new AIFunctionFactoryOptions { Name = tool.Tool!.Name ?? tool.Method.Name }));
    }

    private AsyncServiceScope? ownScope;

    private static ProjectMonitoringMcpTools InScopeOfItsOwn(IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        var tools = ActivatorUtilities.CreateInstance<ProjectMonitoringMcpTools>(scope.ServiceProvider);
        tools.ownScope = scope;
        return tools;
    }

    public async ValueTask DisposeAsync()
    {
        if (ownScope is { } scope)
        {
            await scope.DisposeAsync();
        }
    }

    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new(new ClaimsIdentity());

    /// <summary>What the api asks of every monitoring request, in words for the model instead of a 401 or a 403.</summary>
    private async Task<Problem?> Refusal()
    {
        if (User.IsAuthenticated() is false)
            return new("The user isn't signed in to bit Fabric, and their projects need them to be.");

        if ((await authorizationService.AuthorizeAsync(User, AuthPolicies.TENANT_SELECTED)).Succeeded is false)
            return new("This bit Fabric session has no team, and projects belong to a team. The user makes one, or picks one, on bit Fabric's projects page; an AI app connected to bit Fabric then has to be connected again for that team.");

        return null;
    }

    /// <summary>The team the request's token is for, so a user in several sees which one answers.</summary>
    private async Task<string?> TeamName(CancellationToken cancellationToken)
    {
        var tenantId = User.GetTenantId();

        var team = await dbContext.Tenants.AsNoTracking()
            .Where(tenant => tenant.Id == tenantId)
            .Select(tenant => new { tenant.Title, tenant.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return team is null ? null : string.IsNullOrWhiteSpace(team.Title) ? team.Name : team.Title;
    }

    /// <summary>
    /// The one project of the team <paramref name="wanted"/> names: by id, then by its exact name or host, then by a part of
    /// either; or why there isn't exactly one, with the projects to choose from. A team has 100 projects at most.
    /// </summary>
    private async Task<(Project? Project, Problem? Problem)> FindProject(string? wanted, CancellationToken cancellationToken)
    {
        var projects = await dbContext.Projects.AsNoTracking().OrderBy(project => project.Name).ToListAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(wanted))
            return (null, new("Say which project: its id, name or address.", ChoicesOf(projects)));

        var text = wanted.Trim();

        if (Guid.TryParse(text, out var id))
        {
            return projects.FirstOrDefault(project => project.Id == id) is { } byId
                ? (byId, null)
                : (null, new($"No project of the user's team has the id {id}.", ChoicesOf(projects)));
        }

        var host = HostOf(text);

        Project[] matches = [.. projects.Where(project => string.Equals(project.Name?.Trim(), text, StringComparison.OrdinalIgnoreCase)
                                                          || (host is not null && (host == HostOf(project.Address) || host == HostOf(project.ServerAddress))))];

        if (matches.Length is 0)
        {
            matches = [.. projects.Where(project => project.Name?.Contains(text, StringComparison.OrdinalIgnoreCase) is true
                                                    || HostOf(project.Address)?.Contains(text, StringComparison.OrdinalIgnoreCase) is true)];
        }

        return matches.Length switch
        {
            1 => (matches[0], null),
            0 => (null, new($"No project of the user's team matches \"{text}\".", ChoicesOf(projects))),
            _ => (null, new($"{matches.Length} projects of the user's team match \"{text}\": ask the user which one they mean, then call again with its id.", ChoicesOf(matches)))
        };
    }

    private static ProjectChoice[] ChoicesOf(IEnumerable<Project> projects)
        => [.. projects.Select(project => new ProjectChoice(project.Id, project.Name, project.Address))];

    /// <summary>"Todo.bitplatform.dev", "https://todo.bitplatform.dev/" and "www.todo.bitplatform.dev" alike as "todo.bitplatform.dev"; null for what is no address.</summary>
    private static string? HostOf(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
            return null;

        var text = address.Trim();

        if (Uri.TryCreate(text.Contains("://", StringComparison.Ordinal) ? text : $"https://{text}", UriKind.Absolute, out var uri) is false || uri.Host.Length is 0)
            return null;

        var authority = uri.Authority.ToLowerInvariant();

        return authority.StartsWith("www.", StringComparison.Ordinal) ? authority[4..] : authority;
    }

    /// <summary>Who acts, as the page's own updates name them (See ClaimsPrincipalExtensions.GetDisplayName): a token issued to an AI app carries no name, so it is read from the account.</summary>
    private async Task<string> AuthorName(CancellationToken cancellationToken)
    {
        var name = User.GetEmail() ?? (User.FindFirst(ClaimTypes.Name) ?? User.FindFirst("unique_name"))?.Value;

        if (string.IsNullOrEmpty(name) is false)
            return name;

        var userId = User.GetUserId();

        return await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.Email ?? user.UserName)
            .FirstOrDefaultAsync(cancellationToken) ?? $"{userId}";
    }

    /// <summary>As the project's card words it (See ProjectCard.StateText).</summary>
    private static string HealthOf(ProjectDto project) => (project.ConnectionStatus, project.Health) switch
    {
        (ProjectConnectionStatus.ReconnectNeeded, _) => "NeedsReconnecting",
        (ProjectConnectionStatus.Disconnected, _) => "Disconnected",
        (ProjectConnectionStatus.FirstChecks, ProjectHealth.NoData) => "NotCheckedYet",
        (_, ProjectHealth.Down) => "Down",
        (_, ProjectHealth.Degraded) => "Degraded",
        (_, ProjectHealth.Operational) => "Operational",
        _ => "NoData"
    };

    private static OngoingIncident? OngoingOf(ProjectDto project)
        => project.OngoingIncidentId is { } id ? new(id, project.OngoingIncident, project.OngoingIncidentSince!.Value) : null;

    /// <summary>A part whose settings are missing: a setup finding, not an outage.</summary>
    private const string NotConfigured = "NotConfigured";

    /// <summary>Failing first, then degraded, then not configured, as the Overview ranks its parts.</summary>
    private static int StatusRank(string status) => status switch
    {
        nameof(HealthCheckStatus.Unhealthy) => 0,
        nameof(HealthCheckStatus.Degraded) => 1,
        NotConfigured => 2,
        _ => 3
    };

    private Incident IncidentOf(ProjectIncidentDto incident, ProjectRef project) => new(incident.Id,
        project,
        $"{incident.Kind}",
        $"{incident.Impact}",
        incident.Title,
        incident.EndedAt is null,
        incident.StartedAt,
        incident.EndedAt,
        MinutesOf(incident.StartedAt, incident.EndedAt ?? timeProvider.GetUtcNow()),
        incident.CheckTitles,
        incident.IsAutomatic,
        incident.AfterDeployAt,
        incident.DuringMaintenance,
        incident.AcknowledgedBy,
        incident.AcknowledgedAt,
        [.. incident.Updates.Select(update => new IncidentUpdate(update.At, $"{update.Phase}", update.Text, update.Author ?? "bit Fabric"))],
        [.. incident.Alerts.Select(alert => new AlertSent(alert.SentAt, $"{alert.Kind}", $"{alert.Channel}", alert.Target, alert.Succeeded,
            alert.Problem is { } why ? alert.ProblemStatusCode is { } code ? $"{why} (HTTP {code})" : $"{why}" : null))],
        incident.AgentPrompt);

    private static double MinutesOf(DateTimeOffset from, DateTimeOffset to) => Math.Round(Math.Max(0, (to - from).TotalMinutes), 1);

    private static double? Round(double? value, int digits) => value is { } number ? Math.Round(number, digits) : null;

    private static double Round(double value, int digits) => Math.Round(value, digits);

    private const string CacheWarning = "The home page's last answer came from a cache that ignored its unique query string, so its response time may not be what visitors wait for: the app's cache is set up wrong.";

    private static readonly JsonSerializerOptions resultJson = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary>Written here rather than by the host, so <c>/mcp</c> and the in-app assistant get the same compact JSON.</summary>
    private static JsonElement Result<T>(T value) => JsonSerializer.SerializeToElement(value, resultJson);
}
