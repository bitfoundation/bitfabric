namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

// What bit Fabric and the bitplatform Network (src/Network) say to each other: the projects a location times, and how
// each one did. The Network times anonymously, so a target carries no token, only the address any visitor opens.

/// <summary>A project one location of the bitplatform Network times every minute.</summary>
public partial class NetworkTargetDto
{
    /// <summary>The environment's id, under the name the Network's code reads (src/Network), which deploys on its own.</summary>
    [JsonPropertyName("projectId")]
    public Guid MonitoringId { get; set; }

    /// <summary>
    /// The https address to time: the home page, which the Network asks for with a query string of its own (a new GUID each
    /// time), or a static front end's server's <c>/health</c> (See IsServerHealth).
    /// </summary>
    public string? Url { get; set; }

    /// <summary>The app's front end is hosted statically, so its server's <c>/health</c> is timed instead, as is, with no cache check.</summary>
    public bool IsServerHealth { get; set; }
}

/// <summary>What one location of the bitplatform Network found, posted back after each batch of its minute's targets.</summary>
public partial class NetworkResultsDto
{
    /// <summary>The location's code, such as <c>enam</c> (See NetworkLocations).</summary>
    public string? Location { get; set; }

    /// <summary>The data center the readings ran in, such as <c>IAD</c>, from Cloudflare's <c>/cdn-cgi/trace</c>.</summary>
    public string? Colo { get; set; }

    public NetworkResultDto[] Results { get; set; } = [];
}

public partial class NetworkResultDto
{
    /// <summary>The environment's id, under the name the Network's code writes (src/Network).</summary>
    [JsonPropertyName("projectId")]
    public Guid MonitoringId { get; set; }

    /// <summary>When the reading started.</summary>
    public DateTimeOffset At { get; set; }

    /// <summary>How long the answer took, redirects within the app included; null when it didn't answer.</summary>
    public int? ResponseMs { get; set; }

    /// <summary>Why it didn't answer, in a few words, such as <c>timeout</c>.</summary>
    public string? Error { get; set; }

    /// <summary>The answer carried an <c>Age</c>, <c>cf-cache-status: HIT</c> or <c>X-Cache: HIT</c> header.</summary>
    public bool CacheHit { get; set; }
}

public partial class NetworkResultsResponseDto
{
    /// <summary>How many of the results were stored; one for a project the location doesn't time is ignored.</summary>
    public int Accepted { get; set; }
}
