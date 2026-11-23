using System.Security.Cryptography;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.DataProtection;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Shared.Features.Monitoring;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// bit Fabric as an OAuth client of its customers' apps (bitfabric-internal/specs/monitoring.md, Connecting a project).
/// Its client id is the https address of its own client metadata document (CIMD), which every app made from the template
/// accepts without registration, and it asks for the <c>healthz</c> scope on the <c>/healthz</c> resource with PKCE,
/// nothing else. A token never reads the failures' error texts: they stay in the app.
/// </summary>
public partial class MonitoringOAuthClient(IHttpClientFactory httpClientFactory,
    IDataProtectionProvider dataProtectionProvider,
    ServerApiSettings settings,
    TimeProvider timeProvider)
{
    /// <summary>Where the web app's add flow takes the customer app's answer.</summary>
    /// <summary>
    /// Where a customer's app sends the browser back after its approval page: bit Fabric's api, which hands the answer to
    /// the add page that opened that page beside it (See MonitoringClientController.Callback).
    /// </summary>
    public const string CallbackPath = "/api/v1/MonitoringClient/Callback";

    /// <summary>Where this server serves its client metadata document (See MonitoringClientController).</summary>
    public const string MetadataPath = "/api/v1/MonitoringClient/Metadata";

    public const string HealthzScope = "healthz";

    private IDataProtector Protector => field ??= dataProtectionProvider.CreateProtector("Bit.Fabric.Monitoring.Tokens");

    /// <summary>The client id: the configured one, or the address of the metadata document on this server.</summary>
    public string ClientIdFor(string serverBaseUrl) => settings.Monitoring.ClientId ?? $"{serverBaseUrl.TrimEnd('/')}{MetadataPath}";

    public string Protect(string token) => Protector.Protect(token);

    public string Unprotect(string protectedToken) => Protector.Unprotect(protectedToken);

    /// <summary>A PKCE verifier and its S256 challenge (RFC 7636).</summary>
    public static (string Verifier, string Challenge) CreatePkce()
    {
        var verifier = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));
        var challenge = Base64UrlTextEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        return (verifier, challenge);
    }

    /// <summary>The customer app's authorization server metadata (RFC 8414), read from the issuer its resource names.</summary>
    public async Task<AuthorizationServerMetadata?> ReadAuthorizationServer(string issuer, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);
        using var response = await http.GetAsync($"{issuer.TrimEnd('/')}/.well-known/oauth-authorization-server", cancellationToken);

        if (response.IsSuccessStatusCode is false)
            return null;

        var metadata = await response.Content.ReadFromJsonAsync(MonitoringJsonContext.Default.AuthorizationServerMetadata, cancellationToken);

        // RFC 8414 §3.3: the document must name the issuer it was read from, or it may be another server's.
        return metadata is not null && string.Equals(metadata.Issuer?.TrimEnd('/'), issuer.TrimEnd('/'), StringComparison.OrdinalIgnoreCase)
            && Uri.TryCreate(metadata.AuthorizationEndpoint, UriKind.Absolute, out _)
            && Uri.TryCreate(metadata.TokenEndpoint, UriKind.Absolute, out _)
            ? metadata
            : null;
    }

    /// <summary>The token endpoint's answer to a code (authorization_code) or a refresh (refresh_token), or why it refused.</summary>
    public async Task<TokenResult> RequestToken(string tokenEndpoint, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(MonitoringHttp.ClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        var answer = await response.Content.ReadFromJsonAsync(MonitoringJsonContext.Default.TokenResponse, cancellationToken);

        if (response.IsSuccessStatusCode is false || answer?.AccessToken is null)
            return new(null, answer?.Error ?? $"HTTP {(int)response.StatusCode}", answer?.ErrorDescription);

        return new(answer, null, null);
    }

    /// <summary>
    /// Stores a token answer on the connection. A refresh rotates the refresh token, so the new one replaces the old,
    /// and an answer without one keeps the old (RFC 6749 §6).
    /// </summary>
    public void Store(ProjectConnection connection, TokenResponse answer)
    {
        connection.ProtectedAccessToken = Protect(answer.AccessToken!);

        if (string.IsNullOrEmpty(answer.RefreshToken) is false)
        {
            connection.ProtectedRefreshToken = Protect(answer.RefreshToken);
        }

        connection.AccessTokenExpiresAt = timeProvider.GetUtcNow().AddSeconds(Math.Max(60, answer.ExpiresIn ?? 300));

        if (string.IsNullOrEmpty(answer.Scope) is false)
        {
            connection.Scopes = answer.Scope;
        }
    }

    /// <summary>
    /// A fresh access token for the connection: the stored one while it has more than a minute left, else one from its
    /// refresh token. Null when the app refused the refresh, which needs someone to reconnect.
    /// </summary>
    public async Task<(string? AccessToken, string? Problem)> GetAccessToken(ProjectConnection connection, string clientId, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (forceRefresh is false && connection.AccessTokenExpiresAt > timeProvider.GetUtcNow().AddMinutes(1))
            return (Unprotect(connection.ProtectedAccessToken!), null);

        if (connection.ProtectedRefreshToken is null)
            return (null, ProjectConnectionProblems.Of(ProjectConnectionProblems.ApprovalExpired));

        var result = await RequestToken(connection.TokenEndpoint!, new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = Unprotect(connection.ProtectedRefreshToken),
            ["client_id"] = clientId,
            ["resource"] = connection.Resource!
        }, cancellationToken);

        if (result.Answer is null)
            // A code, put in each reader's words when it's read (See ProjectConnectionProblems); the app's own reason as it said it.
            return (null, result.ErrorDescription?.Trim() is { Length: > 0 } reason
                ? ProjectConnectionProblems.Of(ProjectConnectionProblems.NoLongerAccepted, reason.Length > 300 ? reason[..300] : reason)
                : ProjectConnectionProblems.Of(ProjectConnectionProblems.RenewRefused, result.Error));

        Store(connection, result.Answer);
        return (result.Answer.AccessToken, null);
    }
}

public sealed record TokenResult(TokenResponse? Answer, string? Error, string? ErrorDescription);

public sealed class AuthorizationServerMetadata
{
    [JsonPropertyName("issuer")] public string? Issuer { get; set; }

    [JsonPropertyName("authorization_endpoint")] public string? AuthorizationEndpoint { get; set; }

    [JsonPropertyName("token_endpoint")] public string? TokenEndpoint { get; set; }

    [JsonPropertyName("code_challenge_methods_supported")] public string[]? CodeChallengeMethodsSupported { get; set; }
}

public sealed class ProtectedResourceMetadata
{
    [JsonPropertyName("resource")] public string? Resource { get; set; }

    [JsonPropertyName("authorization_servers")] public string[]? AuthorizationServers { get; set; }

    [JsonPropertyName("scopes_supported")] public string[]? ScopesSupported { get; set; }

    /// <summary>The template says it made the app, and which version ("unknown" without it).</summary>
    [JsonPropertyName("bit_template_version")] public string? BitTemplateVersion { get; set; }
}

public sealed class TokenResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }

    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")] public int? ExpiresIn { get; set; }

    [JsonPropertyName("scope")] public string? Scope { get; set; }

    [JsonPropertyName("error")] public string? Error { get; set; }

    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}

/// <summary>A client metadata document (CIMD) as bit Fabric serves its own.</summary>
public sealed class MonitoringClientMetadata
{
    [JsonPropertyName("client_id")] public string? ClientId { get; set; }

    [JsonPropertyName("client_name")] public string? ClientName { get; set; }

    [JsonPropertyName("client_uri")] public string? ClientUri { get; set; }

    [JsonPropertyName("redirect_uris")] public string[] RedirectUris { get; set; } = [];

    [JsonPropertyName("grant_types")] public string[] GrantTypes { get; set; } = [];

    [JsonPropertyName("response_types")] public string[] ResponseTypes { get; set; } = [];

    [JsonPropertyName("token_endpoint_auth_method")] public string? TokenEndpointAuthMethod { get; set; }

    [JsonPropertyName("scope")] public string? Scope { get; set; }
}

/// <summary>The state an approval in progress keeps between StartConnect and CompleteConnect, keyed by its <c>state</c>.</summary>
public sealed class PendingConnection
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string? Address { get; set; }
    public string? ServerAddress { get; set; }
    public string? Name { get; set; }
    public string? TemplateVersion { get; set; }
    public bool HasStaticFrontEnd { get; set; }
    public string? Issuer { get; set; }
    public string? TokenEndpoint { get; set; }
    public string? Resource { get; set; }
    public string? Scopes { get; set; }
    public string? ClientId { get; set; }
    public string? RedirectUri { get; set; }
    public string? CodeVerifier { get; set; }

    /// <summary>The native app's local server the answer goes back to, as external sign-in's does (See ILocalHttpServer).</summary>
    public int? LocalHttpPort { get; set; }
    public string? LocalHttpNonce { get; set; }
}

/// <summary>What a template app's front end says at <c>/.well-known/bit-app.json</c> (See ProjectAddressProbe).</summary>
public sealed class BitAppDocument
{
    /// <summary>Where the app's <c>/healthz</c> and its OAuth metadata live.</summary>
    [JsonPropertyName("server")] public string? Server { get; set; }

    /// <summary><c>server</c> when Server.Web renders the pages, <c>static</c> for a statically hosted front end.</summary>
    [JsonPropertyName("front_end")] public string? FrontEnd { get; set; }

    /// <summary>The app's web app manifest, which names it.</summary>
    [JsonPropertyName("manifest")] public string? Manifest { get; set; }
}

/// <summary>The part of a W3C web app manifest bit Fabric reads.</summary>
public sealed class WebAppManifest
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("short_name")] public string? ShortName { get; set; }

    [JsonPropertyName("icons")] public WebAppManifestIcon[]? Icons { get; set; }
}

/// <summary>One of a web app manifest's icons: <c>sizes</c> such as "192x192 512x512" or "any", <c>purpose</c> such as "any maskable".</summary>
public sealed class WebAppManifestIcon
{
    [JsonPropertyName("src")] public string? Src { get; set; }

    [JsonPropertyName("sizes")] public string? Sizes { get; set; }

    [JsonPropertyName("type")] public string? Type { get; set; }

    [JsonPropertyName("purpose")] public string? Purpose { get; set; }
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(BitAppDocument))]
[JsonSerializable(typeof(WebAppManifest))]
[JsonSerializable(typeof(AuthorizationServerMetadata))]
[JsonSerializable(typeof(ProtectedResourceMetadata))]
[JsonSerializable(typeof(TokenResponse))]
[JsonSerializable(typeof(MonitoringClientMetadata))]
[JsonSerializable(typeof(PendingConnection))]
public partial class MonitoringJsonContext : JsonSerializerContext;
