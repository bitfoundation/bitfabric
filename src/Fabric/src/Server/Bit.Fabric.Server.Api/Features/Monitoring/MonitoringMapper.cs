using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// More info at src/Server/Bit.Fabric.Server.Api/Features/Mappers.md. A project is only ever read through these: it is
/// written by the connect flow and the probe job, never patched from a request.
/// </summary>
[Mapper]
public static partial class MonitoringMapper
{
    [MapperIgnoreTarget(nameof(ProjectDto.Uptime30Days))]
    [MapperIgnoreTarget(nameof(ProjectDto.MedianMs))]
    [MapperIgnoreTarget(nameof(ProjectDto.P95Ms))]
    [MapperIgnoreTarget(nameof(ProjectDto.ResponseLast24Hours))]
    [MapperIgnoreTarget(nameof(ProjectDto.FailingChecks))]
    [MapperIgnoreTarget(nameof(ProjectDto.HasCacheWarning))]
    [MapperIgnoreTarget(nameof(ProjectDto.OngoingIncidentId))]
    [MapperIgnoreTarget(nameof(ProjectDto.OngoingIncident))]
    [MapperIgnoreTarget(nameof(ProjectDto.OngoingIncidentSince))]
    [MapperIgnoreTarget(nameof(ProjectDto.Scans))]
    [MapperIgnoreSource(nameof(Project.TenantId))]
    [MapperIgnoreSource(nameof(Project.Tenant))]
    [MapperIgnoreSource(nameof(Project.HasStaticFrontEnd))]
    [MapperIgnoreSource(nameof(Project.Connection))]
    [MapperIgnoreSource(nameof(Project.TimeZoneId))]
    [MapperIgnoreSource(nameof(Project.IconSourceHash))]
    [MapperIgnoreSource(nameof(Project.IconReadAt))]
    [MapProperty(nameof(Project.ConnectionProblem), nameof(ProjectDto.ConnectionProblem), Use = nameof(DescribeConnectionProblem))]
    public static partial ProjectDto Map(this Project source);

    /// <summary>The stored code of a lost connection in the reader's language (See ProjectConnectionProblems).</summary>
    private static string? DescribeConnectionProblem(string? stored) => ProjectConnectionProblems.Describe(stored);

    [MapperIgnoreTarget(nameof(ProjectCheckDto.Since))]
    [MapperIgnoreTarget(nameof(ProjectCheckDto.Recent))]
    [MapperIgnoreSource(nameof(ProjectProbeCheck.IsReused))]
    public static partial ProjectCheckDto Map(this ProjectProbeCheck source);

    [MapperIgnoreTarget(nameof(ProjectScanDto.UrgentCount))]
    [MapperIgnoreTarget(nameof(ProjectScanDto.ImportantCount))]
    [MapperIgnoreTarget(nameof(ProjectScanDto.ImprovementCount))]
    [MapperIgnoreTarget(nameof(ProjectScanDto.Findings))]
    [MapperIgnoreSource(nameof(ProjectScan.Project))]
    [MapperIgnoreSource(nameof(ProjectScan.TenantId))]
    [MapperIgnoreSource(nameof(ProjectScan.Tenant))]
    [MapperIgnoreSource(nameof(ProjectScan.RequestedByUserId))]
    [MapperIgnoreSource(nameof(ProjectScan.RequestedBy))]
    [MapperIgnoreSource(nameof(ProjectScan.Findings))]
    public static partial ProjectScanDto Map(this ProjectScan source);

    [MapperIgnoreTarget(nameof(ProjectScanFindingDto.IsReportedWrong))]
    [MapperIgnoreSource(nameof(ProjectScanFinding.ScanId))]
    [MapperIgnoreSource(nameof(ProjectScanFinding.Scan))]
    [MapperIgnoreSource(nameof(ProjectScanFinding.TenantId))]
    [MapperIgnoreSource(nameof(ProjectScanFinding.Tenant))]
    public static partial ProjectScanFindingDto Map(this ProjectScanFinding source);
}
