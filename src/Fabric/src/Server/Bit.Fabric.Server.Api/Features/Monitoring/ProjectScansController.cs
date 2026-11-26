using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// A project's Scans tab (bitfabric-internal/specs/scanners.md; the plan's PR 8). Every query goes through the tenant
/// filter, so another team's project and its scans are simply not found.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class ProjectScansController : AppControllerBase, IProjectScansController
{
    private const int HistoryLength = 50;

    [AutoInject] private ProjectScanReader reader = default!;
    [AutoInject] private ProjectScanScheduler scheduler = default!;
    [AutoInject] private ILogger<ProjectScansController> logger = default!;

    [HttpGet("{monitoringId}")]
    public async Task<ProjectScansDto> GetScans(Guid monitoringId, CancellationToken cancellationToken)
        => await reader.Latest(await FindProject(monitoringId, cancellationToken), cancellationToken);

    [HttpPost]
    public async Task<ProjectScanDto[]> GetHistory(ProjectScanHistoryRequestDto request, CancellationToken cancellationToken)
        => await reader.History((await FindProject(request.MonitoringId, cancellationToken)).Id, request.Kind, HistoryLength, cancellationToken);

    [HttpGet("{scanId}")]
    public async Task<ProjectScanDto> GetScan(Guid scanId, CancellationToken cancellationToken)
        => await reader.Scan(scanId, cancellationToken) ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

    [HttpPost]
    public async Task<ProjectScansDto> RunNow(RunProjectScansRequestDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.MonitoringId, cancellationToken);

        if (await scheduler.RunByHand(project, User.GetUserId(), cancellationToken) is { } tryAgainIn)
            throw new TooManyRequestsException(Localizer[nameof(AppStrings.MonitoringScansRunNowLimit), (int)ProjectScanReader.RunNowInterval.TotalMinutes, (int)Math.Ceiling(tryAgainIn.TotalMinutes)])
                .WithExtensionData("TryAgainIn", tryAgainIn);

        return await reader.Latest(project, cancellationToken);
    }

    [HttpGet("{monitoringId}")]
    public async Task<ProjectScanSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken)
        => await reader.Settings((await FindProject(monitoringId, cancellationToken)).Id, cancellationToken);

    [HttpPost]
    public async Task<ProjectScanSettingsDto> SaveSettings(ProjectScanSettingsDto settings, CancellationToken cancellationToken)
    {
        var project = await FindProject(settings.MonitoringId, cancellationToken);

        return await reader.SaveSettings(project, settings, cancellationToken);
    }

    [HttpPost]
    public async Task ReportWrong(ReportScanFindingRequestDto request, CancellationToken cancellationToken)
    {
        var finding = await DbContext.ProjectScanFindings
            .Where(finding => finding.Id == request.FindingId)
            .Select(finding => new { finding.Id, finding.Key, finding.Where, finding.Scan!.MonitoringId, finding.Scan.Kind, finding.TenantId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        var userId = User.GetUserId();

        // Once a member: saying it twice is still one report.
        if (await DbContext.ProjectScanFindingReports.AnyAsync(report => report.FindingId == finding.Id && report.UserId == userId, cancellationToken))
            return;

        await DbContext.ProjectScanFindingReports.AddAsync(new()
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = finding.MonitoringId,
            TenantId = finding.TenantId,
            FindingId = finding.Id,
            Kind = finding.Kind,
            FindingKey = finding.Key,
            Where = finding.Where,
            UserId = userId,
            ReportedAt = TimeProvider.GetUtcNow(),
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
        }, cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning("A finding was reported as wrong: {FindingKey} of a {Kind} scan of project {MonitoringId}.", finding.Key, finding.Kind, finding.MonitoringId);
    }

    private async Task<ProjectMonitoring> FindProject(Guid monitoringId, CancellationToken cancellationToken)
        => await DbContext.Monitorings.AsNoTracking().FirstOrDefaultAsync(project => project.Id == monitoringId, cancellationToken)
           ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);
}
