using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Microsoft.Extensions.AI;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>A project's status page as its team runs it (bitfabric-internal/specs/monitoring.md, Status page); every query through the tenant.</summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), Authorize(Policy = AuthPolicies.TENANT_SELECTED)]
public partial class StatusPagesController : AppControllerBase, IStatusPagesController
{
    [AutoInject] private StatusPageService statusPages = default!;
    [AutoInject] private CloudflareCustomHostnames customHostnames = default!;
    [AutoInject] private ServerApiSettings settings = default!;

    [HttpGet("{projectId}")]
    public async Task<StatusPageSettingsDto> GetStatusPage(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindProject(projectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);

        return await ToSettings(page, cancellationToken);
    }

    [HttpPost]
    public async Task<StatusPageSettingsDto> SaveStatusPage(StatusPageSettingsDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.ProjectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);

        var slug = request.Slug?.Trim().ToLowerInvariant();
        if (StatusPageService.IsValidSlug(slug) is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringInvalidStatusPageAddress)]);

        if (slug != page.Slug && await DbContext.StatusPages.IgnoreQueryFilters().AnyAsync(other => other.Slug == slug, cancellationToken))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringStatusPageAddressTaken)]);

        string[] languages = [.. request.Languages.Where(language => CultureInfoManager.SupportedCultures.Any(supported => supported.Culture.Name == language)).Distinct()];
        if (languages.Length is 0)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringStatusPageNeedsALanguage)]);

        if (request.Components.Any(component => string.IsNullOrWhiteSpace(component.Name.GetValueOrDefault(languages[0]))))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringComponentNeedsAName)]);

        page.Version = request.Version;
        page.Slug = slug;
        page.IsPublished = request.IsPublished;
        page.ShowsResponseTime = request.ShowsResponseTime;
        page.Languages = languages;
        page.SupportAddress = string.IsNullOrWhiteSpace(request.SupportAddress) ? null : request.SupportAddress.Trim();

        // Components keep their ids, which followers and incidents name.
        var kept = request.Components.Select(component => component.Id).ToHashSet();
        foreach (var removed in page.Components.Where(component => kept.Contains(component.Id) is false).ToList())
        {
            page.Components.Remove(removed);
        }

        var order = 0;
        foreach (var dto in request.Components)
        {
            var component = page.Components.FirstOrDefault(component => component.Id == dto.Id);
            if (component is null)
            {
                component = new() { Id = Guid.CreateSequentialGuid(), StatusPageId = page.Id, TenantId = page.TenantId };
                page.Components.Add(component);
            }

            component.Order = order++;
            component.Name = TextsOf(dto.Name, languages);
            component.Description = TextsOf(dto.Description, languages);
            component.CheckKeys = [.. dto.CheckKeys.Where(key => string.IsNullOrWhiteSpace(key) is false).Distinct()];
            component.IsShown = dto.IsShown;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return await ToSettings(page, cancellationToken);
    }

    [HttpPost]
    public async Task<StatusPageSettingsDto> ConnectCustomHostname(ConnectCustomHostnameRequestDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.ProjectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);

        if (customHostnames.IsConfigured is false)
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringOwnAddressNotSetUpOnServer)]);

        var hostname = request.Hostname!.Trim().TrimEnd('.').ToLowerInvariant();
        if (HostnameRegex().IsMatch(hostname) is false || IsOwnHost(hostname))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringUseYourOwnHostName)]);

        if (await DbContext.StatusPages.IgnoreQueryFilters().AnyAsync(other => other.CustomHostname == hostname && other.Id != page.Id, cancellationToken))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringHostNameTaken)]);

        if (page.CustomHostnameId is { } previous)
        {
            await customHostnames.Delete(previous, cancellationToken);
        }

        var state = await customHostnames.Create(hostname, cancellationToken);

        page.CustomHostname = hostname;
        page.CustomHostnameId = state.Id;
        page.CustomHostnameState = state.Status;
        await DbContext.SaveChangesAsync(cancellationToken);

        return await ToSettings(page, cancellationToken);
    }

    [HttpPost("{projectId}")]
    public async Task<StatusPageSettingsDto> RefreshCustomHostname(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindProject(projectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);

        if (page.CustomHostnameId is { } id && customHostnames.IsConfigured)
        {
            page.CustomHostnameState = (await customHostnames.Get(id, cancellationToken)).Status;
            await DbContext.SaveChangesAsync(cancellationToken);
        }

        return await ToSettings(page, cancellationToken);
    }

    [HttpPost("{projectId}")]
    public async Task<StatusPageSettingsDto> DisconnectCustomHostname(Guid projectId, CancellationToken cancellationToken)
    {
        var project = await FindProject(projectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);

        if (page.CustomHostnameId is { } id && customHostnames.IsConfigured)
        {
            await customHostnames.Delete(id, cancellationToken);
        }

        page.CustomHostname = page.CustomHostnameId = page.CustomHostnameState = null;
        await DbContext.SaveChangesAsync(cancellationToken);

        return await ToSettings(page, cancellationToken);
    }

    [HttpPost]
    public async Task<ProjectIncidentDto> Post(StatusPostRequestDto request, CancellationToken cancellationToken)
    {
        var project = await FindProject(request.ProjectId, cancellationToken);
        var page = await statusPages.Ensure(project, cancellationToken);
        var now = TimeProvider.GetUtcNow();
        var author = User.GetDisplayName();
        var main = page.Languages.FirstOrDefault() ?? CultureInfo.CurrentUICulture.Name;

        if (string.IsNullOrWhiteSpace(request.Text.GetValueOrDefault(main)))
            throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringWriteInTheMainLanguage)]);

        ProjectIncident incident;

        if (request.IncidentId is { } incidentId)
        {
            incident = await DbContext.ProjectIncidents.FirstOrDefaultAsync(incident => incident.Id == incidentId && incident.ProjectId == project.Id, cancellationToken)
                ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

            if (incident.IsAutomatic && request.Phase is ProjectIncidentPhase.Resolved)
                throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringAutomaticIncidentClosesByItself)]);

            incident.Version = request.Version;

            if (request.Phase is ProjectIncidentPhase.Resolved && incident.EndedAt is null)
            {
                incident.EndedAt = now;
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.Title.GetValueOrDefault(main)))
                throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringWriteInTheMainLanguage)]);

            Guid[] known = [.. page.Components.Select(component => component.Id)];
            var isAnnouncement = request.Kind is ProjectIncidentKind.Announcement;

            incident = new()
            {
                Id = Guid.CreateSequentialGuid(),
                ProjectId = project.Id,
                TenantId = project.TenantId,
                Kind = request.Kind,
                Impact = isAnnouncement ? ProjectHealth.Operational : request.Impact is ProjectHealth.Down ? ProjectHealth.Down : ProjectHealth.Degraded,
                StartedAt = now,
                // An announcement is news, not a stretch of time.
                EndedAt = isAnnouncement || request.Phase is ProjectIncidentPhase.Resolved ? now : null,
                ComponentIds = [.. request.ComponentIds.Where(known.Contains)],
                Title = TextsOf(request.Title, page.Languages)
            };

            await DbContext.ProjectIncidents.AddAsync(incident, cancellationToken);
        }

        incident.Updates.Add(new()
        {
            At = now,
            Phase = request.Phase,
            Author = author,
            Text = TextsOf(request.Text, page.Languages)
        });

        await DbContext.SaveChangesAsync(cancellationToken);

        if (request.NotifiesFollowers)
        {
            await statusPages.NotifyFollowers(project, incident, "", cancellationToken);
        }

        return ProjectIncidentsService.ToDto(project, incident, [], []);
    }

    [HttpPost]
    public async Task<Dictionary<string, string>> DraftTranslations(DraftTranslationsRequestDto request, CancellationToken cancellationToken)
    {
        var chatClient = HttpContext.RequestServices.GetService<IChatClient>()
            ?? throw new BadRequestException(Localizer[nameof(AppStrings.MonitoringDraftingNeedsAI)]);

        if (string.IsNullOrWhiteSpace(request.Text) || request.Text.Length > 2000)
            throw new BadRequestException();

        Dictionary<string, string> drafts = [];

        foreach (var culture in request.ToCultures.Where(culture => CultureInfoManager.SupportedCultures.Any(supported => supported.Culture.Name == culture)).Distinct().Take(10))
        {
            var language = CultureInfo.GetCultureInfo(culture).EnglishName;
            var response = await chatClient.GetResponseAsync(
            [
                new(ChatRole.System, $"You translate a status page update about an app's availability into {language}. Keep it short, plain and polite. Keep product names, host names and technical terms as they are. Answer with the translation only."),
                new(ChatRole.User, request.Text)
            ], cancellationToken: cancellationToken);

            drafts[culture] = response.Text.Trim();
        }

        return drafts;
    }

    private async Task<Project> FindProject(Guid projectId, CancellationToken cancellationToken)
        => await DbContext.Projects.FirstOrDefaultAsync(project => project.Id == projectId, cancellationToken)
           ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

    private async Task<StatusPageSettingsDto> ToSettings(StatusPage page, CancellationToken cancellationToken)
    {
        var lastReport = await DbContext.ProjectProbes.AsNoTracking()
            .Where(probe => probe.ProjectId == page.ProjectId && probe.Outcome == ProjectProbeOutcome.Read)
            .OrderByDescending(probe => probe.At)
            .FirstOrDefaultAsync(cancellationToken);

        return new()
        {
            ProjectId = page.ProjectId,
            Slug = page.Slug,
            IsPublished = page.IsPublished,
            ShowsResponseTime = page.ShowsResponseTime,
            Languages = page.Languages,
            SupportAddress = page.SupportAddress,
            Components = [.. page.Components.OrderBy(component => component.Order).Select(component => new StatusComponentDto
            {
                Id = component.Id,
                Name = component.Name.Where(text => text.Culture is not null).ToDictionary(text => text.Culture!, text => text.Text ?? ""),
                Description = component.Description.Where(text => text.Culture is not null).ToDictionary(text => text.Culture!, text => text.Text ?? ""),
                CheckKeys = component.CheckKeys,
                IsShown = component.IsShown
            })],
            PublicUrl = statusPages.PublicUrlOf(page),
            CustomHostname = page.CustomHostname,
            CustomHostnameState = page.CustomHostnameState,
            CnameTarget = customHostnames.IsConfigured ? settings.Monitoring.StatusPageCnameTarget : null,
            FollowerCount = await statusPages.FollowerCountOf(page.Id, cancellationToken),
            Checks = [.. lastReport?.Checks.Where(check => check.IsNotConfigured is false)
                                          .Select(check => new StatusCheckDto { Key = check.Key, Title = check.Title ?? check.Key, Category = check.Category }) ?? []],
            Version = page.Version
        };
    }

    /// <summary>The texts the team wrote, in the page's languages only, empty ones left out.</summary>
    private static List<ProjectText> TextsOf(Dictionary<string, string> texts, string[] languages)
        => [.. texts.Where(text => languages.Contains(text.Key) && string.IsNullOrWhiteSpace(text.Value) is false)
                    .Select(text => new ProjectText { Culture = text.Key, Text = text.Value.Trim().Length > 2000 ? text.Value.Trim()[..2000] : text.Value.Trim() })];

    private bool IsOwnHost(string hostname)
        => new[] { settings.WebAppUrl, settings.Monitoring.StatusPageCnameTarget is { } target ? $"https://{target}" : null }
            .Any(own => Uri.TryCreate(own, UriKind.Absolute, out var uri) && (hostname == uri.Host || hostname.EndsWith($".{uri.Host}", StringComparison.Ordinal)));

    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\.)+[a-z]{2,63}$")]
    private static partial Regex HostnameRegex();
}
