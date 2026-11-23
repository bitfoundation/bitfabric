using System.Globalization;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for bit Fabric's <see cref="IProjectsController"/>: the team's sample projects, the add flow's address
/// cases and approvals, and a project's locations, disconnection and removal. Empty is a team with no project yet; the
/// projects page's Projects switch picks the eight in every state or an agency's fourteen.
/// </summary>
public sealed class WorkbenchProjectsController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider, TimeZoneService timeZoneService)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IProjectsController
{
    /// <summary>The projects page's switch that picks how many projects the team has.</summary>
    public const string AmountSwitch = "amount";

    public Task<List<ProjectDto>> GetProjects(string? timeZone, CancellationToken cancellationToken)
    {
        var zone = MonitoringReads.ZoneOf(timeZone);
        var many = ScenarioState.Scenario.GetSwitch(AmountSwitch) is "many";

        return Load<List<ProjectDto>>("projects", () => World.IsNewTeam
            ? []
            : World.Projects.Where(project => many || MonitoringSamples.AgencyKeys.Contains(project.Key) is false)
                            .Select(project => MonitoringReads.Project(project, zone, World.Now))
                            .OrderBy(Rank)
                            .ToList(), () => [], cancellationToken);
    }

    /// <summary>Failing ones first, as the server lists them.</summary>
    private static int Rank(ProjectDto project) => (project.ConnectionStatus, project.Health) switch
    {
        (_, ProjectHealth.Down) => 0,
        (_, ProjectHealth.Degraded) => 1,
        (ProjectConnectionStatus.ReconnectNeeded, _) => 2,
        (ProjectConnectionStatus.FirstChecks, _) => 3,
        (ProjectConnectionStatus.Disconnected, _) => 5,
        _ => 4
    };

    public async Task<ProjectDetailsDto> GetProject(Guid id, CancellationToken cancellationToken)
    {
        var details = await Load<ProjectDetailsDto>("project", () => MonitoringReads.Details(World.Get(id), timeZoneService.CurrentTimeZone, World.Now),
            () => throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException), cancellationToken);

        // The first checks come in one location at a time, each time the page looks again.
        if (World.Projects.FirstOrDefault(project => project.Id == id) is { } read)
        {
            World.RevealNextReading(read);
        }

        return details;
    }

    public Task<ProjectPerformanceDto> GetPerformance(ProjectPerformanceRequestDto request, CancellationToken cancellationToken)
        => Load("performance", () => MonitoringReads.Performance(World.Get(request.ProjectId), request, World.Now), () => new ProjectPerformanceDto { StepMinutes = request.StepMinutes }, cancellationToken);

    public Task<AddressProbeResultDto> ProbeAddress(ProbeAddressRequestDto request, CancellationToken cancellationToken)
    {
        // Counted as one load, its wait included, so a screenshot shows what was found.
        return ScenarioState.Track(Probe());

        async Task<AddressProbeResultDto> Probe()
        {
            // Longer than a page load: it reads the address, its well-known file, its manifest and its server.
            await Task.Delay(700, cancellationToken);

            return await Load("probe", () => MonitoringSamples.Probe(request.Address, World.Projects),
                () => new AddressProbeResultDto { Outcome = AddressProbeOutcome.NotFound, Address = $"https://{MonitoringSamples.HostOf(request.Address)}" }, cancellationToken);
        }
    }

    public Task<StartConnectResponseDto> StartConnect(StartConnectRequestDto request, CancellationToken cancellationToken) => Save(() =>
    {
        var found = MonitoringSamples.Probe(request.Address, World.Projects);

        if (found.Outcome is not AddressProbeOutcome.Found)
            throw new BadRequestException(AppStrings.MonitoringHealthReportNotFoundAtAddress);

        return new StartConnectResponseDto { AuthorizeUrl = World.BeginApproval(found, request.Name) };
    }, cancellationToken);

    public Task<ProjectDto> CompleteConnect(CompleteConnectRequestDto request, CancellationToken cancellationToken)
    {
        // A load, so a view in Loading or Slow shows the wait for the first report.
        return Load<ProjectDto>("connect", () =>
        {
            if (request.Error is not null || request.Code is null)
                throw new BadRequestException(request.Error is "access_denied"
                    ? AppStrings.MonitoringApprovalDeclined
                    : string.Format(CultureInfo.CurrentCulture, AppStrings.MonitoringAppDidNotApprove, request.Error ?? "no code"));

            return MonitoringReads.Project(World.CompleteApproval(request.State, null), timeZoneService.CurrentTimeZone, World.Now);
        }, () => throw new BadRequestException(AppStrings.MonitoringApprovalExpired), cancellationToken);
    }

    public Task<ProjectDto> SetLocations(SetProjectLocationsRequestDto request, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(request.ProjectId);

        if (request.Locations.Any(code => NetworkLocations.Find(code) is null))
            throw new BadRequestException(AppStrings.MonitoringNoSuchNetworkLocation);

        project.Locations = [.. NetworkLocations.All.Select(location => location.Code).Where(request.Locations.Contains)];
        project.Version++;

        return MonitoringReads.Project(project, timeZoneService.CurrentTimeZone, World.Now);
    }, cancellationToken);

    public Task<ProjectDto> Disconnect(Guid id, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(id);
        project.ConnectionStatus = ProjectConnectionStatus.Disconnected;
        project.ConnectionProblem = null;
        project.Version++;

        return MonitoringReads.Project(project, timeZoneService.CurrentTimeZone, World.Now);
    }, cancellationToken);

    public Task Delete(Guid id, CancellationToken cancellationToken) => Save(() => { World.Projects.Remove(World.Get(id)); }, cancellationToken);
}
