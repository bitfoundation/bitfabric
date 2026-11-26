using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for bit Fabric's <see cref="IProjectSlaController"/>: a sample project's promise, weights and credits, and
/// a month's report counted from its history. Empty is a month nothing was counted in yet.
/// </summary>
public sealed class WorkbenchProjectSlaController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IProjectSlaController
{
    public Task<ProjectSlaSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken)
        => Load("sla-settings", () => World.Get(monitoringId).Sla, () => new ProjectSlaSettingsDto { MonitoringId = monitoringId }, cancellationToken);

    public Task<ProjectSlaSettingsDto> SaveSettings(ProjectSlaSettingsDto settings, CancellationToken cancellationToken) => Save(() =>
    {
        if (settings.TargetPercent is <= 0 or > 100 || settings.PartialWeight is < 0 or > 1 || settings.DegradedWeight is < 0 or > 1
            || settings.Credits.Any(credit => credit.BelowPercent is <= 0 or > 100 || credit.CreditPercent is <= 0 or > 100))
            throw new BadRequestException();

        var project = World.Get(settings.MonitoringId);
        project.Sla = Copy(settings);
        project.Sla.Credits = [.. project.Sla.Credits.OrderByDescending(credit => credit.BelowPercent)];
        project.Sla.Version++;

        return project.Sla;
    }, cancellationToken);

    public Task<ProjectSlaReportDto> GetReport(ProjectSlaRequestDto request, CancellationToken cancellationToken)
    {
        return Load("sla-report", () => MonitoringReads.SlaReport(World.Get(request.MonitoringId), request.Year, request.Month, World.Now), () =>
        {
            // A month nothing was counted in: the project was only just connected.
            var report = MonitoringReads.SlaReport(World.Get(request.MonitoringId), request.Year, request.Month, World.Now);
            report.App = new() { Title = report.ProjectName };
            report.Parts = [];
            report.Months = [.. report.Months.Select(month => new ProjectSlaMonthDto { Year = month.Year, Month = month.Month, IsCurrent = month.IsCurrent })];
            report.Maintenance = [];
            report.BudgetMinutes = report.LeftMinutes = report.MaintenanceMinutes = report.NoDataMinutes = 0;
            report.Credit = null;
            return report;
        }, cancellationToken);
    }
}
