using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The security scan (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13): the headers, cookies and HTTPS of the
/// home page, graded A+ to F the way Mozilla Observatory grades a site (its scoring rebuilt here, MPL-2.0 ideas, never its
/// API: a baseline of 100, a penalty per test that fails, bonuses only from 90 up), plus the bit app's own endpoints that
/// must ask for sign-in, each read once anonymously on the server's address. Nothing signs in and no password is tried.
/// </summary>
public partial class SecurityScanner(ScanHttp http, ILogger<SecurityScanner> logger) : IProjectScanner
{
    /// <summary>Six months, the least max-age Observatory counts as HSTS.</summary>
    private const int MinHstsSeconds = 15_768_000;

    public ProjectScanKind Kind => ProjectScanKind.Security;

    /// <summary>The paths of the template's own endpoints and how each looks when it answers someone who didn't sign in.</summary>
    private static readonly (string Path, string Key, ScanFindingSeverity Severity, int Penalty, Func<ScanResponse, bool> IsOpen)[] bitEndpoints =
    [
        // MapAppHealthChecks: /healthz and /healthz/v1 need AppFeatures.System.Operations_View; /health and /alive are public by design.
        ("/healthz", ScanFindings.HealthzOpen, ScanFindingSeverity.Urgent, -25, response => response.IsOk && response.IsJson),
        // UseHangfireDashboard's default path, behind HangfireDashboardAuthorizationFilter.
        ("/hangfire", ScanFindings.HangfireOpen, ScanFindingSeverity.Urgent, -25, response => response.IsOk && response.Body?.Contains("hangfire", StringComparison.OrdinalIgnoreCase) is true),
        // OAuthResources.DevMcpPath. The MCP transport answers a GET it let through with 400 or 406 (no session, no event
        // stream asked for), or opens a stream; a closed one says 401 first.
        ("/dev-mcp", ScanFindings.DevMcpOpen, ScanFindingSeverity.Urgent, -25, response => response.Status is 400 or 406 || (response.IsOk && response.IsHtml is false)),
        // MapScalarApiReference's and MapOpenApi's default routes, mapped outside Production only.
        ("/scalar", ScanFindings.ApiDocsOpen, ScanFindingSeverity.Urgent, -25, response => response.IsOk && response.Body?.Contains("scalar", StringComparison.OrdinalIgnoreCase) is true),
        ("/openapi/v1.json", ScanFindings.ApiDocsOpen, ScanFindingSeverity.Urgent, -25, response => response.IsOk && response.IsJson && response.Body?.Contains("\"openapi\"", StringComparison.Ordinal) is true),
        // The prefix the template keeps for OData's routes (service-worker and apple-app-site-association leave /odata/ to the server).
        ("/odata/$metadata", ScanFindings.ODataMetadataOpen, ScanFindingSeverity.Important, -10, response => response.IsOk && (response.MediaType?.Contains("xml", StringComparison.OrdinalIgnoreCase) is true || response.IsJson))
    ];

    public async Task<ProjectScanResult> Scan(ProjectMonitoring project, ProjectScan scan, CancellationToken cancellationToken)
    {
        var home = await http.Get($"{project.Address}/", cancellationToken, followRedirects: true);

        if (home is null)
            return ProjectScanResult.Unreachable();

        ProjectScanResult result = new();
        var penalties = 0;
        var bonuses = 0;

        void Add(string key, ScanFindingSeverity severity, int penalty, string[]? parameters = null, string[]? where = null)
        {
            // A test counts once however many places fail it.
            if (result.Findings.Any(finding => finding.Key == key) is false)
            {
                penalties += penalty;
            }

            result.Findings.Add(new(key, severity, parameters ?? [], where ?? []));
        }

        var homePath = home.Url.PathAndQuery;
        var isHttps = home.Url.Scheme == Uri.UriSchemeHttps;

        if (isHttps)
        {
            await CheckHttpsRedirect(project, Add, cancellationToken);
            CheckHsts(home, homePath, Add);
        }

        bonuses += CheckContentSecurityPolicy(home, homePath, Add);
        CheckFrameProtection(home, homePath, Add);

        if (string.Equals(home.Header("X-Content-Type-Options")?.Trim(), "nosniff", StringComparison.OrdinalIgnoreCase) is false)
        {
            Add(ScanFindings.NosniffMissing, ScanFindingSeverity.Improvement, -5, where: [homePath]);
        }

        bonuses += CheckReferrerPolicy(home, homePath, Add);
        bonuses += CheckCookies(home, isHttps, homePath, Add);
        CheckServerDisclosure(home, project, Add);

        foreach (var endpoint in bitEndpoints)
        {
            try
            {
                var response = await http.Get($"{project.ServerAddress}{endpoint.Path}", cancellationToken, readBody: endpoint.Key is not ScanFindings.DevMcpOpen);

                if (response is not null && endpoint.IsOpen(response))
                {
                    Add(endpoint.Key, endpoint.Severity, endpoint.Penalty, where: [$"{project.ServerAddress}{endpoint.Path}"]);
                }
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                // One endpoint that can't be read leaves the others to be.
                logger.LogWarning(exp, "Reading {Path} of project {MonitoringId} failed.", endpoint.Path, project.Id);
            }
        }

        var score = 100 + penalties;
        // Observatory's rule: bonuses only lift a score that is already at least 90.
        if (score >= 90)
        {
            score += bonuses;
        }

        result.Score = Math.Clamp(score, 0, 135);
        result.Grade = GradeOf(result.Score.Value);
        result.Judge(goodFrom: 80, poorBelow: 50);

        return result;
    }

    /// <summary>Mozilla Observatory's grade bands.</summary>
    public static string GradeOf(int score) => score switch
    {
        >= 100 => "A+",
        >= 90 => "A",
        >= 85 => "A-",
        >= 80 => "B+",
        >= 70 => "B",
        >= 65 => "B-",
        >= 60 => "C+",
        >= 50 => "C",
        >= 45 => "C-",
        >= 40 => "D+",
        >= 30 => "D",
        >= 25 => "D-",
        _ => "F"
    };

    private delegate void Finding(string key, ScanFindingSeverity severity, int penalty, string[]? parameters = null, string[]? where = null);

    /// <summary>The http:// address should answer with a redirect to https://. An http port that doesn't answer at all needs nothing.</summary>
    private async Task CheckHttpsRedirect(ProjectMonitoring project, Finding add, CancellationToken cancellationToken)
    {
        var httpAddress = $"http://{new Uri(project.Address!).Host}/";
        var response = await http.Get(httpAddress, cancellationToken, readBody: false);

        if (response is null)
            return;

        var location = response.Status is >= 300 and < 400 && response.Headers.TryGetValue("Location", out var values) ? values.FirstOrDefault() : null;
        var target = location is null ? null : new Uri(new Uri(httpAddress), location);

        if (target?.Scheme != Uri.UriSchemeHttps)
        {
            add(ScanFindings.HttpsRedirectMissing, ScanFindingSeverity.Important, -20, where: [httpAddress]);
        }
    }

    private static void CheckHsts(ScanResponse home, string where, Finding add)
    {
        var hsts = home.Header("Strict-Transport-Security");

        if (hsts is null)
        {
            add(ScanFindings.HstsMissing, ScanFindingSeverity.Important, -20, where: [where]);
            return;
        }

        var maxAge = hsts.Split(';', StringSplitOptions.TrimEntries)
                         .Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries))
                         .FirstOrDefault(part => part[0].Equals("max-age", StringComparison.OrdinalIgnoreCase) && part.Length is 2);

        var seconds = maxAge is not null && long.TryParse(maxAge[1].Trim('"'), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;

        if (seconds < MinHstsSeconds)
        {
            add(ScanFindings.HstsShort, ScanFindingSeverity.Improvement, -10, [(seconds / 86_400).ToString(CultureInfo.InvariantCulture)], [where]);
        }
    }

    /// <summary>The policy of the header, or of a meta tag when there is no header. Returns its bonus.</summary>
    private static int CheckContentSecurityPolicy(ScanResponse home, string where, Finding add)
    {
        var policy = home.Headers.TryGetValue("Content-Security-Policy", out var headers) && headers.Length > 0
            ? headers[0]
            : MetaContent(home.Body, "http-equiv", "Content-Security-Policy");

        if (string.IsNullOrWhiteSpace(policy))
        {
            add(ScanFindings.CspMissing, ScanFindingSeverity.Important, -25, where: [where]);
            return 0;
        }

        var directives = Directives(policy);
        var scripts = directives.GetValueOrDefault("script-src") ?? directives.GetValueOrDefault("default-src");

        // A nonce, a hash or 'strict-dynamic' makes a browser ignore 'unsafe-inline'; no script-src nor default-src restricts nothing.
        var allowsInline = scripts is null
            || (scripts.Contains("'unsafe-inline'") && scripts.Any(source => source.StartsWith("'nonce-", StringComparison.Ordinal) || source.StartsWith("'sha", StringComparison.Ordinal) || source is "'strict-dynamic'") is false);

        if (allowsInline)
        {
            add(ScanFindings.CspUnsafeInline, ScanFindingSeverity.Important, -20, where: [where]);
        }

        if (scripts?.Contains("'unsafe-eval'") is true)
        {
            add(ScanFindings.CspUnsafeEval, ScanFindingSeverity.Improvement, -10, where: [where]);
        }

        return directives.GetValueOrDefault("default-src") is ["'none'"] ? 10 : 0;
    }

    /// <summary><c>frame-ancestors</c> counts only in a header, which is where browsers read it.</summary>
    private static void CheckFrameProtection(ScanResponse home, string where, Finding add)
    {
        var headerPolicy = home.Headers.TryGetValue("Content-Security-Policy", out var headers) ? string.Join(";", headers) : null;
        var hasFrameAncestors = headerPolicy is not null && Directives(headerPolicy).ContainsKey("frame-ancestors");
        var frameOptions = home.Header("X-Frame-Options")?.Trim();

        if (hasFrameAncestors is false && frameOptions?.ToUpperInvariant() is not ("DENY" or "SAMEORIGIN"))
        {
            add(ScanFindings.FrameProtectionMissing, ScanFindingSeverity.Important, -20, where: [where]);
        }
    }

    private static readonly string[] privateReferrerPolicies = ["no-referrer", "same-origin", "strict-origin", "strict-origin-when-cross-origin"];

    private static int CheckReferrerPolicy(ScanResponse home, string where, Finding add)
    {
        var policy = home.Header("Referrer-Policy") ?? MetaContent(home.Body, "name", "referrer");

        if (string.IsNullOrWhiteSpace(policy))
        {
            // Observatory takes no points for it; it's still worth setting.
            add(ScanFindings.ReferrerPolicyMissing, ScanFindingSeverity.Improvement, 0, where: [where]);
            return 0;
        }

        // The last policy a browser understands wins.
        var effective = policy.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.ToLowerInvariant();

        return privateReferrerPolicies.Contains(effective) ? 5 : 0;
    }

    /// <summary>Returns the bonus of cookies that all say SameSite, as Observatory gives it.</summary>
    private static int CheckCookies(ScanResponse home, bool isHttps, string where, Finding add)
    {
        var cookies = home.Cookies.Select(Cookie.Parse).Where(cookie => cookie is { IsDeletion: false }).Cast<Cookie>().DistinctBy(cookie => cookie.Name).ToArray();

        if (cookies.Length is 0)
            return 0;

        if (isHttps && cookies.Where(cookie => cookie.Secure is false).Select(cookie => cookie.Name).ToArray() is { Length: > 0 } notSecure)
        {
            add(ScanFindings.CookieNotSecure, ScanFindingSeverity.Important, -20, [string.Join(", ", notSecure)], [where]);
        }

        // Only a cookie that holds a session must stay away from scripts; a culture or a theme cookie may be read by them.
        if (cookies.Where(cookie => cookie.HttpOnly is false && cookie.LooksLikeASession).Select(cookie => cookie.Name).ToArray() is { Length: > 0 } readable)
        {
            add(ScanFindings.CookieNotHttpOnly, ScanFindingSeverity.Important, -30, [string.Join(", ", readable)], [where]);
        }

        var noSameSite = cookies.Where(cookie => cookie.SameSite is null).Select(cookie => cookie.Name).ToArray();

        if (noSameSite.Length > 0)
        {
            add(ScanFindings.CookieNoSameSite, ScanFindingSeverity.Improvement, 0, [string.Join(", ", noSameSite)], [where]);
            return 0;
        }

        return 5;
    }

    private static readonly string[] disclosingHeaders = ["Server", "X-Powered-By", "X-AspNet-Version", "X-AspNetMvc-Version"];

    /// <summary>A header that names the server's software and its version, such as <c>nginx/1.25.3</c>; a bare name says little.</summary>
    private static void CheckServerDisclosure(ScanResponse home, ProjectMonitoring project, Finding add)
    {
        var disclosed = disclosingHeaders
            .Select(name => (Name: name, Value: home.Header(name)))
            .Where(header => header.Value is not null && (header.Name is not "Server" || header.Value.Any(char.IsDigit)))
            .Select(header => $"{header.Name}: {header.Value}")
            .ToArray();

        if (disclosed.Length > 0)
        {
            add(ScanFindings.ServerVersionDisclosed, ScanFindingSeverity.Improvement, -5, [string.Join("; ", disclosed)], [project.Address!]);
        }
    }

    /// <summary>A policy's directives by name, each with its sources, lowercased; the first of a repeated directive counts.</summary>
    private static Dictionary<string, string[]> Directives(string policy)
    {
        Dictionary<string, string[]> directives = [];

        foreach (var directive in policy.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = directive.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            directives.TryAdd(parts[0], parts[1..]);
        }

        return directives;
    }

    /// <summary>A meta tag's content, found the way a browser reads the head; the SEO scan parses whole pages instead.</summary>
    private static string? MetaContent(string? html, string attribute, string value)
    {
        if (html is null)
            return null;

        var document = new AngleSharp.Html.Parser.HtmlParser().ParseDocument(html);

        return document.QuerySelectorAll("meta")
                       .FirstOrDefault(meta => string.Equals(meta.GetAttribute(attribute), value, StringComparison.OrdinalIgnoreCase))
                       ?.GetAttribute("content");
    }

    private sealed record Cookie(string Name, bool Secure, bool HttpOnly, string? SameSite, bool IsDeletion)
    {
        public bool LooksLikeASession => SessionName().IsMatch(Name);

        public static Cookie? Parse(string header)
        {
            var parts = header.Split(';', StringSplitOptions.TrimEntries);
            var nameValue = parts[0].Split('=', 2);

            if (nameValue[0].Length is 0)
                return null;

            var attributes = parts[1..].Select(part => part.Split('=', 2, StringSplitOptions.TrimEntries)).ToArray();
            string? Attribute(string name) => attributes.FirstOrDefault(attribute => attribute[0].Equals(name, StringComparison.OrdinalIgnoreCase)) is { } found ? (found.Length is 2 ? found[1] : "") : null;

            // A cookie set to expire right away is the app deleting it, not one it keeps.
            var isDeletion = (Attribute("Max-Age") is { } maxAge && int.TryParse(maxAge, out var seconds) && seconds <= 0)
                             || (Attribute("Expires") is { } expires && DateTimeOffset.TryParse(expires, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at) && at < DateTimeOffset.UtcNow);

            return new(nameValue[0], Attribute("Secure") is not null, Attribute("HttpOnly") is not null, Attribute("SameSite"), isDeletion);
        }
    }

    [GeneratedRegex("session|sess|auth|token|identity|login|sid|antiforgery|xsrf|csrf", RegexOptions.IgnoreCase)]
    private static partial Regex SessionName();
}
