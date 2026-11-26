using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for bit Fabric's <see cref="IProjectScansController"/>: a sample project's scans, their past runs, "Run
/// scans now", which finishes a kind each time the tab looks again, the settings, and "This is wrong". Empty is a project
/// no scan has run on yet.
/// </summary>
public sealed class WorkbenchProjectScansController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IProjectScansController
{
    public Task<ProjectScansDto> GetScans(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = World.Get(monitoringId);
        World.MoveScansOn(project);

        return Load("scans", () => MonitoringSampleWorld.ScansOf(project),
            () => new ProjectScansDto { MonitoringId = monitoringId, Address = project.Address, Settings = project.ScanSettings }, cancellationToken);
    }

    public Task<ProjectScanDto[]> GetHistory(ProjectScanHistoryRequestDto request, CancellationToken cancellationToken)
        => Load<ProjectScanDto[]>("scan-history", () => [.. World.Get(request.MonitoringId).ScanRuns
                                                                 .Where(run => request.Kind is null || run.Kind == request.Kind)
                                                                 .Select(run => Copy(run))
                                                                 .Select(run => { run.Findings = []; return run; })], () => [], cancellationToken);

    public Task<ProjectScanDto> GetScan(Guid scanId, CancellationToken cancellationToken)
        => Load("scan", () => World.Projects.SelectMany(project => project.ScanRuns).FirstOrDefault(run => run.Id == scanId) ?? throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException),
            () => throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException), cancellationToken);

    public Task<ProjectScansDto> RunNow(RunProjectScansRequestDto request, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(request.MonitoringId);
        var now = World.Now;

        if (project.CanRunScansAgainAt > now)
            throw new BadRequestException();

        MonitoringSamples.AddPendingScans(project, now, now, ProjectScanTrigger.Manual, running: 0);
        project.CanRunScansAgainAt = now.AddMinutes(10);
        project.ScanLooks = 1;

        return MonitoringSampleWorld.ScansOf(project);
    }, cancellationToken);

    public Task<ProjectScanSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken)
        => Load("scan-settings", () => World.Get(monitoringId).ScanSettings, () => new ProjectScanSettingsDto { MonitoringId = monitoringId }, cancellationToken);

    public Task<ProjectScanSettingsDto> SaveSettings(ProjectScanSettingsDto settings, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(settings.MonitoringId);
        project.ScanSettings = Copy(settings);
        project.ScanSettings.Version++;

        return project.ScanSettings;
    }, cancellationToken);

    public Task ReportWrong(ReportScanFindingRequestDto request, CancellationToken cancellationToken) => Save(() =>
    {
        foreach (var finding in World.Projects.SelectMany(project => project.ScanRuns).SelectMany(run => run.Findings).Where(finding => finding.Id == request.FindingId))
        {
            finding.IsReportedWrong = true;
        }
    }, cancellationToken);
}
