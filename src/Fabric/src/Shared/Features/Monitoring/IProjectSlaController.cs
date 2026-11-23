using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// A project's SLA (bitfabric-internal/specs/monitoring.md, SLA reports): the owner's target, weights and credits, and a
/// month's report, all through the current tenant.
/// </summary>
[Route("api/v1/[controller]/[action]/"), AuthorizedApi]
public interface IProjectSlaController : IAppController
{
    /// <summary>
    /// A month as CSV, at <c>{ExportCsvUri}/{projectId}/{year}/{month}</c>. It downloads a file, so it is called with the
    /// HttpClient directly rather than through the generated proxy, which speaks json only.
    /// </summary>
    public const string ExportCsvUri = "api/v1/ProjectSla/ExportCsv";

    /// <summary>A month as a PDF in the viewer's language, at <c>{ExportPdfUri}/{projectId}/{year}/{month}</c>; See <see cref="ExportCsvUri"/>.</summary>
    public const string ExportPdfUri = "api/v1/ProjectSla/ExportPdf";

    /// <summary>The project's target, weights and credits; the defaults until the owner saves theirs.</summary>
    [HttpGet("{projectId}")]
    Task<ProjectSlaSettingsDto> GetSettings(Guid projectId, CancellationToken cancellationToken);

    [HttpPost]
    Task<ProjectSlaSettingsDto> SaveSettings(ProjectSlaSettingsDto settings, CancellationToken cancellationToken);

    /// <summary>A month of the project's time zone, this one when the request names none, with the last 12 months' figures.</summary>
    [HttpPost]
    Task<ProjectSlaReportDto> GetReport(ProjectSlaRequestDto request, CancellationToken cancellationToken);
}
