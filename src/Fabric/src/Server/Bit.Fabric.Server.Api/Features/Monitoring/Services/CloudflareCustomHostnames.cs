using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services;

/// <summary>
/// Status pages on customers' own host names through Cloudflare for SaaS (bitfabric-internal/specs/monitoring.md, Status
/// page): bit Fabric asks Cloudflare to take <c>status.myapp.com</c> on bit Fabric's zone, the customer points a CNAME at
/// <see cref="MonitoringOptions.StatusPageCnameTarget"/>, and Cloudflare validates it and issues the certificate over
/// HTTP. It needs the zone's API token to hold "SSL and Certificates: Edit", and Cloudflare for SaaS turned on for the
/// zone with a fallback origin; until both are set, <see cref="IsConfigured"/> is false and the pages stay on bit
/// Fabric's own address.
/// </summary>
public partial class CloudflareCustomHostnames
{
    [AutoInject] private IHttpClientFactory httpClientFactory = default!;
    [AutoInject] private ServerApiSettings settings = default!;
    [AutoInject] private ILogger<CloudflareCustomHostnames> logger = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;

    public sealed record State(string Id, string Status);

    private string? ZoneId => settings.Monitoring.StatusPageZoneId ?? settings.Cloudflare?.ZoneIds.FirstOrDefault();

    public bool IsConfigured => string.IsNullOrWhiteSpace(settings.Cloudflare?.ApiToken) is false
                                && string.IsNullOrWhiteSpace(ZoneId) is false
                                && string.IsNullOrWhiteSpace(settings.Monitoring.StatusPageCnameTarget) is false;

    public async Task<State> Create(string hostname, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Post, "");
        request.Content = JsonContent.Create(new CreateBody { Hostname = hostname, Ssl = new() { Method = "http", Type = "dv" } }, CloudflareJsonContext.Default.CreateBody);

        return await Send(request, cancellationToken);
    }

    public async Task<State> Get(string id, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Get, id);
        return await Send(request, cancellationToken);
    }

    public async Task Delete(string id, CancellationToken cancellationToken)
    {
        using var request = Request(HttpMethod.Delete, id);
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode is false && response.StatusCode is not System.Net.HttpStatusCode.NotFound)
            logger.LogWarning("Removing custom host name {Id} answered {Status}.", id, (int)response.StatusCode);
    }

    private HttpRequestMessage Request(HttpMethod method, string id)
    {
        HttpRequestMessage request = new(method, $"https://api.cloudflare.com/client/v4/zones/{ZoneId}/custom_hostnames{(id.Length > 0 ? $"/{id}" : "")}");
        request.Headers.Add("Authorization", $"Bearer {settings.Cloudflare!.ApiToken}");
        return request;
    }

    /// <summary>Active once the host name and its certificate both are; otherwise Cloudflare's own word for where it is.</summary>
    private async Task<State> Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClientFactory.CreateClient().SendAsync(request, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync(CloudflareJsonContext.Default.HostnameResponse, cancellationToken);

        if (body?.Success is not true || body.Result is null)
        {
            var reason = body?.Errors?.FirstOrDefault()?.Message ?? $"HTTP {(int)response.StatusCode}";
            throw new BadRequestException(localizer[nameof(AppStrings.MonitoringCloudflareRefusedHostName), reason]);
        }

        var status = body.Result.Status is "active" && body.Result.Ssl?.Status is not "active" ? body.Result.Ssl?.Status ?? "pending" : body.Result.Status ?? "pending";
        return new(body.Result.Id!, status);
    }

    public sealed class CreateBody
    {
        [JsonPropertyName("hostname")] public string? Hostname { get; set; }

        [JsonPropertyName("ssl")] public SslBody? Ssl { get; set; }
    }

    public sealed class SslBody
    {
        [JsonPropertyName("method")] public string? Method { get; set; }

        [JsonPropertyName("type")] public string? Type { get; set; }

        [JsonPropertyName("status")] public string? Status { get; set; }
    }

    public sealed class HostnameResponse
    {
        [JsonPropertyName("success")] public bool Success { get; set; }

        [JsonPropertyName("errors")] public ErrorBody[]? Errors { get; set; }

        [JsonPropertyName("result")] public HostnameResult? Result { get; set; }
    }

    public sealed class HostnameResult
    {
        [JsonPropertyName("id")] public string? Id { get; set; }

        [JsonPropertyName("status")] public string? Status { get; set; }

        [JsonPropertyName("ssl")] public SslBody? Ssl { get; set; }
    }

    public sealed class ErrorBody
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
    }
}

[JsonSerializable(typeof(CloudflareCustomHostnames.CreateBody))]
[JsonSerializable(typeof(CloudflareCustomHostnames.HostnameResponse))]
public partial class CloudflareJsonContext : JsonSerializerContext;
