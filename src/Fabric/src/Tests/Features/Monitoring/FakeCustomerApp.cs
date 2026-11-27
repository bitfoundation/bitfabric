using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;
using Bit.Fabric.Shared.Features.Diagnostic;
using Microsoft.AspNetCore.WebUtilities;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// A customer's app made from the template, as bit Fabric meets it over the network: its <c>/.well-known/bit-app.json</c>
/// and manifest, its home page, the RFC 9728 and RFC 8414 metadata, the token endpoint and <c>/healthz/v1</c>; and what the
/// scanners read: the home page's headers and cookies, its http:// address, its pages, robots.txt and sitemap,
/// the template's endpoints that must ask for sign-in, its certificates and its domain's registration. Its Handler sits in
/// the monitoring http client's pipeline and answers every request itself, so a test drives each case a real app can be in
/// without one running; CertificateStandIn and RegistrationStandIn stand in for the TLS handshake and RDAP the same way.
/// </summary>
public sealed class FakeCustomerApp
{
    /// <summary>One per handler pipeline the http client factory builds, all answering from the same fake app.</summary>
    public sealed class Handler(FakeCustomerApp app) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => app.Answer(request, cancellationToken);
    }

    /// <summary>The app's own address.</summary>
    public string Address { get; set; } = "https://customer.example";

    /// <summary>Where it serves its health report; the address itself unless the test moves it to an api.</summary>
    public string Server { get; set; } = "https://customer.example";

    /// <summary>Its front end says where its server is, as every front end of a template app does.</summary>
    public bool ServesBitApp { get; set; } = true;

    /// <summary>Its pages are hosted statically, so its front end says <c>static</c>.</summary>
    public bool HasStaticFrontEnd { get; set; }

    /// <summary>The name in its web app manifest.</summary>
    public string AppName { get; set; } = "Customer app";

    public bool Answers { get; set; } = true;

    public bool ServesMetadata { get; set; } = true;

    public string? TemplateVersion { get; set; } = "11.0.0";

    /// <summary>The home page comes from a cache that ignores the query string.</summary>
    public bool CachesIgnoringQueryString { get; set; }

    /// <summary>The page its root sends a visitor to, as an app made from the template sends <c>/</c> to the culture's.</summary>
    public string Landing { get; set; } = "/en-US/";

    /// <summary>A cache in front of it, such as Cloudflare's, answers its home page unless the query string is new to it.</summary>
    public bool CachesHomePage { get; set; }

    /// <summary>Every home page it was asked for, with its query string, in order.</summary>
    public List<Uri> HomeRequested { get; } = [];

    public bool RefusesRefresh { get; set; }

    /// <summary>
    /// What its token endpoint answers a refresh with instead of a token, as a server failing, or a proxy in front of it
    /// during a deployment, does: that status with a page that isn't JSON. Null answers as usual.
    /// </summary>
    public HttpStatusCode? RefreshFailsWith { get; set; }

    /// <summary>Its server serves the detailed report at <c>/healthz/v1</c>; false answers 404, as when its code was removed.</summary>
    public bool ServesHealthz { get; set; } = true;

    /// <summary>How long it takes to answer anything, as a slow app does.</summary>
    public TimeSpan Delay { get; set; }

    /// <summary>The PKCE challenge the approval was asked with, which the code's exchange must prove it knows.</summary>
    public string? Challenge { get; set; }

    public string? AccessToken { get; private set; }

    public int RefreshCount { get; private set; }

    /// <summary>Every address it was asked for, in order, to tell one attempt from a retried one; the scans' are apart.</summary>
    public List<string> Requested { get; } = [];

    /// <summary>The addresses the scanners asked for, which say who they are (See ScanHttp).</summary>
    public List<string> ScanRequested { get; } = [];

    /// <summary>The web app manifest's <c>icons</c>, none by default.</summary>
    public List<Dictionary<string, object?>> ManifestIcons { get; } = [];

    /// <summary>Files the app serves by path, such as its icons, with their content type.</summary>
    public Dictionary<string, (byte[] Bytes, string ContentType)> Files { get; } = [];

    /// <summary>The home page as its server renders it.</summary>
    public string HomeHtml { get; set; } = "<html><head><title>Home</title></head><body></body></html>";

    /// <summary>The headers the home page answers with, such as its Content-Security-Policy.</summary>
    public Dictionary<string, string> HomeHeaders { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The Set-Cookie headers of the home page.</summary>
    public List<string> HomeCookies { get; } = [];

    /// <summary>Its http:// address answers with a redirect to https://.</summary>
    public bool RedirectsHttpToHttps { get; set; }

    /// <summary>Its http:// address serves the app as it is, unencrypted.</summary>
    public bool ServesPlainHttp { get; set; }

    /// <summary>The template's endpoints, such as <c>/hangfire</c>, its server answers someone who didn't sign in.</summary>
    public HashSet<string> OpenPaths { get; } = [];

    /// <summary>The other pages of the app, by path.</summary>
    public Dictionary<string, string> Pages { get; } = [];

    public string? RobotsTxt { get; set; }

    /// <summary>Served at /sitemap.xml.</summary>
    public string? Sitemap { get; set; }

    /// <summary>Served at /sitemap_index.xml, as a template app's robots.txt names it in Production.</summary>
    public string? SitemapIndex { get; set; }

    /// <summary>What a TLS handshake shows for each host; a host missing here doesn't complete one.</summary>
    public Dictionary<string, CertificateReport> Certificates { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The domain registrations RDAP knows, by host; a host missing here is under a TLD without RDAP.</summary>
    public Dictionary<string, DomainRegistration> Registrations { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Stands in for the TLS handshake (See TlsCertificateInspector).</summary>
    public sealed class CertificateStandIn(FakeCustomerApp app) : ICertificateInspector
    {
        public Task<CertificateReport?> Inspect(string host, int port, CancellationToken cancellationToken)
            => Task.FromResult(app.Certificates.TryGetValue(host, out var report) ? report : null);
    }

    /// <summary>Stands in for RDAP (See RdapDomainRegistrationLookup).</summary>
    public sealed class RegistrationStandIn(FakeCustomerApp app) : IDomainRegistrationLookup
    {
        public Task<DomainRegistration?> Find(string host, CancellationToken cancellationToken)
            => Task.FromResult(app.Registrations.TryGetValue(host, out var registration) ? registration : null);
    }

    /// <summary>
    /// Everything the scanners look for, done right: HTTPS enforced, the headers a policy needs, cookies kept to https and
    /// their site, the template's endpoints closed, pages a search engine reads in full, a sitemap robots.txt names, and a
    /// certificate and a domain far from their end.
    /// </summary>
    public FakeCustomerApp DoneRight()
    {
        var host = new Uri(Address).Host;

        RedirectsHttpToHttps = true;
        HomeHeaders["Strict-Transport-Security"] = "max-age=31536000; includeSubDomains";
        HomeHeaders["Content-Security-Policy"] = "default-src 'self'; script-src 'self' 'wasm-unsafe-eval'; frame-ancestors 'self'";
        HomeHeaders["X-Content-Type-Options"] = "nosniff";
        HomeHeaders["Referrer-Policy"] = "strict-origin-when-cross-origin";
        HomeHeaders["Server"] = "Kestrel";
        HomeCookies.Add(".AspNetCore.Culture=c%3Den-US%7Cuic%3Den-US; path=/; secure; samesite=lax");
        HomeCookies.Add("bit_session=abc; path=/; secure; samesite=strict; httponly");

        HomeHtml = Page("Bluebird: book a table in seconds", "Book a table at the best restaurants near you in seconds, and get a reminder before you go.", "/en-US/");
        Pages["/en-US/about"] = Page("About Bluebird and the people behind it", "Who builds Bluebird, why we started it, and how to reach the team behind the app.", "/en-US/about");
        // The same page in another culture of the same language shares its title and description, which isn't a duplicate.
        Pages["/en-GB/about"] = Page("About Bluebird and the people behind it", "Who builds Bluebird, why we started it, and how to reach the team behind the app.", "/en-GB/about");
        RobotsTxt = $"User-agent: *\nDisallow:\n\nSitemap: {Address}/sitemap_index.xml\n";
        SitemapIndex = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><sitemapindex xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><sitemap><loc>{Address}/sitemap.xml</loc></sitemap></sitemapindex>";
        Sitemap = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"><url><loc>{Address}/en-US/</loc></url><url><loc>{Address}/en-US/about</loc></url><url><loc>{Address}/en-GB/about</loc></url></urlset>";

        var now = DateTimeOffset.UtcNow;
        Certificates[host] = new(now.AddDays(60), NameMatches: true, ChainTrusted: true);
        Certificates[new Uri(Server).Host] = new(now.AddDays(60), NameMatches: true, ChainTrusted: true);
        Registrations[host] = new(host, now.AddDays(300));

        return this;
    }

    /// <summary>A page a search engine reads in full: its title, description, language, canonical and other cultures, a preview, and text.</summary>
    public string Page(string title, string description, string path) =>
        $"""
        <!DOCTYPE html>
        <html lang="en-US">
        <head>
            <title>{title}</title>
            <meta name="description" content="{description}">
            <link rel="canonical" href="{Address}{path}">
            <link rel="alternate" hreflang="en-US" href="{Address}{path}">
            <link rel="alternate" hreflang="fa-IR" href="{Address}{path.Replace("/en-US/", "/fa-IR/", StringComparison.Ordinal)}">
            <meta property="og:title" content="{title}">
            <meta property="og:image" content="{Address}/images/og.png">
        </head>
        <body>
            <header><h1>{title}</h1></header>
            <main>
                <p>{description} Pick a restaurant, a time and how many of you are coming, and the table is yours: no calls, no waiting on hold, and a reminder an hour before.</p>
                <img src="/images/table.png" alt="A table set for two by a window">
            </main>
            <script src="_framework/blazor.web.js"></script>
        </body>
        </html>
        """;

    /// <summary>The customer's chat, whose incoming webhook bit Fabric's alerts post to.</summary>
    public const string WebhookOrigin = "https://hooks.chat.example";

    /// <summary>The bodies posted to <see cref="WebhookOrigin"/>.</summary>
    public List<string> WebhookBodies { get; } = [];

    public HealthReportDto Report { get; } = new()
    {
        // The not configured check keeps the report itself Degraded, as the template's does.
        Status = HealthCheckStatus.Degraded,
        ApplicationVersion = "1.0.0+aaaa111",
        InstanceName = "web-1",
        StartedAt = DateTimeOffset.UtcNow.AddDays(-1),
        Entries =
        {
            ["AppDbContext"] = new() { Status = HealthCheckStatus.Healthy, FailureStatus = HealthCheckStatus.Unhealthy, Tags = ["live", "category:Platform"], Category = "Platform", Title = "Database", IconName = "Database", About = "Opens a connection to the app's database." },
            // A cached check: its result was measured minutes before the report, which the report says (See HealthReport).
            ["smtp"] = new() { Status = HealthCheckStatus.Healthy, FailureStatus = HealthCheckStatus.Degraded, Timeout = TimeSpan.FromSeconds(5), Duration = TimeSpan.FromMilliseconds(850), CacheDuration = TimeSpan.FromMinutes(5), Tags = ["category:Messaging"], Category = "Messaging", Title = "Email", IconName = "Mail", Provider = "SMTP", Host = "smtp.example.com" },
            // The app's own check, described the way the template tells its users to describe one.
            ["paymentGateway"] = new() { Status = HealthCheckStatus.Healthy, FailureStatus = HealthCheckStatus.Degraded, Duration = TimeSpan.FromMilliseconds(120), Tags = ["category:Integrations"], Category = "Integrations", Title = "Payment gateway", IconName = "Money", About = "Authorizes and voids a one cent payment." },
            // A dependency whose code the app ships without its settings: Degraded, but a setup finding rather than an outage.
            ["googleSignIn"] = new() { Status = HealthCheckStatus.Degraded, FailureStatus = HealthCheckStatus.Degraded, Description = "Authentication:Google is not configured. Configure it, or remove the code that uses it.", Tags = ["category:Identity", "notConfigured"], Category = "Identity", Title = "Sign in with Google", IconName = "Signin", IsNotConfigured = true }
        }
    };

    public void Fail(string check, string description)
    {
        Report.Entries[check].Status = Report.Entries[check].FailureStatus;
        Report.Entries[check].Description = description;
        Report.Status = Report.Entries.Values.Min(entry => entry.Status);
    }

    public void Recover(string check)
    {
        Report.Entries[check].Status = HealthCheckStatus.Healthy;
        Report.Entries[check].Description = null;
        Report.Status = Report.Entries.Values.Min(entry => entry.Status);
    }

    /// <summary>The access token stops working, so the next round refreshes it.</summary>
    public void ExpireAccessToken() => AccessToken = "expired";

    /// <summary>Takes back the approval, the way a password change does: the token stops working and so does its refresh.</summary>
    public void Revoke()
    {
        AccessToken = "revoked";
        RefusesRefresh = true;
    }

    public async Task<HttpResponseMessage> Answer(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!;
        var origin = url.GetLeftPart(UriPartial.Authority);
        var isScan = request.Headers.UserAgent.ToString().Contains("bitFabric-Scanner", StringComparison.Ordinal);
        lock (Requested) (isScan ? ScanRequested : Requested).Add(url.GetLeftPart(UriPartial.Path));

        if (Delay > TimeSpan.Zero)
        {
            await Task.Delay(Delay, cancellationToken);
        }

        if (origin == WebhookOrigin && request.Method == HttpMethod.Post)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            lock (WebhookBodies) WebhookBodies.Add(body);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }

        // The app's http:// address: a redirect, the app itself, or no answer at all, as a server with port 80 closed gives.
        if (Answers && origin == $"http://{new Uri(Address).Authority}" && (RedirectsHttpToHttps || ServesPlainHttp))
            return RedirectsHttpToHttps
                ? new HttpResponseMessage(HttpStatusCode.MovedPermanently) { Headers = { Location = new Uri($"{Address}{url.PathAndQuery}") } }
                : Home();

        if (Answers is false || (origin != Address && origin != Server))
            throw new HttpRequestException(HttpRequestError.ConnectionError, $"{origin} didn't answer.");

        if (request.Method == HttpMethod.Get && origin == Server && OpenPaths.Contains(url.AbsolutePath))
            return OpenEndpoint(url.AbsolutePath);

        return (request.Method.Method, url.AbsolutePath) switch
        {
            ("GET", "/.well-known/bit-app.json") when origin == Address && ServesBitApp => Json(new Dictionary<string, object?>
            {
                ["server"] = $"{Server}/",
                ["front_end"] = HasStaticFrontEnd ? "static" : "server",
                ["manifest"] = "/manifest.json"
            }),
            ("GET", "/manifest.json") when origin == Address => Json(new Dictionary<string, object?> { ["name"] = AppName, ["short_name"] = "Customer", ["icons"] = ManifestIcons }),
            ("GET", var path) when origin == Address && Files.TryGetValue(path, out var file) => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(file.Bytes) { Headers = { ContentType = new(file.ContentType) } }
            },
            ("GET", "/.well-known/oauth-protected-resource/healthz") when origin == Server && ServesMetadata => Json(new Dictionary<string, object?>
            {
                ["resource"] = $"{Server}/healthz",
                ["authorization_servers"] = new[] { Server },
                ["scopes_supported"] = new[] { "healthz" },
                ["bit_template_version"] = TemplateVersion
            }),
            ("GET", "/.well-known/oauth-authorization-server") when origin == Server => Json(new Dictionary<string, object?>
            {
                ["issuer"] = Server,
                ["authorization_endpoint"] = $"{Server}/oauth/authorize",
                ["token_endpoint"] = $"{Server}/oauth/token",
                ["code_challenge_methods_supported"] = new[] { "S256" }
            }),
            ("POST", "/oauth/token") when origin == Server => await Token(request, cancellationToken),
            ("GET", "/healthz/v1") when origin == Server && ServesHealthz is false => new HttpResponseMessage(HttpStatusCode.NotFound),
            ("GET", "/healthz/v1") when origin == Server => request.Headers.Authorization?.Parameter is { } token && token == AccessToken
                ? HealthReport()
                : new HttpResponseMessage(HttpStatusCode.Unauthorized),
            // The public /health, as the template answers it: the overall status alone, 503 when Unhealthy.
            ("GET", "/health") when origin == Server => new HttpResponseMessage(Report.Status is HealthCheckStatus.Unhealthy ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
            {
                Content = new StringContent($"{Report.Status}", Encoding.UTF8, "text/plain")
            },
            // An app made from the template sends its root to the culture's (See ProjectProber.TimeFrontDoor).
            ("GET", "/") => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri($"{Landing}{url.Query}", UriKind.Relative) } },
            ("GET", var path) when path == Landing || path is "/health" => Home(url),
            // One it no longer lands on sends the visitor on, as after the app's default culture changed.
            ("GET", "/en-US/") => new HttpResponseMessage(HttpStatusCode.Found) { Headers = { Location = new Uri($"{Landing}{url.Query}", UriKind.Relative) } },
            ("GET", "/robots.txt") when origin == Address && RobotsTxt is not null => Text(RobotsTxt, "text/plain"),
            ("GET", "/sitemap.xml") when origin == Address && Sitemap is not null => Text(Sitemap, "application/xml"),
            ("GET", "/sitemap_index.xml") when origin == Address && SitemapIndex is not null => Text(SitemapIndex, "application/xml"),
            ("GET", var path) when origin == Address && Pages.TryGetValue(path, out var page) => Text(page, "text/html"),
            _ => new HttpResponseMessage(HttpStatusCode.NotFound)
        };
    }

    private async Task<HttpResponseMessage> Token(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var form = QueryHelpers.ParseQuery(await request.Content!.ReadAsStringAsync(cancellationToken));

        if (form["grant_type"] == "authorization_code")
        {
            // PKCE: the verifier must hash to the challenge the approval was asked with (RFC 7636 §4.6).
            var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(form["code_verifier"].ToString())));
            if (challenge != Challenge || form["resource"] != $"{Server}/healthz")
                return TokenError("invalid_grant");
        }
        else if (RefreshFailsWith is { } status)
        {
            return new HttpResponseMessage(status) { Content = new StringContent("<html><body>Something went wrong</body></html>", Encoding.UTF8, "text/html") };
        }
        else if (RefusesRefresh)
        {
            return TokenError("invalid_grant", "The user's security changed since the approval, such as a new password.");
        }
        else
        {
            RefreshCount++;
        }

        AccessToken = $"access-{Guid.NewGuid():N}";

        return Json(new Dictionary<string, object?>
        {
            ["access_token"] = AccessToken,
            ["refresh_token"] = $"refresh-{Guid.NewGuid():N}",
            ["token_type"] = "Bearer",
            ["expires_in"] = 300,
            ["scope"] = form.TryGetValue("scope", out var scope) && scope.Count > 0 ? scope.ToString() : "healthz"
        });
    }

    /// <summary>The report as of now: the cached check last really ran three minutes ago.</summary>
    private HttpResponseMessage HealthReport()
    {
        Report.CheckedAt = DateTimeOffset.UtcNow;
        Report.Entries["smtp"].VerifiedAt = Report.CheckedAt.AddMinutes(-3);

        return new(Report.Status is HealthCheckStatus.Unhealthy ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Report, AppJsonContext.Default.HealthReportDto)
        };
    }

    private HttpResponseMessage Home(Uri? url = null)
    {
        var response = Text(HomeHtml, "text/html");

        if (url is not null && url.AbsolutePath != "/health")
        {
            lock (HomeRequested) HomeRequested.Add(url);

            if (CachesHomePage)
            {
                response.Headers.TryAddWithoutValidation("cf-cache-status", QueryHelpers.ParseQuery(url.Query).ContainsKey("bf") ? "MISS" : "HIT");
            }
        }

        foreach (var (name, value) in HomeHeaders)
        {
            response.Headers.TryAddWithoutValidation(name, value);
        }

        foreach (var cookie in HomeCookies)
        {
            response.Headers.TryAddWithoutValidation("Set-Cookie", cookie);
        }

        if (CachesIgnoringQueryString)
        {
            response.Headers.Age = TimeSpan.FromSeconds(42);
        }

        return response;
    }

    /// <summary>One of the template's endpoints, answering as it does when its authorization is gone.</summary>
    private HttpResponseMessage OpenEndpoint(string path) => path switch
    {
        "/healthz" => new(HttpStatusCode.OK) { Content = JsonContent.Create(Report, AppJsonContext.Default.HealthReportDto) },
        "/hangfire" => Text("<html><head><title>Overview - Hangfire Dashboard</title></head><body>Hangfire Dashboard</body></html>", "text/html"),
        "/dev-mcp" => new(HttpStatusCode.NotAcceptable),
        "/scalar" => Text("<html><head><title>Scalar API Reference</title></head><body><script src=\"scalar.js\"></script></body></html>", "text/html"),
        "/openapi/v1.json" => Text("{\"openapi\":\"3.1.1\",\"info\":{\"title\":\"Api\"}}", "application/json"),
        "/odata/$metadata" => Text("<?xml version=\"1.0\"?><edmx:Edmx Version=\"4.0\" xmlns:edmx=\"http://docs.oasis-open.org/odata/ns/edmx\"></edmx:Edmx>", "application/xml"),
        _ => new(HttpStatusCode.NotFound)
    };

    private static HttpResponseMessage Text(string body, string mediaType) => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, mediaType) };

    private static HttpResponseMessage TokenError(string error, string? description = null)
        => Json(new Dictionary<string, object?> { ["error"] = error, ["error_description"] = description }, HttpStatusCode.BadRequest);

    private static HttpResponseMessage Json(Dictionary<string, object?> body, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
}
