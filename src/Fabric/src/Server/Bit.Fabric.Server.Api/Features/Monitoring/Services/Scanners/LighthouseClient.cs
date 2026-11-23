using System.Net;
using System.Net.Http.Json;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Services.Scanners;

/// <summary>
/// The api's side of bit Fabric's Lighthouse service (src/Server/Bit.Fabric.Server.Lighthouse;
/// bitfabric-internal/specs/scanners.md, Decisions, PR 11): one page, as a phone or a computer loads it, measured in the
/// service's own Chrome. The service is bit Fabric's, on a network of its own, so this client calls it directly rather
/// than through the public-address guard; what it measures is a customer's page, which the service's own guard keeps
/// Chrome on the public internet for.
/// </summary>
public partial class LighthouseClient(IHttpClientFactory httpClientFactory, ILogger<LighthouseClient> logger)
{
    public const string ClientName = "Lighthouse";

    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// One run of <paramref name="url"/>. Both kinds of one request name it by the same <paramref name="reuseKey"/>, so the
    /// second reads the first one's run. Null when the service refuses the address because it isn't on the public
    /// internet; throws when the service can't run it, which is bit Fabric's own failure rather than the app's.
    /// </summary>
    public async Task<LighthouseAudit?> Audit(string url, ProjectScanDevice device, string reuseKey, CancellationToken cancellationToken)
    {
        var http = httpClientFactory.CreateClient(ClientName);
        var formFactor = device is ProjectScanDevice.Computer ? "desktop" : "mobile";

        for (var attempt = 1; ; attempt++)
        {
            using var response = await http.PostAsJsonAsync("audit", new { url, formFactor, reuseKey }, jsonOptions, cancellationToken);

            if (response.StatusCode is HttpStatusCode.UnprocessableEntity)
            {
                logger.LogInformation("The Lighthouse service refused {Url}: it isn't on the public internet.", url);
                return null;
            }

            // Its few places to wait are taken: once more, after the time it asks for.
            if (response.StatusCode is HttpStatusCode.ServiceUnavailable && attempt is 1)
            {
                var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(30);
                logger.LogWarning("The Lighthouse service is busy; {Url} is asked for again in {Wait}.", url, wait);
                await Task.Delay(wait, cancellationToken);
                continue;
            }

            if (response.IsSuccessStatusCode is false)
                throw new HttpRequestException($"The Lighthouse service answered {(int)response.StatusCode} for {url}: {await response.Content.ReadAsStringAsync(cancellationToken)}", null, response.StatusCode);

            return await response.Content.ReadFromJsonAsync<LighthouseAudit>(jsonOptions, cancellationToken)
                   ?? throw new HttpRequestException($"The Lighthouse service answered nothing for {url}.");
        }
    }
}

/// <summary>One Lighthouse run of one page, as the service reduces it (See src/Server/Bit.Fabric.Server.Lighthouse/src/audit.ts).</summary>
public sealed class LighthouseAudit
{
    public string? RequestedUrl { get; set; }

    public string? FinalUrl { get; set; }

    public string? FormFactor { get; set; }

    public string? LighthouseVersion { get; set; }

    /// <summary>Lighthouse's code when the page couldn't be loaded or measured, such as FAILED_DOCUMENT_REQUEST or NO_FCP.</summary>
    public string? RuntimeError { get; set; }

    /// <summary>The page's requests the service's guard refused, as they went to an address that isn't public.</summary>
    public int RefusedRequests { get; set; }

    public double? BenchmarkIndex { get; set; }

    /// <summary>0 to 1.</summary>
    public double? PerformanceScore { get; set; }

    /// <summary>0 to 1.</summary>
    public double? AccessibilityScore { get; set; }

    public LighthouseMetrics Metrics { get; set; } = new();

    public List<LighthouseAuditFinding> Findings { get; set; } = [];
}

public sealed class LighthouseMetrics
{
    public double? FirstContentfulPaintMs { get; set; }

    public double? LargestContentfulPaintMs { get; set; }

    public double? TotalBlockingTimeMs { get; set; }

    public double? CumulativeLayoutShift { get; set; }

    public double? SpeedIndexMs { get; set; }

    public double? TimeToFirstByteMs { get; set; }
}

/// <summary>A failing audit of the performance or accessibility category.</summary>
public sealed class LighthouseAuditFinding
{
    /// <summary>Lighthouse's audit id, such as image-delivery-insight or color-contrast.</summary>
    public string? Id { get; set; }

    /// <summary>performance or accessibility.</summary>
    public string? Category { get; set; }

    public string? Group { get; set; }

    public double Weight { get; set; }

    /// <summary>0 to 1, below 0.9.</summary>
    public double Score { get; set; }

    public double? NumericValue { get; set; }

    public string? NumericUnit { get; set; }

    /// <summary>What fixing it saves of each metric, such as LCP and FCP in milliseconds.</summary>
    public Dictionary<string, double>? MetricSavings { get; set; }

    public double? SavingsMs { get; set; }

    public double? SavingsBytes { get; set; }

    public int ItemCount { get; set; }

    /// <summary>Up to ten addresses or elements' HTML.</summary>
    public List<string> Where { get; set; } = [];
}
