using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's incidents (bitfabric-internal/specs/monitoring.md, Checks and Alerts): the open one first, each with its
/// story, who was told, "I'm on it" to stop the escalation, the team's own updates, and once it's over a prompt for the
/// team's AI agent.
/// </summary>
public partial class ProjectIncidents
{
    [Parameter, EditorRequired] public Guid ProjectId { get; set; }

    [AutoInject] private IProjectIncidentsController incidentsController = default!;
    [AutoInject] private Clipboard clipboard = default!;

    private ProjectIncidentDto[]? incidents;
    private Guid? openId;
    private Guid? copiedId;
    private bool isSaving;
    private ProjectIncidentUpdateRequestDto update = new();

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        incidents = await incidentsController.GetIncidents(ProjectId, CurrentCancellationToken);

        // The open one, or the newest, comes open.
        openId = incidents.FirstOrDefault()?.Id;
    }

    private async Task Acknowledge(ProjectIncidentDto incident)
    {
        if (isSaving) return;
        isSaving = true;

        try
        {
            Replace(await incidentsController.Acknowledge(incident.Id, CurrentCancellationToken));
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task PostUpdate(ProjectIncidentDto incident)
    {
        if (isSaving) return;
        isSaving = true;

        try
        {
            update.IncidentId = incident.Id;
            update.Version = incident.Version;
            Replace(await incidentsController.PostUpdate(update, CurrentCancellationToken));
            update = new();
        }
        finally
        {
            isSaving = false;
        }
    }

    private void Replace(ProjectIncidentDto changed)
        => incidents = [.. incidents!.Select(incident => incident.Id == changed.Id ? changed : incident)];

    private async Task Copy(Guid id, string text)
    {
        await clipboard.WriteText(text);
        copiedId = id;
    }

    private static string ToneOf(ProjectIncidentDto incident) => incident.EndedAt is not null ? "tone-ok"
        : incident.Impact is ProjectHealth.Down ? "tone-down"
        : "tone-degraded";

    private string PhaseText(ProjectIncidentPhase phase) => phase switch
    {
        ProjectIncidentPhase.Identified => Localizer[nameof(AppStrings.MonitoringIdentified)],
        ProjectIncidentPhase.Monitoring => Localizer[nameof(AppStrings.MonitoringMonitoringAFix)],
        ProjectIncidentPhase.Resolved => Localizer[nameof(AppStrings.MonitoringResolved)],
        _ => Localizer[nameof(AppStrings.MonitoringInvestigating)]
    };

    private static BitColor PhaseColor(ProjectIncidentPhase phase) => phase switch
    {
        ProjectIncidentPhase.Resolved => BitColor.Success,
        ProjectIncidentPhase.Monitoring => BitColor.Info,
        ProjectIncidentPhase.Identified => BitColor.Warning,
        _ => BitColor.Error
    };

    private static string PhaseIcon(ProjectIncidentPhase phase) => phase switch
    {
        ProjectIncidentPhase.Resolved => BitIconName.CompletedSolid,
        ProjectIncidentPhase.Monitoring => BitIconName.View,
        ProjectIncidentPhase.Identified => BitIconName.Search,
        _ => BitIconName.WarningSolid
    };

    private static string ChannelIcon(ProjectAlertChannel channel) => channel switch
    {
        ProjectAlertChannel.Push => BitIconName.Ringer,
        ProjectAlertChannel.Email => BitIconName.Mail,
        ProjectAlertChannel.Sms => BitIconName.CellPhone,
        _ => BitIconName.PlugConnected
    };

    private string ChannelText(ProjectAlertChannel channel) => channel switch
    {
        ProjectAlertChannel.Push => Localizer[nameof(AppStrings.MonitoringPush)],
        ProjectAlertChannel.Email => Localizer[nameof(AppStrings.Email)],
        ProjectAlertChannel.Sms => Localizer[nameof(AppStrings.MonitoringSms)],
        _ => Localizer[nameof(AppStrings.MonitoringWebhook)]
    };

    private string LocalTime(DateTimeOffset at, string format) => TimeZoneService.ToLocalTime(at).ToString(format, CultureInfo.CurrentUICulture);

    private string Duration(TimeSpan duration) => MonitoringWords.Duration(Localizer, duration);

    private string? ProblemText(ProjectAlertDto alert) => ProjectAlerts.ProblemText(Localizer, alert);
}
