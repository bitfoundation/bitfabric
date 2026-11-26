using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// A project's scans (bitfabric-internal/specs/scanners.md): the latest of each kind with what it found, past runs, running
/// them by hand, when bit Fabric runs them, and a finding someone of the team says is wrong, all through the current tenant.
/// </summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IProjectScansController : IAppController
{
    /// <summary>The newest run of each kind, with its findings, and the project's scan settings.</summary>
    [HttpGet("{monitoringId}")]
    Task<ProjectScansDto> GetScans(Guid monitoringId, CancellationToken cancellationToken);

    /// <summary>Past runs, newest first, without their findings.</summary>
    [HttpPost]
    Task<ProjectScanDto[]> GetHistory(ProjectScanHistoryRequestDto request, CancellationToken cancellationToken);

    /// <summary>One run with its findings.</summary>
    [HttpGet("{scanId}")]
    Task<ProjectScanDto> GetScan(Guid scanId, CancellationToken cancellationToken);

    /// <summary>"Run scans now": every kind at once, at most once every 10 minutes a project.</summary>
    [HttpPost]
    Task<ProjectScansDto> RunNow(RunProjectScansRequestDto request, CancellationToken cancellationToken);

    [HttpGet("{monitoringId}")]
    Task<ProjectScanSettingsDto> GetSettings(Guid monitoringId, CancellationToken cancellationToken);

    [HttpPost]
    Task<ProjectScanSettingsDto> SaveSettings(ProjectScanSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>"This is wrong" on a finding: who, when and which finding is recorded, a guard metric (bitfabric-internal/specs/product.md).</summary>
    [HttpPost]
    Task ReportWrong(ReportScanFindingRequestDto request, CancellationToken cancellationToken);
}
