using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// A project's SLA tab and its exports (bitfabric-internal/specs/monitoring.md, SLA reports; the plan's PR 5). Every query
/// goes through the tenant filter, so another team's project is simply not found.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class ProjectSlaController : AppControllerBase, IProjectSlaController
{
    /// <summary>More tiers than any contract has; it keeps a report's credit table on its page.</summary>
    private const int MaxCredits = 10;

    [AutoInject] private ProjectSlaReports reports = default!;

    [HttpGet("{monitoringId}")]
    public async Task<ProjectSlaSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken)
    {
        var project = await FindProject(monitoringId, cancellationToken);

        return await reports.SettingsOf(project.Id, cancellationToken);
    }

    [HttpPost]
    public async Task<ProjectSlaSettingsDto> SaveSettings(ProjectSlaSettingsDto settings, CancellationToken cancellationToken)
    {
        var project = await FindProject(settings.MonitoringId, cancellationToken);

        if (settings.TargetPercent is <= 0 or >= 100 || double.IsNaN(settings.TargetPercent))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringSlaInvalidTarget)]);

        if (settings.PartialWeight is < 0 or > 1 || settings.DegradedWeight is < 0 or > 1 || double.IsNaN(settings.PartialWeight) || double.IsNaN(settings.DegradedWeight))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringSlaInvalidWeight)]);

        if (settings.Credits.Length > MaxCredits
            || settings.Credits.Any(credit => credit.BelowPercent is <= 0 or > 100 || credit.CreditPercent is <= 0 or > 100 || double.IsNaN(credit.BelowPercent) || double.IsNaN(credit.CreditPercent))
            || settings.Credits.DistinctBy(credit => credit.BelowPercent).Count() != settings.Credits.Length)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringSlaInvalidCredits), MaxCredits]);

        var saved = await DbContext.ProjectSlaSettings.FirstOrDefaultAsync(row => row.MonitoringId == project.Id, cancellationToken);

        if (saved is null)
        {
            saved = new() { MonitoringId = project.Id, TenantId = project.TenantId };
            await DbContext.ProjectSlaSettings.AddAsync(saved, cancellationToken);
        }
        else
        {
            saved.Version = settings.Version;
        }

        saved.TargetPercent = settings.TargetPercent;
        saved.PartialWeight = settings.PartialWeight;
        saved.DegradedWeight = settings.DegradedWeight;
        saved.Credits = [.. settings.Credits.OrderByDescending(credit => credit.BelowPercent).Select(credit => new ProjectSlaCredit { BelowPercent = credit.BelowPercent, CreditPercent = credit.CreditPercent })];

        await DbContext.SaveChangesAsync(cancellationToken);

        return await reports.SettingsOf(project.Id, cancellationToken);
    }

    [HttpPost]
    public async Task<ProjectSlaReportDto> GetReport(ProjectSlaRequestDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.MonitoringId, cancellationToken);

        return await reports.Build(project, request.Year, request.Month, cancellationToken);
    }

    /// <inheritdoc cref="IProjectSlaController.ExportCsvUri"/>
    [HttpGet("{monitoringId}/{year:int}/{month:int}")]
    public async Task<IActionResult> ExportCsv(Guid monitoringId, int year, int month, CancellationToken cancellationToken)
    {
        var report = await reports.Build(await FindProject(monitoringId, cancellationToken), year, month, cancellationToken);

        return File(ProjectSlaReports.Csv(report), "text/csv; charset=utf-8", ProjectSlaReports.FileNameOf(report, "csv"));
    }

    /// <inheritdoc cref="IProjectSlaController.ExportPdfUri"/>
    [HttpGet("{monitoringId}/{year:int}/{month:int}")]
    public async Task<IActionResult> ExportPdf(Guid monitoringId, int year, int month, CancellationToken cancellationToken)
    {
        var report = await reports.Build(await FindProject(monitoringId, cancellationToken), year, month, cancellationToken);

        return File(reports.Pdf(report), "application/pdf", ProjectSlaReports.FileNameOf(report, "pdf"));
    }

    private async Task<ProjectMonitoring> FindProject(Guid monitoringId, CancellationToken cancellationToken)
        => await DbContext.Monitorings.AsNoTracking().FirstOrDefaultAsync(project => project.Id == monitoringId, cancellationToken)
           ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);
}
