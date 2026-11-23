using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>How a scan and its findings read, on a project's Scans tab and on its card alike.</summary>
public static class ProjectScanWords
{
    public static string KindName(IStringLocalizer localizer, ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Security => localizer[nameof(AppStrings.MonitoringScanSecurity)],
        ProjectScanKind.Seo => localizer[nameof(AppStrings.MonitoringScanSeo)],
        ProjectScanKind.DomainAndCertificate => localizer[nameof(AppStrings.MonitoringScanDomainAndCertificate)],
        ProjectScanKind.Performance => localizer[nameof(AppStrings.MonitoringScanPerformance)],
        _ => localizer[nameof(AppStrings.MonitoringScanAccessibility)]
    };

    public static string KindAbout(IStringLocalizer localizer, ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Security => localizer[nameof(AppStrings.MonitoringScanSecurityAbout)],
        ProjectScanKind.Seo => localizer[nameof(AppStrings.MonitoringScanSeoAbout)],
        ProjectScanKind.Performance => localizer[nameof(AppStrings.MonitoringScanPerformanceAbout)],
        ProjectScanKind.Accessibility => localizer[nameof(AppStrings.MonitoringScanAccessibilityAbout)],
        _ => localizer[nameof(AppStrings.MonitoringScanDomainAndCertificateAbout)]
    };

    /// <summary>The icons the approved card design gives each kind.</summary>
    public static string KindIcon(ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Security => BitIconName.Shield,
        ProjectScanKind.Performance => BitIconName.SpeedHigh,
        ProjectScanKind.Seo => BitIconName.Search,
        ProjectScanKind.Accessibility => BitIconName.AccessibilityChecker,
        _ => BitIconName.Certificate
    };

    public static string SeverityName(IStringLocalizer localizer, ScanFindingSeverity severity) => severity switch
    {
        ScanFindingSeverity.Urgent => localizer[nameof(AppStrings.MonitoringScanUrgent)],
        ScanFindingSeverity.Important => localizer[nameof(AppStrings.MonitoringScanImportant)],
        _ => localizer[nameof(AppStrings.MonitoringScanImprovement)]
    };

    public static BitColor SeverityColor(ScanFindingSeverity severity) => severity switch
    {
        ScanFindingSeverity.Urgent => BitColor.Error,
        ScanFindingSeverity.Important => BitColor.Warning,
        _ => BitColor.Info
    };

    public static string SeverityIcon(ScanFindingSeverity severity) => severity switch
    {
        ScanFindingSeverity.Urgent => BitIconName.StatusErrorFull,
        ScanFindingSeverity.Important => BitIconName.WarningSolid,
        _ => BitIconName.Lightbulb
    };

    /// <summary>The tone a surface takes from a scan: the monitoring pages' tones (See _monitoring.scss).</summary>
    public static string Tone(ProjectScanOutcome? outcome, ProjectScanVerdict? verdict) => (outcome, verdict) switch
    {
        (ProjectScanOutcome.Completed, ProjectScanVerdict.Good) => "tone-ok",
        (ProjectScanOutcome.Completed, ProjectScanVerdict.NeedsWork) => "tone-degraded",
        (ProjectScanOutcome.Completed, ProjectScanVerdict.Poor) => "tone-down",
        _ => "tone-nodata"
    };

    /// <summary>A scan's figure: Security's grade, SEO's score, or the days until the first certificate or domain ends.</summary>
    public static string? Value(IStringLocalizer localizer, ProjectScanKind kind, ProjectScanOutcome? outcome, int? score, string? grade, DateTimeOffset? expiresAt, DateTimeOffset now)
    {
        if (outcome is not ProjectScanOutcome.Completed)
            return null;

        return kind switch
        {
            ProjectScanKind.Security => grade,
            ProjectScanKind.DomainAndCertificate => expiresAt is { } at
                ? at <= now ? localizer[nameof(AppStrings.MonitoringScanExpired)] : localizer[nameof(AppStrings.MonitoringDurationDays), (int)Math.Floor((at - now).TotalDays)]
                : "—",
            _ => score?.ToString(CultureInfo.CurrentCulture)
        };
    }

    public static string? Value(IStringLocalizer localizer, ProjectScanDto scan, DateTimeOffset now)
        => Value(localizer, scan.Kind, scan.Outcome, scan.Score, scan.Grade, scan.ExpiresAt, now);

    public static string? Value(IStringLocalizer localizer, ProjectScanSummaryDto scan, DateTimeOffset now)
        => Value(localizer, scan.Kind, scan.Outcome, scan.Score, scan.Grade, scan.ExpiresAt, now);

    /// <summary>Why a scan ran, in words.</summary>
    public static string Trigger(IStringLocalizer localizer, ProjectScanDto scan) => scan.Trigger switch
    {
        ProjectScanTrigger.Deployment => scan.ApplicationVersion is { Length: > 0 } version
            ? localizer[nameof(AppStrings.MonitoringScanTriggerDeployment), ShortVersion(version)]
            : localizer[nameof(AppStrings.MonitoringScanTriggerDeploymentNoVersion)],
        ProjectScanTrigger.Weekly => localizer[nameof(AppStrings.MonitoringScanTriggerWeekly)],
        ProjectScanTrigger.Manual => localizer[nameof(AppStrings.MonitoringScanTriggerManual)],
        _ => localizer[nameof(AppStrings.MonitoringScanTriggerConnected)]
    };

    /// <summary>A build as people say it: the commit after the '+', shortened, or the version as it is.</summary>
    public static string ShortVersion(string version)
        => version.Split('+', 2) is [_, var commit] && commit.Length > 0 ? commit[..Math.Min(7, commit.Length)] : version;

    /// <summary>The finding's text of what it means, in the viewer's language; its key when this build doesn't know it.</summary>
    public static string Title(IStringLocalizer localizer, ProjectScanFindingDto finding)
        => ScanFindings.Localize(localizer, finding)?.Title ?? finding.Key ?? "";
}
