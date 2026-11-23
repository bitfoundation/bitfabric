using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

public static class MonitoringHttp
{
    /// <summary>The one http client every request to a customer's app goes through (See PublicAddressGuard).</summary>
    public const string ClientName = "Monitoring";

    public static IServiceCollection AddMonitoring(this IServiceCollection services)
    {
        services.AddScoped<ProjectAddressProbe>();
        services.AddScoped<ProjectIcons>();
        services.AddScoped<MonitoringOAuthClient>();
        services.AddScoped<ProjectProber>();
        services.AddScoped<ProjectTexts>();
        services.AddScoped<ProjectIncidentTracker>();
        services.AddScoped<ProjectAlerter>();
        services.AddScoped<StatusPageService>();
        services.AddScoped<CloudflareCustomHostnames>();
        services.AddScoped<ProjectProbeJobRunner>();
        services.AddScoped<ProjectProbesRetentionJobRunner>();
        services.AddScoped<ProjectEscalationJobRunner>();
        services.AddScoped<ProjectSlaReports>();
        services.AddScoped<ProjectsService>();
        services.AddScoped<ProjectIncidentsService>();
        services.AddScoped<ProjectHistoryExport>();

        // The scanners, each read through the guarded client below; the TLS handshake and RDAP are behind
        // interfaces the tests replace.
        services.AddScoped<ScanHttp>();
        services.AddScoped<SitemapReader>();
        services.AddScoped<IProjectScanner, SecurityScanner>();
        services.AddScoped<IProjectScanner, SeoScanner>();
        services.AddScoped<IProjectScanner, DomainAndCertificateScanner>();
        // The browser scans, run by bit Fabric's Lighthouse service when Monitoring:LighthouseUrl names it.
        services.AddScoped<LighthouseClient>();
        services.AddScoped<IProjectScanner, PerformanceScanner>();
        services.AddScoped<IProjectScanner, AccessibilityScanner>();
        services.AddScoped<ICertificateInspector, TlsCertificateInspector>();
        services.AddScoped<IDomainRegistrationLookup, RdapDomainRegistrationLookup>();
        services.AddScoped<ProjectScanScheduler>();
        services.AddScoped<ProjectScanReader>();
        services.AddScoped<ProjectScanJobRunner>();
        services.AddScoped<ProjectScanWeeklyJobRunner>();
        services.AddScoped<ProjectScansEmailJobRunner>();
        services.AddScoped<ProjectScansMcpTools>();

        // RDAP's servers are on the public internet, never a customer's address, so they get a client of their own that
        // follows a registry's redirect; its connections still go through the guard.
        services.AddHttpClient(RdapDomainRegistrationLookup.ClientName, (serviceProvider, httpClient) =>
        {
            httpClient.Timeout = serviceProvider.GetRequiredService<ServerApiSettings>().Monitoring.ScanRequestTimeout;
            httpClient.MaxResponseContentBufferSize = 1024 * 1024;
            httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("bitFabric-Monitoring/1.0 (+https://bitfabric.co)");
        })
        .UseSocketsHttpHandler((handler, _) =>
        {
            handler.MaxAutomaticRedirections = 3;
            handler.ConnectCallback = (context, cancellationToken) => PublicAddressGuard.Connect(context, allowLocalAddresses: false, cancellationToken);
        });

#pragma warning disable EXTEXP0001 // RemoveAllResilienceHandlers is still marked experimental.
        // bit Fabric's own Lighthouse service on a network of its own, never a customer's address: no public-address guard,
        // and no retries or attempt timeouts, since a page's run takes a minute and running it twice costs a Chrome.
        services.AddHttpClient(LighthouseClient.ClientName, (serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<ServerApiSettings>();
            if (string.IsNullOrWhiteSpace(settings.Monitoring.LighthouseUrl) is false)
            {
                httpClient.BaseAddress = new Uri($"{settings.Monitoring.LighthouseUrl.TrimEnd('/')}/");
            }
            httpClient.Timeout = settings.Monitoring.LighthouseRequestTimeout;
            httpClient.MaxResponseContentBufferSize = 4 * 1024 * 1024;
        })
        .RemoveAllResilienceHandlers();

        services.AddHttpClient(ClientName, (serviceProvider, httpClient) =>
        {
            var settings = serviceProvider.GetRequiredService<ServerApiSettings>();
            httpClient.Timeout = settings.Monitoring.RequestTimeout;
            // A report is a few KB; anything far bigger isn't one.
            httpClient.MaxResponseContentBufferSize = 1024 * 1024;
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("bitFabric-Monitoring/1.0 (+https://bitfabric.co)");
        })
        // Configuring the shared handler rather than replacing it keeps ConfigureHttpClientDefaults' TLS floor here.
        .UseSocketsHttpHandler((handler, serviceProvider) =>
        {
            var allowLocalAddresses = serviceProvider.GetRequiredService<ServerApiSettings>().Monitoring.AllowLocalAddresses;

            // A redirect would arrive somewhere the guard never saw; the app's own address must answer.
            handler.AllowAutoRedirect = false;
            handler.ConnectCallback = (context, cancellationToken) => PublicAddressGuard.Connect(context, allowLocalAddresses, cancellationToken);
            // Each round is a fresh look, not a reused connection's: DNS and TLS count, as for a visitor.
            handler.PooledConnectionLifetime = TimeSpan.FromMinutes(2);
        })
        // No retries and no circuit breaker: a round measures one attempt, as a visitor makes it, and one client name
        // serves every customer's app, so a breaker opened by one app that doesn't answer would fail all the others.
        .RemoveAllResilienceHandlers();
#pragma warning restore EXTEXP0001

        return services;
    }

    /// <summary>
    /// A GET that follows redirects within the same origin, as a visitor's browser would: an app made from the template
    /// sends <c>/</c> to <c>/en-US/</c>. One to another origin isn't followed, since the guard approved only this one,
    /// and the response that asks for it is returned instead.
    /// </summary>
    public static async Task<HttpResponseMessage> GetFollowingSameOriginRedirects(this HttpClient http, string url, CancellationToken cancellationToken, int maxRedirects = 3)
    {
        var uri = new Uri(url);

        for (var redirects = 0; ; redirects++)
        {
            var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if ((int)response.StatusCode is < 300 or >= 400 || response.Headers.Location is null || redirects == maxRedirects)
                return response;

            var next = new Uri(uri, response.Headers.Location);

            if (string.Equals(next.GetLeftPart(UriPartial.Authority), uri.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase) is false)
                return response;

            response.Dispose();
            uri = next;
        }
    }
}
