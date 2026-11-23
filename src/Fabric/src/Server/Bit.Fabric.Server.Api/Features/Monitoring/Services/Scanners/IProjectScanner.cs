using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// One kind of scan (bitfabric-internal/specs/scanners.md). It reads only what any visitor of the app sees, from the
/// project's address and its server's, through the monitoring http client and its public-address guard, and never signs
/// in or tries a password. One kind runs in a job of its own, so a scanner that fails never stops the others (See
/// ProjectScanJobRunner).
/// </summary>
public interface IProjectScanner
{
    ProjectScanKind Kind { get; }

    /// <summary>Whether this server can run it: the browser scans need the Lighthouse service's address.</summary>
    bool IsAvailable => true;

    /// <summary>
    /// It loads pages in the Lighthouse service's Chrome, so it takes a slot of <see cref="MonitoringOptions.BrowserScanParallelism"/>
    /// rather than <see cref="MonitoringOptions.ScanParallelism"/>, and has <see cref="MonitoringOptions.BrowserScanTimeout"/>.
    /// </summary>
    bool UsesBrowser => false;

    /// <summary>Scans the project for the scan written down when it was asked for, which says when and why.</summary>
    Task<ProjectScanResult> Scan(Project project, ProjectScan scan, CancellationToken cancellationToken);
}

/// <summary>What a scanner found and how it judges the app.</summary>
public sealed class ProjectScanResult
{
    public ProjectScanOutcome Outcome { get; init; } = ProjectScanOutcome.Completed;

    public ProjectScanVerdict? Verdict { get; set; }

    public int? Score { get; set; }

    public string? Grade { get; set; }

    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>The pages and devices a browser scan measured.</summary>
    public List<ProjectScanPage> Pages { get; } = [];

    public List<ScanFindingResult> Findings { get; } = [];

    public static ProjectScanResult Unreachable() => new() { Outcome = ProjectScanOutcome.Unreachable };

    /// <summary>
    /// Poor with anything urgent or a score below <paramref name="poorBelow"/>, needs work with anything important or a
    /// score below <paramref name="goodFrom"/>, good otherwise.
    /// </summary>
    public void Judge(int goodFrom, int poorBelow)
    {
        Verdict = Findings.Any(finding => finding.Severity is ScanFindingSeverity.Urgent) || Score < poorBelow ? ProjectScanVerdict.Poor
            : Findings.Any(finding => finding.Severity is ScanFindingSeverity.Important) || Score < goodFrom ? ProjectScanVerdict.NeedsWork
            : ProjectScanVerdict.Good;
    }
}

/// <summary>A finding: one of ScanFindings' keys, its severity, the values its texts are formatted with, and where it was seen.</summary>
public sealed record ScanFindingResult(string Key, ScanFindingSeverity Severity, string[] Parameters, string[] Where);
