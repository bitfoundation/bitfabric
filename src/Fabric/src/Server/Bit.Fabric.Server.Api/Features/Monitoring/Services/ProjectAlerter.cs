using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Bit.Fabric.Server.Api.Features.Monitoring.Components;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.PushNotification;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Hangfire;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Tells a project's team (bitfabric-internal/specs/monitoring.md, Alerts): one alert per incident and step rather than a
/// flood, each in its route's language, saying what broke and what to do; everyone a step reached hears when it's over,
/// with a prompt for their AI agent. Pushes, emails and SMS go through the app's own queues; a webhook is called at once,
/// through the same guarded client bit Fabric calls customers' apps with, since its address is the customer's too.
/// </summary>
public partial class ProjectAlerter
{
    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private PushNotificationService pushNotificationService = default!;
    [AutoInject] private IBackgroundJobClient backgroundJobClient = default!;
    [AutoInject] private IHttpClientFactory httpClientFactory = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;
    [AutoInject] private ILogger<ProjectAlerter> logger = default!;
    [AutoInject] private HtmlRenderer htmlRenderer = default!;

    /// <summary>What a message says, in its route's language.</summary>
    private sealed record Message(string Title, string Body, string PagePath);

    /// <summary>
    /// Alerts the routes of the incident's step about an incident that opened or escalated, or, once it's over, every
    /// route up to the step it reached. Then saves, with the next escalation's time.
    /// </summary>
    public async Task<List<ProjectAlert>> Notify(ProjectMonitoring project, ProjectIncident incident, ProjectAlertKind kind, CancellationToken cancellationToken)
    {
        var routes = await dbContext.ProjectAlertRoutes.IgnoreQueryFilters()
            .Include(route => route.User)
            .Where(route => route.MonitoringId == project.Id)
            // Never someone who has left the team since (See TeamDeparture), whatever routes were left behind.
            .Where(route => route.UserId == null || dbContext.TenantMemberships.Any(membership => membership.TenantId == route.TenantId && membership.UserId == route.UserId && membership.AcceptedOn != null))
            .ToListAsync(cancellationToken);

        var reached = kind switch
        {
            ProjectAlertKind.Resolved => routes.Where(route => route.Step <= incident.EscalationStep),
            ProjectAlertKind.Escalated => routes.Where(route => route.Step == incident.EscalationStep),
            _ => routes.Where(route => route.Step == 0)
        };

        var alerts = await Send(project, incident, kind, [.. reached], cancellationToken);

        if (kind is ProjectAlertKind.Opened or ProjectAlertKind.Escalated)
        {
            incident.NextEscalationAt = NextStep(routes, incident.EscalationStep) is { } next
                ? incident.StartedAt.AddMinutes(next.DelayMinutes)
                : null;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return alerts;
    }

    /// <summary>The first route of the next step, which says how long after the start that step goes out.</summary>
    public static ProjectAlertRoute? NextStep(IEnumerable<ProjectAlertRoute> routes, int step)
        => routes.Where(route => route.Step > step).OrderBy(route => route.Step).ThenBy(route => route.DelayMinutes).FirstOrDefault();

    /// <summary>The app stopped accepting bit Fabric's approval: step 0 hears it, since nothing is read until someone reconnects.</summary>
    public async Task NotifyConnectionLost(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        var routes = await dbContext.ProjectAlertRoutes.IgnoreQueryFilters()
            .Include(route => route.User)
            .Where(route => route.MonitoringId == project.Id && route.Step == 0)
            // Never someone who has left the team since (See TeamDeparture), whatever routes were left behind.
            .Where(route => route.UserId == null || dbContext.TenantMemberships.Any(membership => membership.TenantId == route.TenantId && membership.UserId == route.UserId && membership.AcceptedOn != null))
            .ToListAsync(cancellationToken);

        await Send(project, null, ProjectAlertKind.ConnectionLost, routes, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A test alert to every route, so the team sees where alerts land.</summary>
    public async Task<List<ProjectAlert>> SendTest(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        var routes = await dbContext.ProjectAlertRoutes
            .Include(route => route.User)
            .Where(route => route.MonitoringId == project.Id)
            // Never someone who has left the team since (See TeamDeparture), whatever routes were left behind.
            .Where(route => route.UserId == null || dbContext.TenantMemberships.Any(membership => membership.TenantId == route.TenantId && membership.UserId == route.UserId && membership.AcceptedOn != null))
            .ToListAsync(cancellationToken);

        var alerts = await Send(project, null, ProjectAlertKind.Test, routes, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return alerts;
    }

    private async Task<List<ProjectAlert>> Send(ProjectMonitoring project, ProjectIncident? incident, ProjectAlertKind kind, List<ProjectAlertRoute> routes, CancellationToken cancellationToken)
    {
        List<ProjectAlert> alerts = [];

        foreach (var route in routes)
        {
            var message = InCulture(route.Culture, () => Compose(project, incident, kind));

            ProjectAlert alert = new()
            {
                Id = Guid.CreateSequentialGuid(),
                MonitoringId = project.Id,
                TenantId = project.TenantId,
                IncidentId = incident?.Id,
                Kind = kind,
                Channel = route.Channel,
                SentAt = timeProvider.GetUtcNow(),
                Target = TargetOf(route)
            };

            try
            {
                (alert.Succeeded, alert.Problem, alert.ProblemStatusCode) = route.Channel switch
                {
                    ProjectAlertChannel.Push => await Push(route, message, cancellationToken),
                    ProjectAlertChannel.Email => await Email(project, route, message),
                    ProjectAlertChannel.Sms => Sms(route, message),
                    _ => await Webhook(project, incident, kind, route, message, cancellationToken)
                };
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                logger.LogWarning(exp, "Alerting route {RouteId} of project {MonitoringId} failed.", route.Id, project.Id);
                (alert.Succeeded, alert.Problem, alert.ProblemStatusCode) = (false, ProjectAlertProblem.Failed, null);
            }

            alerts.Add(alert);
        }

        await dbContext.ProjectAlerts.AddRangeAsync(alerts, cancellationToken);

        return alerts;
    }

    private Message Compose(ProjectMonitoring project, ProjectIncident? incident, ProjectAlertKind kind)
    {
        var path = PageUrls.ProjectEnvironment(project.ProjectId, project.Id);

        if (incident is null)
        {
            return kind is ProjectAlertKind.ConnectionLost
                ? new(localizer[nameof(AppStrings.MonitoringAlertConnectionLostTitle), project.Name!], ProjectConnectionProblems.Describe(localizer, project.ConnectionProblem) ?? localizer[nameof(AppStrings.MonitoringAlertConnectionLostBody)], path)
                : new(localizer[nameof(AppStrings.MonitoringTestAlertTitle), project.Name!], localizer[nameof(AppStrings.MonitoringTestAlertBody), project.Name!], path);
        }

        path = $"{path}?tab=incidents";
        var culture = CultureInfo.CurrentUICulture.Name;
        var title = ProjectTexts.In(incident.Title, culture) ?? "";

        return kind switch
        {
            ProjectAlertKind.Resolved => new(localizer[nameof(AppStrings.MonitoringAlertResolvedTitle), title],
                                             $"{ProjectTexts.In(incident.Updates[^1].Text, culture)} {localizer[nameof(AppStrings.MonitoringAskYourAIAgent), AgentPromptOf(project, incident)]}",
                                             path),
            ProjectAlertKind.Escalated => new($"{project.Name}: {title}",
                                              localizer[nameof(AppStrings.MonitoringAlertEscalatedBody), ProjectTexts.Duration(localizer, timeProvider.GetUtcNow() - incident.StartedAt)],
                                              path),
            _ => new($"{project.Name}: {title}", ProjectTexts.In(incident.Updates[0].Text, culture) ?? title, path)
        };
    }

    /// <summary>For the customer's AI agent, which can read the app's own logs: English, as agents read it best.</summary>
    public static string AgentPromptOf(ProjectMonitoring project, ProjectIncident incident)
        => $"bit Fabric saw the app at {project.Address} fail ({string.Join(", ", incident.CheckKeys)}) from {incident.StartedAt:u} to {incident.EndedAt ?? DateTimeOffset.UtcNow:u}. Read the app's logs and traces around that time, find the cause, and propose a fix.";

    private async Task<(bool, ProjectAlertProblem?, int?)> Push(ProjectAlertRoute route, Message message, CancellationToken cancellationToken)
    {
        var userId = route.UserId;

        var reached = await pushNotificationService.RequestPush(new()
        {
            Title = message.Title,
            Message = message.Body,
            PageUrl = message.PagePath
        }, subscription => subscription.UserSession != null && subscription.UserSession.UserId == userId, cancellationToken);

        return reached > 0 ? (true, null, null) : (false, ProjectAlertProblem.NoDevice, null);
    }

    private async Task<(bool, ProjectAlertProblem?, int?)> Email(ProjectMonitoring project, ProjectAlertRoute route, Message message)
    {
        if (route.User?.Email is not { Length: > 0 } email || route.User.EmailConfirmed is false)
            return (false, ProjectAlertProblem.NoConfirmedEmail, null);

        // In the frame every email shares (See EmailLayout), and in the route's language, as the message is.
        var webAppUrl = settings.WebAppUrl ?? "https://bitfabric.co/";
        var body = await InCultureAsync(route.Culture, () => Render(new ProjectAlertEmailModel
        {
            Title = message.Title,
            Body = message.Body,
            ProjectName = project.Name ?? "",
            Link = new Uri($"{webAppUrl.TrimEnd('/')}{message.PagePath}"),
            WebAppUrl = new Uri(webAppUrl)
        }));

        var name = route.User.DisplayName ?? email;
        var subject = message.Title;
        backgroundJobClient.Enqueue<EmailServiceJobsRunner>(runner => runner.SendEmailJob(email, name, subject, body, null!, default));

        return (true, null, null);
    }

    private (bool, ProjectAlertProblem?, int?) Sms(ProjectAlertRoute route, Message message)
    {
        if (route.User?.PhoneNumber is not { Length: > 0 } phoneNumber || route.User.PhoneNumberConfirmed is false)
            return (false, ProjectAlertProblem.NoConfirmedPhoneNumber, null);

        var text = $"{message.Title}\n{message.Body}";
        var sms = text.Length > 300 ? text[..300] : text;
        backgroundJobClient.Enqueue<PhoneServiceJobsRunner>(runner => runner.SendSms(phoneNumber, sms, null!, default));

        return (true, null, null);
    }

    private async Task<(bool, ProjectAlertProblem?, int?)> Webhook(ProjectMonitoring project, ProjectIncident? incident, ProjectAlertKind kind, ProjectAlertRoute route, Message message, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(route.Address, UriKind.Absolute, out var address) is false)
            return (false, ProjectAlertProblem.InvalidWebhook, null);

        var link = $"{settings.WebAppUrl?.TrimEnd('/')}{message.PagePath}";
        var text = $"{message.Title}\n{message.Body}";

        JsonNode body = route.WebhookFormat switch
        {
            ProjectWebhookFormat.Slack => new JsonObject { ["text"] = $"*{message.Title}*\n{message.Body}\n<{link}|{project.Name}>" },
            ProjectWebhookFormat.Discord => new JsonObject { ["content"] = $"**{message.Title}**\n{message.Body}\n{link}" },
            ProjectWebhookFormat.Teams => TeamsCard(message, project.Name, link),
            _ => new JsonObject
            {
                ["event"] = kind.ToString(),
                ["project"] = new JsonObject { ["id"] = project.Id.ToString(), ["name"] = project.Name, ["address"] = project.Address },
                ["incident"] = incident is null ? null : new JsonObject
                {
                    ["id"] = incident.Id.ToString(),
                    ["impact"] = incident.Impact.ToString(),
                    ["parts"] = new JsonArray([.. incident.CheckKeys.Select(key => (JsonNode)key)]),
                    ["startedAt"] = incident.StartedAt,
                    ["endedAt"] = incident.EndedAt
                },
                ["title"] = message.Title,
                ["text"] = text,
                ["url"] = link
            }
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));

        var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
        using var response = await http.PostAsJsonAsync(address, body, timeout.Token);

        return response.IsSuccessStatusCode ? (true, null, null) : (false, ProjectAlertProblem.WebhookRefused, (int)response.StatusCode);
    }

    /// <summary>
    /// A Teams channel's webhook is a workflow since Microsoft retired Teams' connectors in May 2026, and its "Send webhook
    /// alerts to a channel" posts only an Adaptive Card: a plain text fails there with "Property 'type' must be
    /// 'AdaptiveCard'", once Teams has already answered 202. So the alert is a card, in a message's attachments, the shape
    /// of Microsoft's own sample.
    /// </summary>
    private static JsonObject TeamsCard(Message message, string projectName, string link) => new()
    {
        ["type"] = "message",
        ["attachments"] = new JsonArray(new JsonObject
        {
            ["contentType"] = "application/vnd.microsoft.card.adaptive",
            ["content"] = new JsonObject
            {
                ["$schema"] = "http://adaptivecards.io/schemas/adaptive-card.json",
                ["type"] = "AdaptiveCard",
                ["version"] = "1.4",
                ["body"] = new JsonArray(
                    new JsonObject { ["type"] = "TextBlock", ["text"] = message.Title, ["weight"] = "Bolder", ["size"] = "Medium", ["wrap"] = true },
                    new JsonObject { ["type"] = "TextBlock", ["text"] = message.Body, ["wrap"] = true }),
                ["actions"] = new JsonArray(new JsonObject { ["type"] = "Action.OpenUrl", ["title"] = projectName, ["url"] = link })
            }
        })
    };

    private static string? TargetOf(ProjectAlertRoute route) => route.Channel switch
    {
        ProjectAlertChannel.Webhook => Uri.TryCreate(route.Address, UriKind.Absolute, out var address) ? address.Host : route.Address,
        _ => route.User?.DisplayName ?? route.User?.UserName
    };

    private async Task<string> Render(ProjectAlertEmailModel model)
    {
        return await htmlRenderer.Dispatcher.InvokeAsync(async () =>
        {
            var rendered = await htmlRenderer.RenderComponentAsync<ProjectAlertEmailTemplate>(ParameterView.FromDictionary(new Dictionary<string, object?>
            {
                [nameof(ProjectAlertEmailTemplate.Model)] = model
            }));

            return rendered.ToHtmlString();
        });
    }

    private static async Task<T> InCultureAsync<T>(string? culture, Func<Task<T>> write)
    {
        var current = CultureInfo.CurrentUICulture;
        var currentFormat = CultureInfo.CurrentCulture;

        try
        {
            if (culture is not null && CultureInfoManager.SupportedCultures.FirstOrDefault(supported => supported.Culture.Name == culture) is { Culture: not null } supported)
            {
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = supported.Culture;
            }

            return await write();
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
            CultureInfo.CurrentCulture = currentFormat;
        }
    }

    private static T InCulture<T>(string? culture, Func<T> write)
    {
        var current = CultureInfo.CurrentUICulture;
        var currentFormat = CultureInfo.CurrentCulture;

        try
        {
            if (culture is not null && CultureInfoManager.SupportedCultures.FirstOrDefault(supported => supported.Culture.Name == culture) is { Culture: not null } supported)
            {
                CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = supported.Culture;
            }

            return write();
        }
        finally
        {
            CultureInfo.CurrentUICulture = current;
            CultureInfo.CurrentCulture = currentFormat;
        }
    }
}
