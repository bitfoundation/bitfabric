using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// Who hears about a project's incidents and when (bitfabric-internal/specs/monitoring.md, Alerts): step 0 at once, each
/// later step after its delay unless someone pressed "I'm on it", and everyone a step reached when it's over. A route is a
/// push, an email or an SMS to a member of the team, or a webhook such as a Slack, Discord or Teams channel's.
/// </summary>
public partial class ProjectAlerts
{
    [Parameter, EditorRequired] public Guid ProjectId { get; set; }

    [AutoInject] private IProjectIncidentsController incidentsController = default!;

    private ProjectAlertSettingsDto? alertSettings;
    private List<ProjectAlertRouteDto> routes = [];
    private ProjectAlertDto[]? testResults;
    private bool isSaving;
    private bool isTesting;
    private bool isDirty;

    // The route being added.
    private ProjectAlertChannel newChannel = ProjectAlertChannel.Push;
    private Guid? newUserId;
    private string? newAddress;
    private ProjectWebhookFormat newFormat = ProjectWebhookFormat.Slack;
    private int newStep;
    private int newDelay = 10;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        Show(await incidentsController.GetAlertSettings(ProjectId, CurrentCancellationToken));
    }

    private void Show(ProjectAlertSettingsDto loaded)
    {
        alertSettings = loaded;
        routes = [.. loaded.Routes];
        newUserId ??= loaded.Members.FirstOrDefault()?.UserId;
        isDirty = false;
    }

    private IEnumerable<IGrouping<int, ProjectAlertRouteDto>> Steps() => routes.GroupBy(route => route.Step).OrderBy(step => step.Key);

    private int NextStep => routes.Count is 0 ? 0 : routes.Max(route => route.Step) + 1;

    private void SetDelay(int step, int minutes)
    {
        foreach (var route in routes.Where(route => route.Step == step))
        {
            route.DelayMinutes = Math.Clamp(minutes, 1, 24 * 60);
        }

        isDirty = true;
    }

    private void Remove(ProjectAlertRouteDto route)
    {
        routes.Remove(route);

        // Steps stay numbered from 0 without gaps, so step 0 is always the one that goes out at once.
        var renumbered = routes.Select(r => r.Step).Distinct().Order().Select((step, index) => (step, index)).ToDictionary(pair => pair.step, pair => pair.index);
        foreach (var r in routes)
        {
            r.Step = renumbered[r.Step];
            if (r.Step is 0) r.DelayMinutes = 0;
        }

        isDirty = true;
    }

    private bool CanAdd => newChannel is ProjectAlertChannel.Webhook
        ? Uri.TryCreate(newAddress?.Trim(), UriKind.Absolute, out var address) && address.Scheme == Uri.UriSchemeHttps
        : newUserId is not null;

    private void Add()
    {
        if (CanAdd is false) return;

        var step = Math.Clamp(newStep, 0, NextStep);
        var delay = step is 0 ? 0 : routes.FirstOrDefault(route => route.Step == step)?.DelayMinutes ?? Math.Clamp(newDelay, 1, 24 * 60);

        routes.Add(new()
        {
            Step = step,
            DelayMinutes = delay,
            Channel = newChannel,
            UserId = newChannel is ProjectAlertChannel.Webhook ? null : newUserId,
            UserName = alertSettings!.Members.FirstOrDefault(member => member.UserId == newUserId)?.Name,
            Address = newChannel is ProjectAlertChannel.Webhook ? newAddress!.Trim() : null,
            WebhookFormat = newChannel is ProjectAlertChannel.Webhook ? newFormat : null
        });

        newAddress = null;
        isDirty = true;
    }

    private async Task Save()
    {
        if (isSaving) return;
        isSaving = true;

        try
        {
            Show(await incidentsController.SaveAlertSettings(new() { ProjectId = ProjectId, Routes = [.. routes] }, CurrentCancellationToken));
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task SendTest()
    {
        if (isTesting) return;
        isTesting = true;

        try
        {
            testResults = await incidentsController.SendTestAlert(ProjectId, CurrentCancellationToken);
        }
        finally
        {
            isTesting = false;
        }
    }

    private ProjectTeamMemberDto? MemberOf(Guid? userId) => alertSettings?.Members.FirstOrDefault(member => member.UserId == userId);

    /// <summary>Why a route can't reach its member, which the list says next to it.</summary>
    private string? Problem(ProjectAlertRouteDto route) => (route.Channel, MemberOf(route.UserId)) switch
    {
        (ProjectAlertChannel.Email, { HasEmail: false }) => Localizer[nameof(AppStrings.MonitoringNoConfirmedEmail)].Value,
        (ProjectAlertChannel.Sms, { HasPhoneNumber: false }) => Localizer[nameof(AppStrings.MonitoringNoConfirmedPhoneNumber)].Value,
        _ => null
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
        ProjectAlertChannel.Push => Localizer[nameof(AppStrings.MonitoringAPush)],
        ProjectAlertChannel.Email => Localizer[nameof(AppStrings.MonitoringAnEmail)],
        ProjectAlertChannel.Sms => Localizer[nameof(AppStrings.MonitoringAnSms)],
        _ => Localizer[nameof(AppStrings.MonitoringAWebhook)]
    };

    private static string FormatText(ProjectWebhookFormat? format) => format switch
    {
        ProjectWebhookFormat.Slack => "Slack",
        ProjectWebhookFormat.Discord => "Discord",
        ProjectWebhookFormat.Teams => "Microsoft Teams",
        _ => "JSON"
    };

    private static string Host(string? address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : address ?? "";

    /// <summary>Why an alert didn't reach its route, in the reader's language; null for one that did.</summary>
    public static string? ProblemText(IStringLocalizer localizer, ProjectAlertDto alert) => alert.Problem switch
    {
        null => null,
        ProjectAlertProblem.NoDevice => localizer[nameof(AppStrings.MonitoringAlertProblemNoDevice)],
        ProjectAlertProblem.NoConfirmedEmail => localizer[nameof(AppStrings.MonitoringNoConfirmedEmail)],
        ProjectAlertProblem.NoConfirmedPhoneNumber => localizer[nameof(AppStrings.MonitoringNoConfirmedPhoneNumber)],
        ProjectAlertProblem.InvalidWebhook => localizer[nameof(AppStrings.MonitoringAlertProblemInvalidWebhook)],
        ProjectAlertProblem.WebhookRefused => localizer[nameof(AppStrings.MonitoringAlertProblemWebhookRefused), alert.ProblemStatusCode ?? 0],
        _ => localizer[nameof(AppStrings.MonitoringAlertProblemFailed)]
    };
}
