using System.ComponentModel;
using System.Reflection;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// The scans on bit Fabric's own /mcp (every feature also gets tools there), for
/// the customer's AI agent: the latest scans of a project with what to fix, its past runs, and running them now. Every
/// read goes through the caller's current team, exactly as the Scans tab's (See ProjectScansController), and every text
/// is in the caller's language, with the prompts in English as the tab copies them.
/// </summary>
[McpServerToolType]
public partial class ProjectScansMcpTools : IAsyncDisposable
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    [AutoInject] private AppDbContext dbContext = default!;
    [AutoInject] private ProjectScanReader reader = default!;
    [AutoInject] private ProjectScanScheduler scheduler = default!;
    [AutoInject] private IHttpContextAccessor httpContextAccessor = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;

    [McpServerTool(Name = nameof(GetProjectScans), Title = "Show a project's scans", ReadOnly = true, OpenWorld = false), Description("Returns the latest security, performance, SEO, accessibility and domain and certificate scans bit Fabric ran on one of the user's monitored projects: each scan's grade or score, when and why it ran, and its findings ranked most urgent first, each with what it means, why it matters, the fix, where it was seen and a prompt for an AI coding agent; plus one prompt that fixes them all. Performance and accessibility also list each page and device Lighthouse measured with its score, and performance its Core Web Vitals: LCP, CLS, TBT (lab stand-in for INP), FCP, Speed Index and time to first byte, in milliseconds except CLS. Scans waiting or running are listed too. The scans read only what any visitor of the app sees.")]
    public async Task<string> GetProjectScans(
        [Description("The project: its id, its name, or its address or host name as the user says it, such as 'bluebird.app'.")] string project,
        CancellationToken cancellationToken)
    {
        var (found, problem) = await Find(project, cancellationToken);
        if (found is null)
            return problem!;

        var scans = await reader.Latest(found, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            Project = new { found.Id, found.Name, found.Address, found.ServerAddress },
            Latest = scans.Latest.Select(scan => Describe(scan, found.Address, withFindings: true)),
            Pending = scans.Pending.Select(scan => new { scan.Id, scan.Kind, scan.Trigger, scan.RequestedAt, scan.StartedAt }),
            FixEverythingPrompt = scans.Latest.Any(scan => scan.Findings.Length > 0)
                ? ScanPrompts.ForAll(found.Address, scans.Latest.SelectMany(scan => scan.Findings.Select(finding => (scan.Kind, finding))))
                : null,
            scans.Settings.ScanAfterDeployments,
            scans.Settings.WeeklyScans,
            scans.Settings.WeeklyEmail,
            RunScansAgainAt = scans.CanRunAgainAt
        }, jsonOptions);
    }

    [McpServerTool(Name = nameof(GetProjectScanHistory), Title = "Show a project's past scans", ReadOnly = true, OpenWorld = false), Description("Returns the past scans of one of the user's monitored projects, newest first, up to 50: each run's kind, why it ran (after a deployment, weekly, by hand, or the first after connecting), when, its grade or score, and how many urgent, important and improvement findings it had. Use GetProjectScans for the findings of the latest ones.")]
    public async Task<string> GetProjectScanHistory(
        [Description("The project: its id, its name, or its address or host name as the user says it.")] string project,
        [Description("Only this kind of scan: Security, Performance, Seo, Accessibility or DomainAndCertificate. Leave empty for every kind.")] string? kind = null,
        CancellationToken cancellationToken = default)
    {
        var (found, problem) = await Find(project, cancellationToken);
        if (found is null)
            return problem!;

        ProjectScanKind? onlyKind = null;
        if (string.IsNullOrWhiteSpace(kind) is false)
        {
            if (Enum.TryParse<ProjectScanKind>(kind.Replace(" ", "", StringComparison.Ordinal), ignoreCase: true, out var parsed) is false || ScanFindings.AvailableKinds.Contains(parsed) is false)
                return $"There is no '{kind}' scan. The kinds are {string.Join(", ", ScanFindings.AvailableKinds)}.";

            onlyKind = parsed;
        }

        var history = await reader.History(found.Id, onlyKind, take: 50, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            Project = new { found.Id, found.Name, found.Address },
            Runs = history.Select(scan => Describe(scan, found.Address, withFindings: false))
        }, jsonOptions);
    }

    [McpServerTool(Name = nameof(RunProjectScans), Title = "Run a project's scans now", ReadOnly = false, Destructive = false, Idempotent = false, OpenWorld = true), Description("Starts every scan (security, performance, SEO, accessibility, domain and certificate) of one of the user's monitored projects now; their results are ready through GetProjectScans a minute or two later, performance and accessibility within a few minutes, as Lighthouse loads each page in a browser. Always ask the user to confirm before calling it, naming the project. It can run at most once every 10 minutes a project, and reads only what any visitor of the app sees: it never signs in or tries a password.")]
    public async Task<string> RunProjectScans(
        [Description("The project: its id, its name, or its address or host name as the user says it.")] string project,
        CancellationToken cancellationToken)
    {
        var (found, problem) = await Find(project, cancellationToken);
        if (found is null)
            return problem!;

        if (await scheduler.RunByHand(found, httpContextAccessor.HttpContext!.User.GetUserId(), cancellationToken) is { } tryAgainIn)
            return localizer[nameof(AppStrings.MonitoringScansRunNowLimit), (int)ProjectScanReader.RunNowInterval.TotalMinutes, (int)Math.Ceiling(tryAgainIn.TotalMinutes)];

        return $"{localizer[nameof(AppStrings.MonitoringScansStarted)]} ({found.Name}, {found.Address})";
    }

    [McpServerTool(Name = nameof(SetProjectScanSettings), Title = "Change when a project is scanned", ReadOnly = false, Destructive = false, Idempotent = true, OpenWorld = false), Description("Changes when bit Fabric scans one of the user's monitored projects, as the settings of its Scans tab do: scans after every deployment bit Fabric sees in the health check (off until the owner allows it), weekly scans, and whether the project is in the weekly scans email every member of the team gets with the latest scans and their urgent findings. Leave out what shouldn't change. It changes it for the whole team, so first tell the user exactly what will change, naming the project, and call it only once they have confirmed in the conversation.")]
    public async Task<string> SetProjectScanSettings(
        [Description("The project: its id, its name, or its address or host name as the user says it.")] string project,
        [Description("True to scan after every deployment, false to stop; leave out to keep it.")] bool? scanAfterDeployments = null,
        [Description("True to scan every week, false to stop; leave out to keep it.")] bool? weeklyScans = null,
        [Description("True to have the project in the weekly scans email, false to leave it out; leave out to keep it.")] bool? weeklyEmail = null,
        CancellationToken cancellationToken = default)
    {
        var (found, problem) = await Find(project, cancellationToken);
        if (found is null)
            return problem!;

        var settings = await reader.Settings(found.Id, cancellationToken);
        settings.ScanAfterDeployments = scanAfterDeployments ?? settings.ScanAfterDeployments;
        settings.WeeklyScans = weeklyScans ?? settings.WeeklyScans;
        settings.WeeklyEmail = weeklyEmail ?? settings.WeeklyEmail;

        var saved = await reader.SaveSettings(found, settings, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            Project = new { found.Id, found.Name, found.Address },
            saved.ScanAfterDeployments,
            saved.WeeklyScans,
            saved.WeeklyEmail
        }, jsonOptions);
    }

    private object Describe(ProjectScanDto scan, string? address, bool withFindings) => new
    {
        scan.Id,
        scan.Kind,
        Name = KindName(scan.Kind),
        scan.Trigger,
        scan.ApplicationVersion,
        scan.RequestedAt,
        scan.FinishedAt,
        scan.Outcome,
        scan.Verdict,
        scan.Grade,
        scan.Score,
        scan.ExpiresAt,
        scan.UrgentCount,
        scan.ImportantCount,
        scan.ImprovementCount,
        // The browser scans' pages and devices, the home page on a phone first, and Performance's Core Web Vitals.
        Measured = scan.Pages.Length is 0 ? null : scan.Pages.Select(page => new
        {
            page.Path,
            page.Device,
            page.Score,
            page.LargestContentfulPaintMs,
            page.CumulativeLayoutShift,
            page.TotalBlockingTimeMs,
            page.FirstContentfulPaintMs,
            page.SpeedIndexMs,
            page.TimeToFirstByteMs
        }),
        Findings = withFindings ? scan.Findings.Select(finding =>
        {
            var texts = ScanFindings.Localize(localizer, finding);
            return new
            {
                finding.Id,
                finding.Key,
                finding.Severity,
                WhatItMeans = texts?.Title,
                WhyItMatters = texts?.Why,
                TheFix = texts?.Fix,
                finding.Where,
                finding.IsReportedWrong,
                PromptForAnAIAgent = ScanPrompts.For(address, scan.Kind, finding)
            };
        }) : null
    };

    private string KindName(ProjectScanKind kind) => kind switch
    {
        ProjectScanKind.Security => localizer[nameof(AppStrings.MonitoringScanSecurity)],
        ProjectScanKind.Seo => localizer[nameof(AppStrings.MonitoringScanSeo)],
        ProjectScanKind.DomainAndCertificate => localizer[nameof(AppStrings.MonitoringScanDomainAndCertificate)],
        ProjectScanKind.Performance => localizer[nameof(AppStrings.MonitoringScanPerformance)],
        _ => localizer[nameof(AppStrings.MonitoringScanAccessibility)]
    };

    /// <summary>
    /// The current team's project the user named: by id, by its name, or by its address or host. Like the controllers'
    /// TENANT_SELECTED policy, nothing is read without a team, which would be the fallback tenant every team-less user shares.
    /// </summary>
    private async Task<(ProjectMonitoring? Project, string? Problem)> Find(string named, CancellationToken cancellationToken)
    {
        if (httpContextAccessor.HttpContext?.User.GetTenantId() is null)
            return (null, "The user has no team selected in bit Fabric. Ask them to open bit Fabric's projects page, which makes or picks their team, then try again.");

        var projects = await dbContext.Monitorings.AsNoTracking().ToListAsync(cancellationToken);
        var wanted = named.Trim();

        var host = Uri.TryCreate(wanted.Contains("://", StringComparison.Ordinal) ? wanted : $"https://{wanted}", UriKind.Absolute, out var uri) ? uri.Host : wanted;

        ProjectMonitoring[] matches = Guid.TryParse(wanted, out var id)
            ? [.. projects.Where(project => project.Id == id)]
            : [.. projects.Where(project => string.Equals(project.Name, wanted, StringComparison.OrdinalIgnoreCase))];

        if (matches.Length is 0)
        {
            matches = [.. projects.Where(project => string.Equals(HostOf(project.Address), host, StringComparison.OrdinalIgnoreCase)
                                                    || string.Equals(HostOf(project.ServerAddress), host, StringComparison.OrdinalIgnoreCase))];
        }

        if (matches.Length is 0)
        {
            matches = [.. projects.Where(project => project.Name?.Contains(wanted, StringComparison.OrdinalIgnoreCase) is true
                                                    || project.Address?.Contains(wanted, StringComparison.OrdinalIgnoreCase) is true)];
        }

        return matches.Length switch
        {
            1 => (matches[0], null),
            0 => (null, $"No project of the user's team matches '{named}'. Their projects are: {Listing(projects)}"),
            _ => (null, $"Several projects match '{named}'; ask the user which one: {Listing(matches)}")
        };
    }

    private static string? HostOf(string? address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : null;

    private static string Listing(IEnumerable<ProjectMonitoring> projects)
        => projects.Any() ? string.Join("; ", projects.Select(project => $"{project.Name} ({project.Address}, id {project.Id})")) : "none yet.";

    /// <summary>
    /// The tools for the in-app assistant, each run in a scope of its own (See ProjectMonitoringMcpTools.AIFunctions), so
    /// "how secure is my app?" gets the same answer there as in grok.com.
    /// </summary>
    public static IEnumerable<AIFunction> AIFunctions(IServiceProvider services)
    {
        return typeof(ProjectScansMcpTools).GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Select(method => (Method: method, Tool: method.GetCustomAttribute<McpServerToolAttribute>()))
            .Where(tool => tool.Tool is not null)
            .Select(tool => AIFunctionFactory.Create(tool.Method, _ => InScopeOfItsOwn(services), new AIFunctionFactoryOptions { Name = tool.Tool!.Name ?? tool.Method.Name }));
    }

    private AsyncServiceScope? ownScope;

    private static ProjectScansMcpTools InScopeOfItsOwn(IServiceProvider services)
    {
        var scope = services.CreateAsyncScope();
        var tools = ActivatorUtilities.CreateInstance<ProjectScansMcpTools>(scope.ServiceProvider);
        tools.ownScope = scope;
        return tools;
    }

    public async ValueTask DisposeAsync()
    {
        if (ownScope is { } scope)
        {
            await scope.DisposeAsync();
        }
    }
}
