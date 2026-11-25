using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>How a part of the application map is doing, in the same five tones its Overview uses.</summary>
public enum ProjectMapPartTone { Healthy, Degraded, Failing, NotConfigured, Neutral }

/// <summary>The application map's tones and names, shared by its tab and its details (See ProjectMap).</summary>
public static class ProjectMapTones
{
    public static ProjectMapPartTone ToneOf(ProjectMapPartDto part) => part.IsNotConfigured
        ? ProjectMapPartTone.NotConfigured
        : part.Status switch
        {
            HealthCheckStatus.Healthy => ProjectMapPartTone.Healthy,
            HealthCheckStatus.Degraded => ProjectMapPartTone.Degraded,
            _ => ProjectMapPartTone.Failing
        };

    /// <summary>The worst of the parts that can fail; parts that only miss their settings count only when there's nothing else.</summary>
    public static ProjectMapPartTone WorstOf(IEnumerable<ProjectMapPartDto> parts)
    {
        var tones = parts.Select(ToneOf).ToArray();

        return tones.Contains(ProjectMapPartTone.Failing) ? ProjectMapPartTone.Failing
            : tones.Contains(ProjectMapPartTone.Degraded) ? ProjectMapPartTone.Degraded
            : tones.Contains(ProjectMapPartTone.Healthy) ? ProjectMapPartTone.Healthy
            : tones.Contains(ProjectMapPartTone.NotConfigured) ? ProjectMapPartTone.NotConfigured
            : ProjectMapPartTone.Neutral;
    }

    /// <summary>Failing first, then degraded, then working, then those that only miss their settings.</summary>
    public static int RankOf(ProjectMapPartTone tone) => tone switch
    {
        ProjectMapPartTone.Failing => 0,
        ProjectMapPartTone.Degraded => 1,
        ProjectMapPartTone.Healthy => 2,
        ProjectMapPartTone.NotConfigured => 3,
        _ => 4
    };

    public static ProjectMapPartTone ToneOf(ProjectHealth health) => health switch
    {
        ProjectHealth.Operational => ProjectMapPartTone.Healthy,
        ProjectHealth.Degraded => ProjectMapPartTone.Degraded,
        ProjectHealth.Down => ProjectMapPartTone.Failing,
        _ => ProjectMapPartTone.Neutral
    };

    /// <summary>What the app calls a part; its report key when the app says nothing.</summary>
    public static string TitleOf(ProjectMapPartDto part) => string.IsNullOrWhiteSpace(part.Title) ? part.Key ?? "?" : part.Title;

    public static string IconOf(ProjectMapPartTone tone) => tone switch
    {
        ProjectMapPartTone.Healthy => BitIconName.CompletedSolid,
        ProjectMapPartTone.Degraded => BitIconName.WarningSolid,
        ProjectMapPartTone.Failing => BitIconName.StatusErrorFull,
        ProjectMapPartTone.NotConfigured => BitIconName.Settings,
        _ => BitIconName.Info
    };

    public static BitColor ColorOf(ProjectMapPartTone tone) => tone switch
    {
        ProjectMapPartTone.Healthy => BitColor.Success,
        ProjectMapPartTone.Degraded => BitColor.Warning,
        ProjectMapPartTone.Failing => BitColor.Error,
        ProjectMapPartTone.NotConfigured => BitColor.Info,
        _ => BitColor.SecondaryForeground
    };

    public static string ClassOf(ProjectMapPartTone tone) => $"tone-{tone.ToString().ToLowerInvariant()}";
}
