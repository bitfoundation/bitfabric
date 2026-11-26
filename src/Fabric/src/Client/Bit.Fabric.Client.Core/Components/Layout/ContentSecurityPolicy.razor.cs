using Bit.Belemetry;

namespace Bit.Fabric.Client.Core.Components.Layout;

public partial class ContentSecurityPolicy
{
    [AutoInject] private ClientCoreSettings settings = default!;
    [AutoInject] private AbsoluteServerAddressProvider absoluteServerAddressProvider = default!;
    [AutoInject] private IConfiguration configuration = default!;

    protected string? CspContent { get; set; }

    protected override void OnInitialized()
    {
        CspContent = BuildCspString(settings, absoluteServerAddressProvider, configuration);
    }

    public static string BuildCspString(ClientCoreSettings settings, AbsoluteServerAddressProvider absoluteServerAddressProvider, IConfiguration configuration)
    {
        var apiUrl = absoluteServerAddressProvider.GetAddress().ToString();
        var webAppUrl = settings.WebAppUrl?.ToString();

        // 1. Common Trusted Origins (Our own servers)
        var ownOrigins = new HashSet<string> { "'self'", apiUrl };
        if (string.IsNullOrWhiteSpace(webAppUrl) is false)
            ownOrigins.Add(webAppUrl);

        // 2. Service Specific Origins
        var connectSrc = new HashSet<string>(ownOrigins) { "blob:" };
        var imgSrc = new HashSet<string>(ownOrigins) { "data:", "blob:" };
        var scriptSrc = new HashSet<string>(ownOrigins) { "'unsafe-inline'", "'wasm-unsafe-eval'", "'unsafe-hashes'" };
        var styleSrc = new HashSet<string>(ownOrigins) { "'unsafe-inline'" };
        var fontSrc = new HashSet<string>(ownOrigins) { "data:" };
        var frameSrc = new HashSet<string>(ownOrigins);
        var mediaSrc = new HashSet<string>(ownOrigins);



        // --- Add the OTLP collector Belemetry's browser script posts to: Belemetry:OtlpEndpoint, else the server's own ---
        if (configuration.GetBelemetryOtlpEndpoint() is { } browserOtlpEndpoint)
        {
            connectSrc.Add($"{browserOtlpEndpoint.Scheme}://{browserOtlpEndpoint.Authority}");
        }
        else if (configuration["OTEL_EXPORTER_OTLP_PROTOCOL"]?.Trim().ToLowerInvariant() is not "grpc"
            && Uri.TryCreate(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"], UriKind.Absolute, out var otlpEndpoint)
            && otlpEndpoint.Scheme is "http" or "https")
        {
            connectSrc.Add($"{otlpEndpoint.Scheme}://{otlpEndpoint.Authority}");
        }

        // --- Add the status page StatusPageNotice reads (See ClientCoreSettings.StatusPageSummaryUrl) ---
        if (Uri.TryCreate(settings.StatusPageSummaryUrl, UriKind.Absolute, out var statusPageSummary) && statusPageSummary.Scheme is "https" or "http")
        {
            connectSrc.Add($"{statusPageSummary.Scheme}://{statusPageSummary.Authority}");
        }



        connectSrc.Add("https://*.service.signalr.net");
        connectSrc.Add(apiUrl.Replace("http:", "ws:").Replace("https:", "wss:"));
        mediaSrc.Add("blob:");

        // --- Add for home page's github stats ---
        imgSrc.Add("https://img.shields.io");
        connectSrc.Add("https://api.github.com");

        // Inert today - the component only renders the meta tag when the environment is NOT Development - and kept
        // as the record of what a Development policy would have to allow, for whoever relaxes that guard.
        if (AppEnvironment.IsDevelopment())
        {
            connectSrc.Add("ws://localhost:* wss://localhost:*"); // Allow localhost WebSocket connections during development (hot reload / debugging)
            connectSrc.Add("https://raw.githubusercontent.com/"); // Source maps
        }

        // Construct the final CSP string
        var policy = $"default-src {string.Join(" ", ownOrigins)}; " + // Fallback for all directives.
                     $"script-src {string.Join(" ", scriptSrc)}; " +   // Defines valid sources for executable JavaScript.
                     $"style-src {string.Join(" ", styleSrc)}; " +     // Specifies trusted sources for CSS stylesheets.
                     $"font-src {string.Join(" ", fontSrc)}; " +       // Controls where web fonts (e.g., Google Fonts) can be loaded from.
                     $"img-src {string.Join(" ", imgSrc)}; " +         // Defines allowed origins for images.
                     $"connect-src {string.Join(" ", connectSrc)}; " + // Limits targets for API calls (fetch, XHR, WebSockets).
                     $"frame-src {string.Join(" ", frameSrc)}; " +     // Restricts sources for iframes and nested browsing contexts.
                     $"media-src {string.Join(" ", mediaSrc)}; " +     // Controls where video/audio media can be loaded from.
                     $"worker-src 'self'; " +                          // Restricts the sources from which Web Workers can be loaded to the same origin.
                     $"base-uri {string.Join(" ", ownOrigins)}; " +    // Restricts the URLs that can be used in a document's <base> element.
                     $"form-action {string.Join(" ", ownOrigins)}; " + // Ensures form data is only submitted to your own server.
                     $"object-src 'none';";                            // Disallows legacy plugins like Flash.

        if (AppEnvironment.IsDevelopment() is false)
        {
            policy += "upgrade-insecure-requests; ";                   // Tells browsers to treat all of this site's insecure URLs (those over HTTP) as though they have been replaced with secure URLs.
        }

        return policy;
    }
}
