using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.PushNotification;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// What a project's status page shows and who hears from it (bitfabric-internal/specs/monitoring.md, Status page): its
/// first components, the public model built for one visitor's language from the stored incidents, and pushes to its
/// followers, each in their own language. Nothing of the health report itself (keys, descriptions, times) reaches a
/// visitor: only the components' names, their states and the incidents' texts.
/// </summary>
public partial class StatusPageService
{
    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private ProjectTexts texts = default!;
    [AutoInject] private PushNotificationService pushNotificationService = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private TimeProvider timeProvider = default!;

    /// <summary>A part of the app nobody named yet takes its kind's name, as its users would say it.</summary>
    private static readonly (string Category, Func<IStringLocalizer<AppStrings>, string> Name)[] defaultComponents =
    [
        ("Platform", localizer => localizer[nameof(AppStrings.MonitoringComponentWebsiteAndApp)]),
        ("Identity", localizer => localizer[nameof(AppStrings.MonitoringComponentSignIn)]),
        ("Messaging", localizer => localizer[nameof(AppStrings.MonitoringComponentEmailSmsAndNotifications)]),
        ("AI", localizer => localizer[nameof(AppStrings.MonitoringComponentAIAssistant)]),
        ("Integrations", localizer => localizer[nameof(AppStrings.MonitoringComponentConnectedServices)])
    ];

    /// <summary>The project's page as it is, untracked, or null when its team never opened the Status page tab, which makes it.</summary>
    public async Task<StatusPage?> Find(Guid monitoringId, CancellationToken cancellationToken)
        => await dbContext.StatusPages.AsNoTracking().Include(page => page.Components)
                                      .FirstOrDefaultAsync(page => page.MonitoringId == monitoringId, cancellationToken);

    /// <summary>How many browsers follow the page by push.</summary>
    public Task<int> FollowerCountOf(Guid pageId, CancellationToken cancellationToken)
        => dbContext.StatusPageFollowers.CountAsync(follower => follower.StatusPageId == pageId, cancellationToken);

    /// <summary>The project's page, made unpublished with a component per kind of part when it has none yet.</summary>
    public async Task<StatusPage> Ensure(ProjectMonitoring project, CancellationToken cancellationToken)
    {
        var page = await dbContext.StatusPages.Include(page => page.Components)
                                              .FirstOrDefaultAsync(page => page.MonitoringId == project.Id, cancellationToken);

        if (page is not null)
            return page;

        var lastReport = await dbContext.ProjectProbes.AsNoTracking()
            .Where(probe => probe.MonitoringId == project.Id && probe.Outcome == ProjectProbeOutcome.Read)
            .OrderByDescending(probe => probe.At)
            .FirstOrDefaultAsync(cancellationToken);

        var configured = lastReport?.Checks.Where(check => check.IsNotConfigured is false && check.Key is not null).ToArray() ?? [];

        page = new()
        {
            Id = Guid.CreateSequentialGuid(),
            MonitoringId = project.Id,
            TenantId = project.TenantId,
            Slug = await UniqueSlug(project.Name!, cancellationToken),
            Languages = [CultureInfo.CurrentUICulture.Name],
            ShowsResponseTime = true
        };

        var order = 0;
        foreach (var (category, name) in defaultComponents)
        {
            string[] keys = [.. configured.Where(check => check.Category == category).Select(check => check.Key!)];
            if (keys.Length is 0) continue;

            page.Components.Add(new()
            {
                Id = Guid.CreateSequentialGuid(),
                StatusPageId = page.Id,
                TenantId = project.TenantId,
                Order = order++,
                Name = texts.InEveryLanguage(name),
                CheckKeys = keys
            });
        }

        // The app's own checks of a kind the template doesn't name join a component under that kind's name.
        foreach (var group in configured.Where(check => defaultComponents.Any(known => known.Category == check.Category) is false).GroupBy(check => check.Category ?? ""))
        {
            page.Components.Add(new()
            {
                Id = Guid.CreateSequentialGuid(),
                StatusPageId = page.Id,
                TenantId = project.TenantId,
                Order = order++,
                Name = [new() { Culture = CultureInfo.CurrentUICulture.Name, Text = string.IsNullOrEmpty(group.Key) ? project.Name : group.Key }],
                CheckKeys = [.. group.Select(check => check.Key!)]
            });
        }

        await dbContext.StatusPages.AddAsync(page, cancellationToken);

        // Two pages made at once can both find an address free; the second takes a random suffix instead.
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                return page;
            }
            catch (DbUpdateException exp) when (attempt < 3 && exp.InnerException is Npgsql.PostgresException { SqlState: "23505", ConstraintName: var constraint } && constraint?.Contains(nameof(StatusPage.Slug), StringComparison.Ordinal) is true)
            {
                page.Slug = $"{Slugify(project.Name!)}-{Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(2))}";
            }
        }
    }

    private async Task<string> UniqueSlug(string name, CancellationToken cancellationToken)
    {
        var slug = Slugify(name);
        var candidate = slug;

        for (var i = 2; await dbContext.StatusPages.IgnoreQueryFilters().AnyAsync(page => page.Slug == candidate, cancellationToken); i++)
        {
            candidate = $"{slug}-{i}";
        }

        return candidate;
    }

    /// <summary>"My App!" as "my-app"; a name with no latin letter or digit becomes "status".</summary>
    public static string Slugify(string name)
    {
        var slug = SlugRegex().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        slug = slug.Length > 50 ? slug[..50].TrimEnd('-') : slug;
        return slug.Length is 0 ? "status" : slug;
    }

    public static bool IsValidSlug(string? slug) => slug is { Length: > 0 and <= 60 } && ValidSlugRegex().IsMatch(slug);

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,58}[a-z0-9])?$")]
    private static partial Regex ValidSlugRegex();

    /// <summary>Where a visitor reaches the page: the customer's host name once its certificate is active.</summary>
    public string PublicUrlOf(StatusPage page)
        => page is { CustomHostname: { } host, CustomHostnameState: "active" }
            ? $"https://{host}"
            : $"{settings.WebAppUrl?.TrimEnd('/')}/status/{page.Slug}";

    /// <summary>The components an incident is about: those its team named, or those made of its parts.</summary>
    public static IEnumerable<StatusComponent> ComponentsOf(ProjectIncident incident, IReadOnlyCollection<StatusComponent> components)
    {
        if (incident.ComponentIds.Length > 0)
            return components.Where(component => incident.ComponentIds.Contains(component.Id));

        // The app itself didn't answer: every part of it is down.
        if (incident.CheckKeys.Contains(ProjectRollup.ReportKey))
            return components;

        return components.Where(component => component.CheckKeys.Intersect(incident.CheckKeys).Any());
    }

    /// <summary>The page as a visitor sees it, in <paramref name="culture"/> when the page has it.</summary>
    public async Task<PublicStatusDto> BuildPublic(StatusPage page, ProjectMonitoring project, string? culture, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var since = now.AddDays(-90);
        culture = CultureInfoManager.SupportedCultures.Any(supported => supported.Culture.Name == culture) ? culture : page.Languages.FirstOrDefault();

        var incidents = await dbContext.ProjectIncidents.IgnoreQueryFilters().AsNoTracking()
            .Where(incident => incident.MonitoringId == project.Id && (incident.EndedAt == null || incident.EndedAt >= since))
            .OrderByDescending(incident => incident.StartedAt)
            .ToListAsync(cancellationToken);

        var windows = await dbContext.ProjectMaintenanceWindows.IgnoreQueryFilters().AsNoTracking()
            .Where(window => window.MonitoringId == project.Id)
            .ToListAsync(cancellationToken);

        var components = page.Components.Where(component => component.IsShown).OrderBy(component => component.Order).ToArray();
        var active = incidents.Where(incident => incident.EndedAt is null && incident.Kind is ProjectIncidentKind.Incident).ToArray();

        // A page whose app bit Fabric can't read right now says so rather than look operational.
        var isStale = project.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded or ProjectConnectionStatus.Disconnected || project.LastCheckedAt is null || now - project.LastCheckedAt > TimeSpan.FromMinutes(10);

        PublicComponentDto[] publicComponents = [.. components.Select(component =>
        {
            var touching = incidents.Where(incident => incident.Kind is ProjectIncidentKind.Incident && ComponentsOf(incident, components).Contains(component)).ToArray();

            return new PublicComponentDto
            {
                Id = component.Id,
                Name = ProjectTexts.In(component.Name, culture),
                Description = ProjectTexts.In(component.Description, culture),
                State = isStale ? ProjectHealth.NoData : touching.Where(incident => incident.EndedAt is null).Select(incident => incident.Impact).DefaultIfEmpty(ProjectHealth.Operational).Max(),
                Stretches = [.. touching.Select(incident => new PublicStretchDto { From = incident.StartedAt, To = incident.EndedAt, Impact = incident.Impact })]
            };
        })];

        var times = page.ShowsResponseTime
            ? await dbContext.ProjectProbes.IgnoreQueryFilters().AsNoTracking()
                .Where(probe => probe.MonitoringId == project.Id && probe.At >= now.AddHours(-24) && probe.ResponseMs != null)
                .Select(probe => new { Ms = probe.ResponseMs!.Value, probe.VisitorMs })
                .ToListAsync(cancellationToken)
            : [];
        var medianMs = times.Select(time => time.Ms).ToList();
        var visitorMs = times.Where(time => time.VisitorMs is not null).Select(time => time.VisitorMs!.Value).ToList();

        return new()
        {
            Slug = page.Slug,
            Name = project.Name,
            AppAddress = project.Address,
            IconId = project.IconId,
            State = isStale ? ProjectHealth.NoData : active.Select(incident => incident.Impact).DefaultIfEmpty(ProjectHealth.Operational).Max(),
            InMaintenance = windows.Any(window => window.Covers(now)),
            Culture = culture,
            Languages = page.Languages,
            CheckedAt = project.LastCheckedAt,
            WatchedSince = project.CreatedOn,
            MedianResponseMs = medianMs.Count is 0 ? null : medianMs.Order().ElementAt(medianMs.Count / 2),
            MedianVisitorMs = visitorMs.Count is 0 ? null : visitorMs.Order().ElementAt(visitorMs.Count / 2),
            SupportAddress = page.SupportAddress,
            PushPublicKey = settings.AdsPushVapid?.PublicKey,
            Components = publicComponents,
            Active = [.. active.Select(incident => ToPublic(incident, components, culture, page))],
            Maintenance = [.. windows.Where(window => window.RepeatsWeekly || window.EndsAt > now && window.StartsAt < now.AddDays(30))
                                     .OrderBy(window => window.StartsAt)
                                     .Select(window => new PublicMaintenanceDto { Title = window.Title, StartsAt = window.StartsAt, EndsAt = window.EndsAt, RepeatsWeekly = window.RepeatsWeekly })],
            History = [.. incidents.Where(incident => incident.EndedAt is not null).Select(incident => ToPublic(incident, components, culture, page))]
        };
    }

    public static PublicIncidentDto ToPublic(ProjectIncident incident, IReadOnlyCollection<StatusComponent> components, string? culture, StatusPage page) => new()
    {
        Id = incident.Id,
        Kind = incident.Kind,
        Impact = incident.Impact,
        Title = ProjectTexts.In(incident.Title, culture),
        StartedAt = incident.StartedAt,
        EndedAt = incident.EndedAt,
        ComponentIds = [.. ComponentsOf(incident, components).Select(component => component.Id)],
        Updates = [.. incident.Updates.OrderByDescending(update => update.At).Select(update => new PublicIncidentUpdateDto
        {
            At = update.At,
            Phase = update.Phase,
            Text = ProjectTexts.In(update.Text, culture),
            IsFallback = culture is not null && update.Text.Any(text => text.Culture == culture) is false
        })]
    };

    /// <summary>
    /// Pushes an incident's news to the page's followers, each in their language, and only to those who follow one of its
    /// components (or all of them).
    /// </summary>
    public async Task NotifyFollowers(ProjectMonitoring project, ProjectIncident incident, string headline, CancellationToken cancellationToken)
    {
        var page = await dbContext.StatusPages.IgnoreQueryFilters().AsNoTracking()
            .Include(page => page.Components)
            .FirstOrDefaultAsync(page => page.MonitoringId == project.Id && page.IsPublished, cancellationToken);

        if (page is null)
            return;

        Guid[] affected = [.. ComponentsOf(incident, page.Components).Select(component => component.Id)];

        var followers = await dbContext.StatusPageFollowers.AsNoTracking()
            .Where(follower => follower.StatusPageId == page.Id)
            .ToListAsync(cancellationToken);

        foreach (var group in followers.Where(follower => follower.ComponentIds.Length is 0 || follower.ComponentIds.Intersect(affected).Any())
                                       .GroupBy(follower => follower.Culture))
        {
            int[] ids = [.. group.Select(follower => follower.PushNotificationSubscriptionId)];

            await pushNotificationService.RequestPush(new()
            {
                Title = $"{project.Name}: {ProjectTexts.In(incident.Title, group.Key)}",
                Message = headline.Length > 0 ? headline : ProjectTexts.In(incident.Updates.OrderByDescending(update => update.At).First().Text, group.Key),
                PageUrl = PublicUrlOf(page)
            }, subscription => ids.Contains(subscription.Id), cancellationToken);
        }
    }
}
