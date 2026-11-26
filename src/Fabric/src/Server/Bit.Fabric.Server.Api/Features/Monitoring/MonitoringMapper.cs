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
    [MapperIgnoreTarget(nameof(MonitoringDto.Uptime30Days))]
    [MapperIgnoreTarget(nameof(MonitoringDto.MedianMs))]
    [MapperIgnoreTarget(nameof(MonitoringDto.P95Ms))]
    [MapperIgnoreTarget(nameof(MonitoringDto.ResponseLast24Hours))]
    [MapperIgnoreTarget(nameof(MonitoringDto.FailingChecks))]
    [MapperIgnoreTarget(nameof(MonitoringDto.HasCacheWarning))]
    [MapperIgnoreTarget(nameof(MonitoringDto.OngoingIncidentId))]
    [MapperIgnoreTarget(nameof(MonitoringDto.OngoingIncident))]
    [MapperIgnoreTarget(nameof(MonitoringDto.OngoingIncidentSince))]
    [MapperIgnoreTarget(nameof(MonitoringDto.Scans))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.TenantId))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.Tenant))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.HasStaticFrontEnd))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.Connection))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.TimeZoneId))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.IconSourceHash))]
    [MapperIgnoreSource(nameof(ProjectMonitoring.IconReadAt))]
    [MapProperty(nameof(ProjectMonitoring.ConnectionProblem), nameof(MonitoringDto.ConnectionProblem), Use = nameof(DescribeConnectionProblem))]
    public static partial MonitoringDto Map(this ProjectMonitoring source);

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
    [MapperIgnoreSource(nameof(ProjectScan.Monitoring))]
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
