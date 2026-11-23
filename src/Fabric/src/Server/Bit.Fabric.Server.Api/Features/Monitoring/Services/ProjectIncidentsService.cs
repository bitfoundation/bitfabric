using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// A project's History and Incidents and its maintenance windows (bitfabric-internal/specs/monitoring.md),
/// for <see cref="ProjectIncidentsController"/> and the AI tools (<see cref="ProjectMonitoringMcpTools"/>) alike,
/// so "I'm on it" and a team's update do the same from either. Every query goes through the tenant filter: another team's
/// project or incident is simply not found.
/// </summary>
public partial class ProjectIncidentsService(AppDbContext dbContext,
    TimeProvider timeProvider,
    ProjectTexts texts,
    IStringLocalizer<AppStrings> localizer)
{
    /// <summary>The periods, incidents, deployments and maintenance overlapping a span, for History.</summary>
    public async Task<ProjectHistoryDto> GetHistory(ProjectHistoryRequestDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.ProjectId, cancellationToken);
        var from = request.From.ToUniversalTime();
        var to = request.To.ToUniversalTime();

        if (to <= from || to - from > TimeSpan.FromDays(400))
            throw new BadRequestException();

        // Only the stretches a part wasn't healthy: healthy is what a part is when nothing says otherwise.
        var periods = await dbContext.ProjectCheckPeriods.AsNoTracking()
            .Where(period => period.ProjectId == project.Id && period.Status != HealthCheckStatus.Healthy
                             && period.StartedAt < to && (period.EndedAt == null || period.EndedAt > from))
            .OrderBy(period => period.StartedAt)
            .Select(period => new ProjectPeriodDto
            {
                CheckKey = period.CheckKey,
                Title = period.Title,
                Status = period.Status,
                StartedAt = period.StartedAt,
                EndedAt = period.EndedAt,
                Description = period.Description
            })
            .ToArrayAsync(cancellationToken);

        var incidents = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.ProjectId == project.Id && incident.StartedAt < to && (incident.EndedAt == null || incident.EndedAt > from))
            .OrderByDescending(incident => incident.StartedAt)
            .ToListAsync(cancellationToken);

        var starts = await dbContext.ProjectStarts.AsNoTracking()
            .Where(start => start.ProjectId == project.Id && start.At >= from && start.At < to)
            .OrderBy(start => start.At)
            .Select(start => new ProjectStartDto { At = start.At, IsDeploy = start.IsDeploy, ApplicationVersion = start.ApplicationVersion })
            .ToArrayAsync(cancellationToken);

        var lastReport = await LastReportOf(project.Id, cancellationToken);
        var parts = await PartsOf(project, lastReport, periods, from, to, cancellationToken);
        var partTitles = parts.ToDictionary(part => part.Key!, part => part.Title!);
        var titles = TitlesOf(lastReport);

        return new()
        {
            // A period goes by its part's name today, and a part History leaves out takes its periods with it.
            Periods = [.. periods.Where(period => partTitles.ContainsKey(period.CheckKey!)).Select(period =>
            {
                period.Title = partTitles[period.CheckKey!];
                return period;
            })],
            Parts = parts,
            Incidents = [.. incidents.Select(incident => ToDto(project, incident, titles, []))],
            Starts = starts,
            Maintenance = await GetMaintenanceWindows(project.Id, cancellationToken),
            WatchedSince = project.CreatedOn
        };
    }

    /// <summary>
    /// History's rows, where a part counts only while the app reports it: the latest report's parts and every
    /// other part that wasn't healthy within the span, each named the way the latest report names it, and each with the
    /// time within the span the app reported it, from the 15-minute rollups every round writes for every configured part.
    /// A part the latest report says only misses its settings gets no row, whatever an older template once said of it.
    /// </summary>
    private async Task<ProjectHistoryPartDto[]> PartsOf(Project project, ProjectProbe? lastReport, ProjectPeriodDto[] periods, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var reported = lastReport?.Checks.Where(check => check.Key is not null).DistinctBy(check => check.Key).ToDictionary(check => check.Key!) ?? [];

        var keys = reported.Values.Where(check => check.IsNotConfigured is false).Select(check => check.Key!)
            .Concat(periods.Select(period => period.CheckKey!))
            .Where(key => reported.GetValueOrDefault(key)?.IsNotConfigured is not true)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        // The bucket that began before the span covers its start.
        var bucketsFrom = from.AddMinutes(-ProjectRollup.BucketMinutes);

        var spans = await dbContext.ProjectRollups.AsNoTracking()
            .Where(rollup => rollup.ProjectId == project.Id && keys.Contains(rollup.CheckKey!) && rollup.BucketStart > bucketsFrom && rollup.BucketStart < to)
            .GroupBy(rollup => rollup.CheckKey)
            .Select(group => new { Key = group.Key!, First = group.Min(rollup => rollup.BucketStart), Last = group.Max(rollup => rollup.BucketStart) })
            .ToDictionaryAsync(span => span.Key, StringComparer.Ordinal, cancellationToken);

        return [.. keys.Select(key =>
        {
            var span = spans.GetValueOrDefault(key);

            return new ProjectHistoryPartDto
            {
                Key = key,
                Title = NonBlank(reported.GetValueOrDefault(key)?.Title)
                        ?? periods.Where(period => period.CheckKey == key).Select(period => NonBlank(period.Title)).LastOrDefault(title => title is not null)
                        ?? key,
                ReportedFrom = span?.First,
                ReportedTo = reported.ContainsKey(key) ? null : span?.Last.AddMinutes(ProjectRollup.BucketMinutes)
            };
        })];
    }

    private static string? NonBlank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text;

    /// <summary>The open incidents and those of the last 90 days, newest first.</summary>
    public async Task<ProjectIncidentDto[]> GetIncidents(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindProject(projectId, cancellationToken);

        return await IncidentsOf(project, timeProvider.GetUtcNow().AddDays(-90), cancellationToken);
    }

    /// <summary>The project's open incidents and those that began since <paramref name="since"/>, open ones first, then newest first.</summary>
    public async Task<ProjectIncidentDto[]> IncidentsOf(Project project, DateTimeOffset since, CancellationToken cancellationToken)
    {
        var incidents = await dbContext.ProjectIncidents.AsNoTracking()
            .Where(incident => incident.ProjectId == project.Id && (incident.EndedAt == null || incident.StartedAt >= since))
            .OrderByDescending(incident => incident.EndedAt == null)
            .ThenByDescending(incident => incident.StartedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        Guid[] ids = [.. incidents.Select(incident => incident.Id)];

        var alerts = (await dbContext.ProjectAlerts.AsNoTracking()
            .Where(alert => alert.IncidentId != null && ids.Contains(alert.IncidentId.Value))
            .OrderByDescending(alert => alert.SentAt)
            .ToListAsync(cancellationToken))
            .ToLookup(alert => alert.IncidentId!.Value);

        var titles = await TitlesOf(project.Id, cancellationToken);

        return [.. incidents.Select(incident => ToDto(project, incident, titles, alerts[incident.Id]))];
    }

    /// <summary>
    /// "I'm on it" from <paramref name="name"/>, as the team and the update read it: stops the escalation and tells
    /// everyone already alerted who has it. Nothing changes for an incident that is over or that someone already took.
    /// </summary>
    public async Task<ProjectIncidentDto> Acknowledge(Guid id, Guid userId, string name, CancellationToken cancellationToken)
    {
        var incident = await dbContext.ProjectIncidents.Include(incident => incident.Project).FirstOrDefaultAsync(incident => incident.Id == id, cancellationToken)
            ?? throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

        if (incident.EndedAt is null && incident.AcknowledgedAt is null)
        {
            incident.AcknowledgedAt = timeProvider.GetUtcNow();
            incident.AcknowledgedByUserId = userId;
            incident.AcknowledgedBy = name;
            incident.NextEscalationAt = null;
            incident.Updates.Add(new()
            {
                At = incident.AcknowledgedAt.Value,
                Phase = ProjectIncidentPhase.Identified,
                Text = texts.InEveryLanguage(inLanguage => inLanguage[nameof(AppStrings.MonitoringIsOnItUpdate), name])
            });

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return await Reload(incident, cancellationToken);
    }

    /// <summary>An update the team writes, such as what they found, in the language it was written in and by <paramref name="author"/>.</summary>
    public async Task<ProjectIncidentDto> PostUpdate(ProjectIncidentUpdateRequestDto request, string author, CancellationToken cancellationToken)
    {
        var incident = await dbContext.ProjectIncidents.Include(incident => incident.Project).FirstOrDefaultAsync(incident => incident.Id == request.IncidentId, cancellationToken)
            ?? throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

        // bit Fabric closes the incidents it opened, once the checks pass.
        if (incident.IsAutomatic && request.Phase is ProjectIncidentPhase.Resolved)
            throw new BadRequestException(localizer[nameof(AppStrings.MonitoringAutomaticIncidentClosesByItself)]);

        incident.Version = request.Version;
        incident.Updates.Add(new()
        {
            At = timeProvider.GetUtcNow(),
            Phase = request.Phase,
            Author = author,
            // In the language it was written in; a reader in another one gets it as it is.
            Text = [new() { Culture = CultureInfo.CurrentUICulture.Name, Text = request.Text!.Trim() }]
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return await Reload(incident, cancellationToken);
    }

    public async Task<ProjectMaintenanceWindowDto[]> GetMaintenanceWindows(Guid projectId, CancellationToken cancellationToken)
    {
        return await dbContext.ProjectMaintenanceWindows.AsNoTracking()
            .Where(window => window.ProjectId == projectId)
            .OrderBy(window => window.StartsAt)
            .Select(window => new ProjectMaintenanceWindowDto
            {
                Id = window.Id,
                ProjectId = window.ProjectId,
                Title = window.Title,
                StartsAt = window.StartsAt,
                EndsAt = window.EndsAt,
                RepeatsWeekly = window.RepeatsWeekly,
                Version = window.Version
            })
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>A time the team works on the app: no alert goes out in it, and it doesn't count against the SLA.</summary>
    public async Task<ProjectMaintenanceWindowDto> AddMaintenanceWindow(ProjectMaintenanceWindowDto window, CancellationToken cancellationToken)
    {
        var project = await FindProject(window.ProjectId, cancellationToken);

        // A weekly window shorter than the week, so it ends before it comes again.
        if (window.EndsAt <= window.StartsAt || (window.RepeatsWeekly && window.EndsAt - window.StartsAt >= TimeSpan.FromDays(7)))
            throw new BadRequestException(localizer[nameof(AppStrings.MonitoringWindowEndsAfterItStarts)]);

        ProjectMaintenanceWindow added = new()
        {
            Id = Guid.CreateSequentialGuid(),
            ProjectId = project.Id,
            TenantId = project.TenantId,
            Title = window.Title!.Trim(),
            StartsAt = window.StartsAt.ToUniversalTime(),
            EndsAt = window.EndsAt.ToUniversalTime(),
            RepeatsWeekly = window.RepeatsWeekly
        };

        await dbContext.ProjectMaintenanceWindows.AddAsync(added, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        window.Id = added.Id;
        window.Version = added.Version;
        return window;
    }

    /// <summary>A project of the tenant, untracked; another team's is not found.</summary>
    public async Task<Project> FindProject(Guid projectId, CancellationToken cancellationToken)
        => await dbContext.Projects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken)
           ?? throw new ResourceNotFoundException(localizer[nameof(AppStrings.ResourceNotFoundException)]);

    /// <summary>The newest round that read the project's report, which names its parts; null before the first.</summary>
    public async Task<ProjectProbe?> LastReportOf(Guid projectId, CancellationToken cancellationToken)
    {
        return await dbContext.ProjectProbes.AsNoTracking()
            .Where(probe => probe.ProjectId == projectId && probe.Outcome == ProjectProbeOutcome.Read)
            .OrderByDescending(probe => probe.At)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>What the app calls each part, from the newest round that read its report.</summary>
    private async Task<Dictionary<string, string>> TitlesOf(Guid projectId, CancellationToken cancellationToken)
        => TitlesOf(await LastReportOf(projectId, cancellationToken));

    private static Dictionary<string, string> TitlesOf(ProjectProbe? lastReport)
    {
        return lastReport?.Checks.Where(check => check.Key is not null)
                                 .DistinctBy(check => check.Key)
                                 .ToDictionary(check => check.Key!, check => check.Title ?? check.Key!) ?? [];
    }

    private async Task<ProjectIncidentDto> Reload(ProjectIncident incident, CancellationToken cancellationToken)
    {
        var alerts = await dbContext.ProjectAlerts.AsNoTracking()
            .Where(alert => alert.IncidentId == incident.Id)
            .OrderByDescending(alert => alert.SentAt)
            .ToListAsync(cancellationToken);

        return ToDto(incident.Project!, incident, await TitlesOf(incident.ProjectId, cancellationToken), alerts);
    }

    public static ProjectIncidentDto ToDto(Project project, ProjectIncident incident, Dictionary<string, string> titles, IEnumerable<ProjectAlert> alerts)
    {
        var culture = CultureInfo.CurrentUICulture.Name;

        return new()
        {
            Id = incident.Id,
            ProjectId = incident.ProjectId,
            Kind = incident.Kind,
            Impact = incident.Impact,
            Title = ProjectTexts.In(incident.Title, culture),
            StartedAt = incident.StartedAt,
            EndedAt = incident.EndedAt,
            CheckKeys = incident.CheckKeys,
            CheckTitles = [.. incident.CheckKeys.Select(key => key == ProjectRollup.ReportKey ? project.Name! : titles.GetValueOrDefault(key, key))],
            IsAutomatic = incident.IsAutomatic,
            AfterDeployAt = incident.AfterDeployAt,
            DuringMaintenance = incident.DuringMaintenance,
            AcknowledgedBy = incident.AcknowledgedBy,
            AcknowledgedAt = incident.AcknowledgedAt,
            Updates = [.. incident.Updates.OrderByDescending(update => update.At).Select(update => new ProjectIncidentUpdateDto
            {
                At = update.At,
                Phase = update.Phase,
                Text = ProjectTexts.In(update.Text, culture),
                Author = update.Author
            })],
            Alerts = [.. alerts.Select(ToDto)],
            AgentPrompt = incident.EndedAt is null ? null : ProjectAlerter.AgentPromptOf(project, incident),
            Version = incident.Version
        };
    }

    public static ProjectAlertDto ToDto(ProjectAlert alert) => new()
    {
        SentAt = alert.SentAt,
        Kind = alert.Kind,
        Channel = alert.Channel,
        Target = alert.Target,
        Succeeded = alert.Succeeded,
        Problem = alert.Problem,
        ProblemStatusCode = alert.ProblemStatusCode
    };
}
