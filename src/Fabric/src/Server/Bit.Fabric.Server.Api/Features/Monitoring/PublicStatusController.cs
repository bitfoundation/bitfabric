using System.Net;
using System.Text;
using System.Xml.Linq;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.PushNotification;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Microsoft.AspNetCore.Cors;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// The public side of status pages (bitfabric-internal/specs/monitoring.md, Status page): anonymous, readable from
/// customers' own host names, and cached for a minute, the time between two rounds anyway. Besides the page's data:
/// following it by push, RSS and Atom, a <c>summary.json</c> in Statuspage's shape that other tools read, a calendar of
/// the maintenance, a badge, and an embed that shows in the customer's app only while something is wrong.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), AllowAnonymous, EnableCors(CorsPolicy)]
public partial class PublicStatusController : AppControllerBase, IPublicStatusController
{
    public const string CorsPolicy = "PublicStatus";

    [AutoInject] private StatusPageService statusPages = default!;
    [AutoInject] private PushNotificationService pushNotificationService = default!;
    [AutoInject] private ProjectTexts texts = default!;

    [HttpGet("{slug}")]
    public async Task<PublicStatusDto> Get(string slug, string? language, CancellationToken cancellationToken)
    {
        var (page, project) = await Find(slug, cancellationToken);
        CacheForAMinute();

        return await statusPages.BuildPublic(page, project, language, cancellationToken);
    }

    [HttpGet("{hostname}")]
    public async Task<string?> SlugOf(string hostname, CancellationToken cancellationToken)
    {
        hostname = hostname.Trim().ToLowerInvariant();

        return await DbContext.StatusPages.IgnoreQueryFilters().AsNoTracking()
            .Where(page => page.CustomHostname == hostname && page.CustomHostnameState == "active" && page.IsPublished)
            .Select(page => page.Slug)
            .FirstOrDefaultAsync(cancellationToken);
    }

    [HttpPost]
    public async Task Follow(FollowStatusPageRequestDto request, CancellationToken cancellationToken)
    {
        var (page, _) = await Find(request.Slug ?? "", cancellationToken);

        // The visitor's browser, as a row of the template's push subscriptions with no session: the same row an
        // anonymous visitor of the app itself gets.
        await pushNotificationService.Subscribe(new()
        {
            DeviceId = $"{request.P256dh}-{request.Auth}",
            Platform = "browser",
            Endpoint = request.Endpoint,
            P256dh = request.P256dh,
            Auth = request.Auth
        }, cancellationToken);

        var deviceId = $"{request.P256dh}-{request.Auth}";
        var subscription = await DbContext.PushNotificationSubscriptions.IgnoreQueryFilters()
            .Where(subscription => subscription.DeviceId == deviceId)
            .OrderByDescending(subscription => subscription.Id)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new BadRequestException();

        var culture = CultureInfoManager.SupportedCultures.Any(supported => supported.Culture.Name == request.Culture) ? request.Culture! : page.Languages.FirstOrDefault() ?? "en-US";
        Guid[] componentIds = [.. request.ComponentIds.Where(id => page.Components.Any(component => component.Id == id))];

        var follower = await DbContext.StatusPageFollowers.FirstOrDefaultAsync(follower => follower.StatusPageId == page.Id && follower.PushNotificationSubscriptionId == subscription.Id, cancellationToken);

        if (follower is null)
        {
            await DbContext.StatusPageFollowers.AddAsync(new()
            {
                Id = Guid.CreateSequentialGuid(),
                StatusPageId = page.Id,
                PushNotificationSubscriptionId = subscription.Id,
                Culture = culture,
                ComponentIds = componentIds,
                FollowedAt = TimeProvider.GetUtcNow()
            }, cancellationToken);
        }
        else
        {
            follower.Culture = culture;
            follower.ComponentIds = componentIds;
        }

        await DbContext.SaveChangesAsync(cancellationToken);
    }

    [HttpPost]
    public async Task Unfollow(FollowStatusPageRequestDto request, CancellationToken cancellationToken)
    {
        var deviceId = $"{request.P256dh}-{request.Auth}";

        // An anonymous visitor has no team, so the page's tenant filter would hide every page.
        await DbContext.StatusPageFollowers.IgnoreQueryFilters()
            .Where(follower => follower.StatusPage!.Slug == request.Slug && follower.PushNotificationSubscription!.DeviceId == deviceId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>RSS 2.0 of the incidents and announcements of the last 90 days.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Rss(string slug, string? language, CancellationToken cancellationToken)
    {
        var (status, url) = await Status(slug, language, cancellationToken);

        XElement channel = new("channel",
            new XElement("title", InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringAppStatus), status.Name ?? ""])),
            new XElement("link", url),
            new XElement("description", InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringFeedDescription), status.Name ?? ""])),
            new XElement("language", status.Culture),
            Items(status).Select(incident => new XElement("item",
                new XElement("title", incident.Title),
                new XElement("link", $"{url}#{incident.Id}"),
                new XElement("guid", new XAttribute("isPermaLink", "false"), incident.Id),
                new XElement("pubDate", incident.StartedAt.ToString("r", CultureInfo.InvariantCulture)),
                new XElement("description", incident.Updates.FirstOrDefault()?.Text))));

        return Xml(new XDocument(new XElement("rss", new XAttribute("version", "2.0"), channel)), "application/rss+xml");
    }

    /// <summary>Atom of the incidents and announcements of the last 90 days.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Atom(string slug, string? language, CancellationToken cancellationToken)
    {
        var (status, url) = await Status(slug, language, cancellationToken);
        XNamespace atom = "http://www.w3.org/2005/Atom";
        var items = Items(status).ToArray();

        XElement feed = new(atom + "feed",
            new XAttribute(XNamespace.Xml + "lang", status.Culture ?? "en-US"),
            new XElement(atom + "title", InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringAppStatus), status.Name ?? ""])),
            new XElement(atom + "id", url),
            new XElement(atom + "link", new XAttribute("href", url)),
            new XElement(atom + "updated", (items.FirstOrDefault()?.Updates.FirstOrDefault()?.At ?? status.WatchedSince).ToString("o", CultureInfo.InvariantCulture)),
            items.Select(incident => new XElement(atom + "entry",
                new XElement(atom + "title", incident.Title),
                new XElement(atom + "id", $"urn:uuid:{incident.Id}"),
                new XElement(atom + "link", new XAttribute("href", $"{url}#{incident.Id}")),
                new XElement(atom + "updated", (incident.Updates.FirstOrDefault()?.At ?? incident.StartedAt).ToString("o", CultureInfo.InvariantCulture)),
                new XElement(atom + "summary", incident.Updates.FirstOrDefault()?.Text))));

        return Xml(new XDocument(feed), "application/atom+xml");
    }

    /// <summary>The page in Atlassian Statuspage's <c>summary.json</c> shape, which monitoring tools and widgets read.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Summary(string slug, CancellationToken cancellationToken)
    {
        var (status, url) = await Status(slug, null, cancellationToken);

        static string ComponentStatus(ProjectHealth health, bool maintenance) => maintenance ? "under_maintenance" : health switch
        {
            ProjectHealth.Down => "major_outage",
            ProjectHealth.Degraded => "partial_outage",
            _ => "operational"
        };

        var indicator = status.State switch
        {
            ProjectHealth.Down => "major",
            ProjectHealth.Degraded => "minor",
            _ => status.InMaintenance ? "maintenance" : "none"
        };

        var body = new
        {
            page = new { id = status.Slug, name = status.Name, url, time_zone = "Etc/UTC", updated_at = status.CheckedAt },
            components = status.Components.Select(component => new
            {
                id = component.Id,
                name = component.Name,
                description = component.Description,
                status = ComponentStatus(component.State, status.InMaintenance && component.State is ProjectHealth.Operational)
            }),
            incidents = status.Active.Select(incident => new
            {
                id = incident.Id,
                name = incident.Title,
                status = PhaseName(incident.Updates.FirstOrDefault()?.Phase),
                impact = incident.Impact is ProjectHealth.Down ? "major" : "minor",
                created_at = incident.StartedAt,
                shortlink = $"{url}#{incident.Id}",
                incident_updates = incident.Updates.Select(update => new { status = PhaseName(update.Phase), body = update.Text, created_at = update.At })
            }),
            scheduled_maintenances = status.Maintenance.Select(window => new { name = window.Title, scheduled_for = window.StartsAt, scheduled_until = window.EndsAt }),
            status = new
            {
                indicator,
                description = indicator is "none"
                    ? InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringSummaryAllSystemsOperational)])
                    : status.Active.FirstOrDefault()?.Title ?? InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringUnderMaintenance)])
            }
        };

        return new JsonResult(body, JsonSerializerOptions.Web);
    }

    /// <summary>The maintenance windows as an iCalendar, so a visitor's calendar shows them; a weekly one repeats there too.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Calendar(string slug, CancellationToken cancellationToken)
    {
        var (status, url) = await Status(slug, null, cancellationToken);

        StringBuilder ics = new();
        ics.Append("BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//bit Fabric//Status//EN\r\n");
        ics.Append($"X-WR-CALNAME:{Escape(InLanguageOf(status, localizer => localizer[nameof(AppStrings.MonitoringMaintenanceOf), status.Name ?? ""]))}\r\n");

        foreach (var window in status.Maintenance)
        {
            ics.Append("BEGIN:VEVENT\r\n");
            ics.Append($"UID:{window.StartsAt.UtcTicks}-{status.Slug}@bitfabric\r\n");
            ics.Append($"DTSTAMP:{TimeProvider.GetUtcNow():yyyyMMdd'T'HHmmss'Z'}\r\n");
            ics.Append($"DTSTART:{window.StartsAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n");
            ics.Append($"DTEND:{window.EndsAt.UtcDateTime:yyyyMMdd'T'HHmmss'Z'}\r\n");
            if (window.RepeatsWeekly)
            {
                ics.Append("RRULE:FREQ=WEEKLY\r\n");
            }
            ics.Append($"SUMMARY:{Escape(window.Title ?? "")}\r\n");
            ics.Append($"URL:{url}\r\n");
            ics.Append("END:VEVENT\r\n");
        }

        ics.Append("END:VCALENDAR\r\n");

        return Content(ics.ToString(), "text/calendar", Encoding.UTF8);
    }

    /// <summary>A badge for a README or a site: the app's state in a word, in its color.</summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Badge(string slug, CancellationToken cancellationToken)
    {
        var (status, _) = await Status(slug, "en-US", cancellationToken);

        var (word, color) = status.State switch
        {
            ProjectHealth.Down => ("outage", "#d13438"),
            ProjectHealth.Degraded => ("degraded", "#c19c00"),
            ProjectHealth.NoData => ("unknown", "#8a8886"),
            _ => status.InMaintenance ? ("maintenance", "#0078d4") : ("operational", "#107c10")
        };

        // Widths by character count: an approximation that keeps the badge free of a font measurement.
        var labelWidth = 6 * "status".Length + 12;
        var valueWidth = 6 * word.Length + 12;

        var svg = $"""
            <svg xmlns="http://www.w3.org/2000/svg" width="{labelWidth + valueWidth}" height="20" role="img" aria-label="status: {word}">
              <title>{WebUtility.HtmlEncode(status.Name)} status: {word}</title>
              <rect width="{labelWidth}" height="20" fill="#555"/>
              <rect x="{labelWidth}" width="{valueWidth}" height="20" fill="{color}"/>
              <g fill="#fff" font-family="Verdana,DejaVu Sans,sans-serif" font-size="11" text-anchor="middle">
                <text x="{labelWidth / 2}" y="14">status</text>
                <text x="{labelWidth + valueWidth / 2}" y="14">{word}</text>
              </g>
            </svg>
            """;

        return Content(svg, "image/svg+xml", Encoding.UTF8);
    }

    /// <summary>
    /// A script for the customer's app that shows a bar only during an incident or maintenance, linking to the page:
    /// <c>&lt;script src=".../Embed/{slug}" async&gt;&lt;/script&gt;</c>.
    /// </summary>
    [HttpGet("{slug}")]
    public async Task<IActionResult> Embed(string slug, CancellationToken cancellationToken)
    {
        var (status, url) = await Status(slug, null, cancellationToken);

        var message = status.Active.FirstOrDefault()?.Title ?? (status.InMaintenance ? status.Maintenance.FirstOrDefault()?.Title : null);

        if (message is null)
            return Content("/* All systems operational */", "text/javascript", Encoding.UTF8);

        var color = status.State is ProjectHealth.Down ? "#d13438" : status.InMaintenance && status.Active.Length is 0 ? "#0078d4" : "#c19c00";
        // As JavaScript string literals: JSON's are, escaping included.
        var text = JsonSerializer.Serialize(message, JsonSerializerOptions.Web);
        var link = JsonSerializer.Serialize(url, JsonSerializerOptions.Web);

        return Content($$"""
            (function () {
              var bar = document.createElement('a');
              bar.href = {{link}};
              bar.target = '_blank';
              bar.rel = 'noopener';
              bar.textContent = {{text}};
              bar.setAttribute('role', 'status');
              bar.style.cssText = 'display:block;padding:8px 16px;background:{{color}};color:#fff;font:14px/1.4 system-ui,sans-serif;text-align:center;text-decoration:none;z-index:2147483647;position:relative';
              (document.body || document.documentElement).insertBefore(bar, (document.body || document.documentElement).firstChild);
            })();
            """, "text/javascript", Encoding.UTF8);
    }

    private async Task<(StatusPage Page, Project Project)> Find(string slug, CancellationToken cancellationToken)
    {
        var page = await DbContext.StatusPages.IgnoreQueryFilters().AsNoTracking()
            .Include(page => page.Components)
            .FirstOrDefaultAsync(page => page.Slug == slug && page.IsPublished, cancellationToken)
            ?? throw new ResourceNotFoundException(Localizer[nameof(AppStrings.ResourceNotFoundException)]);

        var project = await DbContext.Projects.IgnoreQueryFilters().AsNoTracking().FirstAsync(project => project.Id == page.ProjectId, cancellationToken);

        return (page, project);
    }

    private async Task<(PublicStatusDto Status, string Url)> Status(string slug, string? language, CancellationToken cancellationToken)
    {
        var (page, project) = await Find(slug, cancellationToken);
        CacheForAMinute();

        return (await statusPages.BuildPublic(page, project, language, cancellationToken), statusPages.PublicUrlOf(page));
    }

    /// <summary>bit Fabric's own words in a feed, in the feed's language rather than the request's.</summary>
    private string InLanguageOf(PublicStatusDto status, Func<IStringLocalizer<AppStrings>, string> write)
        => ProjectTexts.In(texts.InEveryLanguage(write), status.Culture) ?? "";

    private static IEnumerable<PublicIncidentDto> Items(PublicStatusDto status) => status.Active.Concat(status.History).OrderByDescending(incident => incident.StartedAt);

    private static string PhaseName(ProjectIncidentPhase? phase) => phase switch
    {
        ProjectIncidentPhase.Identified => "identified",
        ProjectIncidentPhase.Monitoring => "monitoring",
        ProjectIncidentPhase.Resolved => "resolved",
        _ => "investigating"
    };

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\n", "\\n");

    private ContentResult Xml(XDocument document, string contentType)
        => Content(document.Declaration is null ? $"<?xml version=\"1.0\" encoding=\"utf-8\"?>\n{document}" : document.ToString(), contentType, Encoding.UTF8);

    private void CacheForAMinute() => Response.Headers.CacheControl = "public, max-age=60";
}
