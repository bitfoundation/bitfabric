using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The sample world of one view (a circuit): a copy of the sample projects that the monitoring pages change as a team
/// would, so "I'm on it", an update, saved settings, a maintenance window, picked locations or a disconnection show at
/// once. A reload starts again from the samples.
/// </summary>
public sealed class MonitoringSampleWorld
{
    /// <summary>The <c>state</c> of the approval the add page's "back from the approval" cases come back with.</summary>
    public const string SampleApprovalState = "workbench-sample-approval";

    private List<SampleProject>? projects;
    private readonly Dictionary<string, (AddressProbeResultDto Found, string? Name)> approvals = [];

    /// <summary>The projects, in the order the server lists them; created on first use.</summary>
    public List<SampleProject> Projects => projects ??= MonitoringSamples.Create(MonitoringSamples.Now());

    /// <summary>A team created on the projects page: it has no project yet.</summary>
    public bool IsNewTeam { get; set; }

    /// <summary>Raised when the projects page creates a team, so its host shows the new team.</summary>
    public event Action? TeamCreated;

    public DateTimeOffset Now => DateTimeOffset.UtcNow;

    public SampleProject Get(Guid id)
        => Projects.FirstOrDefault(project => project.Id == id) ?? throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException);

    public SampleProject? Find(string key) => Projects.FirstOrDefault(project => project.Key == key);

    public SampleProject? FindBySlug(string? slug) => Projects.FirstOrDefault(project => string.Equals(project.StatusPage.Slug, slug, StringComparison.OrdinalIgnoreCase));

    public SampleIncident GetIncident(Guid id, out SampleProject project)
    {
        foreach (var candidate in Projects)
        {
            if (candidate.Incidents.FirstOrDefault(incident => incident.Id == id) is { } incident)
            {
                project = candidate;
                return incident;
            }
        }

        throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException);
    }

    public void CreateTeam()
    {
        IsNewTeam = true;
        TeamCreated?.Invoke();
    }

    /// <summary>Starts an approval in the customer's app: the address of its sample approval page, with the state it comes back with.</summary>
    public string BeginApproval(AddressProbeResultDto found, string? name)
    {
        var state = Guid.NewGuid().ToString("N");
        approvals[state] = (found, name);

        return $"{found.ServerAddress ?? found.Address}/api/v1/OAuth/Authorize?response_type=code&client_id=bit-fabric&scope=healthz&state={state}";
    }

    /// <summary>The state of one of this world's approvals in an approval page's address; null for any other address.</summary>
    public string? ApprovalStateOf(string url)
    {
        var query = Uri.TryCreate(url, UriKind.Absolute, out var uri) ? Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query) : [];

        return query.TryGetValue("state", out var state) && approvals.ContainsKey(state.ToString()) ? state.ToString() : null;
    }

    /// <summary>
    /// The approval came back with a code: the app it was asked of is added, or reconnected when it is watched already. The
    /// sample state of the add page's cases stands for Taskbloom.
    /// </summary>
    public SampleProject CompleteApproval(string? state, string? name)
    {
        if (state is null || (approvals.Remove(state, out var approval) is false && state != SampleApprovalState))
            throw new BadRequestException(AppStrings.MonitoringApprovalExpired);

        var found = approval.Found ?? MonitoringSamples.Probe("taskbloom.app", Projects);
        var now = MonitoringSamples.Now();

        if (Projects.FirstOrDefault(project => project.Address == found.Address) is { } existing)
        {
            existing.ConnectionStatus = ProjectConnectionStatus.Connected;
            existing.ConnectionProblem = null;
            existing.CheckedAt = now;
            existing.ApprovedOn = now;
            existing.Version++;
            return existing;
        }

        var added = MonitoringSamples.NewlyConnected(now, found, name ?? approval.Name);
        Projects.Insert(0, added);
        IsNewTeam = false;

        return added;
    }

    /// <summary>
    /// The Scans tab looked again while scans waited or ran: from the second look on, the running one finishes and the next
    /// one starts, so "Run scans now" ends in results a few looks later.
    /// </summary>
    public void MoveScansOn(SampleProject project)
    {
        if (project.PendingScans.Count is 0)
            return;

        if (project.ScanLooks++ is 0)
            return;

        var now = DateTimeOffset.UtcNow;

        if (project.PendingScans.FirstOrDefault(scan => scan.StartedAt is not null) is { } running)
        {
            var version = running.Trigger is ProjectScanTrigger.Deployment ? project.ApplicationVersion : null;
            project.ScanRuns.Insert(0, MonitoringSamples.Scan(project, running.Kind, running.Trigger, now, ProjectScanOutcome.Completed, project.ScanProfile, 0, version, project.ScanRuns.Count));
            project.PendingScans.Remove(running);
        }

        if (project.PendingScans.FirstOrDefault(scan => scan.StartedAt is null) is { } queued)
        {
            queued.StartedAt = now;
        }

        if (project.PendingScans.Count is 0)
        {
            project.ScanLooks = 0;
        }
    }

    /// <summary>Reveals the next location's reading of a project whose first checks are running, one each time it is read.</summary>
    public void RevealNextReading(SampleProject project)
    {
        if (project.ReadingsIn is { } readingsIn && readingsIn < project.Readings.Count)
        {
            project.ReadingsIn = readingsIn + 1;
        }
    }

    /// <summary>The scans of a project, as its Scans tab shows them.</summary>
    public static ProjectScansDto ScansOf(SampleProject project) => new()
    {
        ProjectId = project.Id,
        Address = project.Address,
        Latest = [.. ScanFindings.AvailableKinds.Select(kind => project.ScanRuns.FirstOrDefault(run => run.Kind == kind)).OfType<ProjectScanDto>()],
        Pending = [.. project.PendingScans],
        Settings = project.ScanSettings,
        CanRunAgainAt = project.CanRunScansAgainAt > DateTimeOffset.UtcNow ? project.CanRunScansAgainAt : null
    };
}
