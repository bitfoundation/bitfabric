using System.Net;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Finds where a customer's app serves its health report from what the app tells anyone
/// (bitfabric-internal/specs/monitoring.md, The app says where its server is): every front end of a template app names
/// its server and front end at <see cref="BitAppPath"/>, the web app manifest it points to names the app, and the
/// server's RFC 9728 metadata names its <c>/healthz</c> resource. Nothing is guessed: an address without the document is
/// not a bit app.
/// </summary>
public partial class ProjectAddressProbe(IHttpClientFactory httpClientFactory, ServerApiSettings settings, ILogger<ProjectAddressProbe> logger)
{
    public const string HealthzPath = "/healthz";

    public const string ResourceMetadataPath = "/.well-known/oauth-protected-resource/healthz";

    public const string BitAppPath = "/.well-known/bit-app.json";

    /// <summary>
    /// <c>https://host[:port]</c> from what the customer typed, with no path, query or credentials; null when it isn't an
    /// address. Plain http is taken as https, except on local addresses where <see cref="MonitoringOptions.AllowLocalAddresses"/> lets it stay.
    /// </summary>
    public string? Normalize(string? typed)
    {
        typed = typed?.Trim();
        if (string.IsNullOrEmpty(typed))
            return null;

        if (typed.Contains("://", StringComparison.Ordinal) is false)
        {
            typed = $"https://{typed}";
        }

        if (Uri.TryCreate(typed, UriKind.Absolute, out var uri) is false || uri.Scheme is not ("https" or "http")
            || string.IsNullOrEmpty(uri.UserInfo) is false || uri.HostNameType is UriHostNameType.Unknown)
            return null;

        var keepsHttp = uri.Scheme is "http" && settings.Monitoring.AllowLocalAddresses;
        var scheme = keepsHttp ? "http" : "https";
        var port = uri.IsDefaultPort || (uri.Scheme is "http" && keepsHttp is false) ? "" : $":{uri.Port}";

        return $"{scheme}://{uri.IdnHost.ToLowerInvariant()}{port}";
    }

    public async Task<AddressProbeResultDto> Probe(string? typedAddress, CancellationToken cancellationToken)
    {
        var address = Normalize(typedAddress);
        if (address is null)
            return new() { Outcome = AddressProbeOutcome.Unreachable };

        var (answered, document) = await Read(address, BitAppPath, MonitoringJsonContext.Default.BitAppDocument, cancellationToken);

        // An address that answers without the document is another kind of site, or a bit app made before the template had it.
        if (document is null || Normalize(document.Server) is not { } server)
            return new() { Outcome = answered ? AddressProbeOutcome.NotFound : AddressProbeOutcome.Unreachable, Address = address };

        if (await ReadResourceMetadata(server, cancellationToken) is not { } metadata)
            return new() { Outcome = AddressProbeOutcome.NotFound, Address = address };

        var (manifest, _) = await ReadManifest(address, document, cancellationToken);

        return new()
        {
            Outcome = AddressProbeOutcome.Found,
            Address = address,
            ServerAddress = server,
            HasStaticFrontEnd = document.FrontEnd is "static",
            TemplateVersion = metadata.BitTemplateVersion,
            AppTitle = (manifest?.Name ?? manifest?.ShortName)?.Trim() is { Length: > 0 and <= 100 } name ? name : null
        };
    }

    /// <summary>
    /// The web app manifest the app's document points to, on the app's own origin, and the address it was read at, which
    /// its icons' addresses are relative to; nulls when the app answers neither.
    /// </summary>
    public async Task<(WebAppManifest? Manifest, Uri? Url)> ReadManifest(string address, CancellationToken cancellationToken)
    {
        var (_, document) = await Read(address, BitAppPath, MonitoringJsonContext.Default.BitAppDocument, cancellationToken);

        return document is null ? (null, null) : await ReadManifest(address, document, cancellationToken);
    }

    private async Task<(WebAppManifest? Manifest, Uri? Url)> ReadManifest(string address, BitAppDocument document, CancellationToken cancellationToken)
    {
        if (Uri.TryCreate(new Uri(address), document.Manifest ?? "/manifest.json", out var manifestUrl) is false
            || Normalize(manifestUrl.GetLeftPart(UriPartial.Authority)) != address)
            return (null, null);

        var manifest = (await Read(manifestUrl.ToString(), "", MonitoringJsonContext.Default.WebAppManifest, cancellationToken)).Value;

        return manifest is null ? (null, null) : (manifest, manifestUrl);
    }

    /// <summary>The metadata of the server's <c>/healthz</c>, when it names an authorization server and that resource.</summary>
    public async Task<ProtectedResourceMetadata?> ReadResourceMetadata(string server, CancellationToken cancellationToken)
    {
        var (_, metadata) = await Read(server, ResourceMetadataPath, MonitoringJsonContext.Default.ProtectedResourceMetadata, cancellationToken);

        return metadata?.AuthorizationServers is { Length: > 0 }
               && metadata.Resource?.TrimEnd('/').EndsWith(HealthzPath, StringComparison.OrdinalIgnoreCase) is true
            ? metadata
            : null;
    }

    /// <summary>A JSON document at <paramref name="path"/> of <paramref name="origin"/>, and whether anything answered there at all.</summary>
    private async Task<(bool Answered, T? Value)> Read<T>(string origin, string path, System.Text.Json.Serialization.Metadata.JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
        where T : class
    {
        var answered = false;

        try
        {
            var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
            using var response = await http.GetFollowingSameOriginRedirects($"{origin}{path}", cancellationToken);
            answered = true;

            if (response.StatusCode is not HttpStatusCode.OK)
                return (true, null);

            return (true, await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken));
        }
        catch (Exception exp) when (exp is HttpRequestException or TaskCanceledException or JsonException or NotSupportedException)
        {
            logger.LogInformation("Nothing to read at {Origin}{Path}: {Reason}", origin, path, exp.Message);
            return (answered, null);
        }
    }
}
