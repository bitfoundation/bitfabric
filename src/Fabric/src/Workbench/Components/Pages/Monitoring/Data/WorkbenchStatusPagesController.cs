using System.Text.RegularExpressions;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Infrastructure.Exceptions;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-ins for bit Fabric's <see cref="IStatusPagesController"/> and <see cref="IPublicStatusController"/>: a sample
/// project's status page as its team runs it, with its own host name checked again into Cloudflare's next state, posts in
/// every language it's written in and AI drafts of the missing ones; and the public page a visitor reads.
/// </summary>
public sealed partial class WorkbenchStatusPagesController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IStatusPagesController
{
    public Task<StatusPageSettingsDto> GetStatusPage(Guid monitoringId, CancellationToken cancellationToken)
        => Load("status-page", () => MonitoringReads.StatusPage(World.Get(monitoringId)), () =>
        {
            // A project that has none gets its first, unpublished, with a component per kind of part.
            var page = MonitoringReads.StatusPage(World.Get(monitoringId));
            return new StatusPageSettingsDto
            {
                MonitoringId = monitoringId,
                Slug = page.Slug,
                Languages = [MonitoringTexts.MainCulture],
                Components = [.. page.Checks.GroupBy(check => check.Category).Select(group => new StatusComponentDto
                {
                    Id = MonitoringNoise.IdOf($"{monitoringId}:first:{group.Key}"),
                    Name = new() { [MonitoringTexts.MainCulture] = group.Key ?? "" },
                    CheckKeys = [.. group.Select(check => check.Key!)]
                })],
                Checks = page.Checks,
                CnameTarget = page.CnameTarget,
                PublicUrl = $"https://bitfabric.co/status/{page.Slug}"
            };
        }, cancellationToken);

    public Task<StatusPageSettingsDto> SaveStatusPage(StatusPageSettingsDto settings, CancellationToken cancellationToken) => Save(() =>
    {
        if (settings.Slug is null || SlugRegex().IsMatch(settings.Slug) is false)
            throw new BadRequestException();

        var project = World.Get(settings.MonitoringId);
        var page = project.StatusPage;

        page.Slug = settings.Slug;
        page.IsPublished = settings.IsPublished;
        page.ShowsResponseTime = settings.ShowsResponseTime;
        page.Languages = settings.Languages.Length is 0 ? [MonitoringTexts.MainCulture] : settings.Languages;
        page.SupportAddress = settings.SupportAddress;
        page.Components = [.. settings.Components.Select(component => { component.Id = component.Id == Guid.Empty ? Guid.NewGuid() : component.Id; return component; })];
        page.Version++;

        return MonitoringReads.StatusPage(project);
    }, cancellationToken);

    public Task<StatusPageSettingsDto> ConnectCustomHostname(ConnectCustomHostnameRequestDto request, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(request.MonitoringId);

        if (project.StatusPage.CnameTarget is null)
            throw new BadRequestException();

        project.StatusPage.CustomHostname = request.Hostname?.Trim().ToLowerInvariant();
        project.StatusPage.CustomHostnameState = "pending";

        return MonitoringReads.StatusPage(project);
    }, cancellationToken);

    /// <summary>Cloudflare found the CNAME record: a pending host name becomes active.</summary>
    public Task<StatusPageSettingsDto> RefreshCustomHostname(Guid monitoringId, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(monitoringId);

        if (project.StatusPage.CustomHostnameState is "pending")
        {
            project.StatusPage.CustomHostnameState = "active";
        }

        return MonitoringReads.StatusPage(project);
    }, cancellationToken);

    public Task<StatusPageSettingsDto> DisconnectCustomHostname(Guid monitoringId, CancellationToken cancellationToken) => Save(() =>
    {
        var project = World.Get(monitoringId);
        project.StatusPage.CustomHostname = null;
        project.StatusPage.CustomHostnameState = null;

        return MonitoringReads.StatusPage(project);
    }, cancellationToken);

    public async Task<ProjectIncidentDto> Post(StatusPostRequestDto request, CancellationToken cancellationToken)
    {
        var name = await CurrentUserName();

        return await Save(() =>
        {
            var project = World.Get(request.MonitoringId);
            var now = World.Now;

            if (request.IncidentId is { } incidentId)
            {
                var incident = World.GetIncident(incidentId, out _);
                incident.Updates.Insert(0, new() { At = now, Phase = request.Phase, Author = name, Text = Written(request.Text) });
                incident.EndedAt = request.Phase is ProjectIncidentPhase.Resolved ? now : incident.EndedAt;
                incident.Version++;

                return MonitoringReads.Incident(project, incident);
            }

            var isAnnouncement = request.Kind is ProjectIncidentKind.Announcement;
            var posted = new SampleIncident
            {
                Id = Guid.NewGuid(),
                Kind = request.Kind,
                Impact = isAnnouncement ? ProjectHealth.Operational : request.Impact,
                Title = Written(request.Title),
                StartedAt = now,
                EndedAt = isAnnouncement || request.Phase is ProjectIncidentPhase.Resolved ? now : null,
                ComponentIds = request.ComponentIds
            };
            posted.Updates.Add(new() { At = now, Phase = request.Phase, Author = name, Text = Written(request.Text) });
            project.Incidents.Add(posted);

            return MonitoringReads.Incident(project, posted);
        }, cancellationToken);
    }

    /// <summary>
    /// The sample world's stand-in for the AI's drafts: the text marked as a draft in each language asked for, for the team
    /// to review, as the real ones are never posted as they are.
    /// </summary>
    public Task<Dictionary<string, string>> DraftTranslations(DraftTranslationsRequestDto request, CancellationToken cancellationToken) => Save(()
        => request.ToCultures.ToDictionary(culture => culture, culture => culture.StartsWith("fa", StringComparison.Ordinal)
            ? $"(پیش‌نویس AI) {request.Text}"
            : $"(AI draft, {CultureInfoManager.GetCultureInfo(culture)?.NativeName ?? culture}) {request.Text}"), cancellationToken);

    private static Dictionary<string, string> Written(Dictionary<string, string> texts)
        => texts.Where(pair => string.IsNullOrWhiteSpace(pair.Value) is false).ToDictionary(pair => pair.Key, pair => pair.Value.Trim());

    [GeneratedRegex("^[a-z0-9-]{1,60}$")]
    private static partial Regex SlugRegex();
}

/// <summary>The stand-in for bit Fabric's <see cref="IPublicStatusController"/>: a published sample status page, by its slug.</summary>
public sealed class WorkbenchPublicStatusController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), IPublicStatusController
{
    public Task<PublicStatusDto> Get(string slug, string? language, CancellationToken cancellationToken)
        => Load<PublicStatusDto>("public-status", () => World.FindBySlug(slug) is { StatusPage.IsPublished: true } project
            ? MonitoringReads.PublicStatus(project, language, World.Now)
            : throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException),
            () => throw new ResourceNotFoundException(AppStrings.ResourceNotFoundException), cancellationToken);

    public Task<string?> SlugOf(string hostname, CancellationToken cancellationToken)
        => Task.FromResult(World.Projects.FirstOrDefault(project => project.StatusPage.CustomHostname == hostname && project.StatusPage.CustomHostnameState is "active")?.StatusPage.Slug);

    public Task Follow(FollowStatusPageRequestDto request, CancellationToken cancellationToken) => Save(() => { }, cancellationToken);

    public Task Unfollow(FollowStatusPageRequestDto request, CancellationToken cancellationToken) => Save(() => { }, cancellationToken);
}
