namespace Bit.Fabric.Server.Api;

/// <summary>How bit Fabric watches customers' apps (bitfabric-internal/specs/monitoring.md).</summary>
public partial class MonitoringOptions
{
    /// <summary>
    /// Lets bit Fabric open loopback and private addresses, over http as well, which a customer's app never has. For
    /// Development and the tests, where the app bit Fabric watches is itself on localhost; never on a deployed server, where
    /// it would let anyone who adds a project reach the server's own network (SSRF).
    /// </summary>
    public bool AllowLocalAddresses { get; set; }

    /// <summary>The name this server's rounds carry, shown on every page as where the check ran from.</summary>
    public string Region { get; set; } = "Germany";

    /// <summary>How long one request to a customer's app may take before the round counts it as not answering.</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(15);

    /// <summary>How many projects one run of the probe job checks at the same time.</summary>
    public int Parallelism { get; set; } = 16;

    /// <summary>
    /// How much of the minute the probe job's rounds are spread over: each project starts at its own moment in it, the same
    /// every minute (See ProjectProbeJobRunner.OffsetOf), so they don't all reach one host, or load this server, at once.
    /// It leaves the rest of the minute for the last ones to finish.
    /// </summary>
    public TimeSpan RoundSpread { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// bit Fabric's own OAuth client id in customers' apps. By default the address of its client metadata document
    /// (CIMD), which every app made from the template accepts without registration; set it where the customer's app
    /// has bit Fabric pre-registered instead, as the tests do, since an app refuses a metadata document on localhost.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// The secret the bitplatform Network (src/Network) sends in the <c>X-Network-Secret</c> header to read its targets and
    /// post its readings (See MonitoringNetworkController); the Worker holds the same value as its <c>NETWORK_SECRET</c>.
    /// Empty turns those endpoints off, so the project pages show Germany alone.
    /// </summary>
    public string? NetworkSecret { get; set; }

    /// <summary>
    /// How many projects one location of the bitplatform Network times a minute, the first added first. 50 keep every
    /// location within the Workers free plan's daily allowance (src/Network/README.md); raise it on a paid plan.
    /// </summary>
    public int NetworkTargetsPerLocation { get; set; } = 50;

    /// <summary>
    /// What a customer's CNAME record points at to put a status page on their own host name, such as
    /// <c>status-pages.bitfabric.co</c>: a name on bit Fabric's zone that Cloudflare for SaaS uses as the fallback origin.
    /// Without it, status pages stay on bit Fabric's own address (See CloudflareCustomHostnames).
    /// </summary>
    public string? StatusPageCnameTarget { get; set; }

    /// <summary>The Cloudflare zone that holds the custom host names; the first of <c>Cloudflare:ZoneIds</c> by default.</summary>
    public string? StatusPageZoneId { get; set; }

    /// <summary>
    /// How many scans run at the same time across every project and every instance of bit Fabric's server: a distributed
    /// semaphore's slots, so a busy hour queues them rather than reading many apps at once.
    /// </summary>
    public int ScanParallelism { get; set; } = 4;

    /// <summary>How long after a deployment its scans start, so the new build is up on every instance by then.</summary>
    public TimeSpan ScanAfterDeploymentDelay { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>How long one request of a scan may take; a scanner reads only what any visitor sees, so it needn't wait long.</summary>
    public TimeSpan ScanRequestTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>How long one scan may take in all before it's given up as failed.</summary>
    public TimeSpan ScanTimeout { get; set; } = TimeSpan.FromMinutes(3);

    /// <summary>
    /// bit Fabric's Lighthouse service (src/Server/Bit.Fabric.Server.Lighthouse), which runs the performance and accessibility
    /// scans in a Chrome of its own on a network of its own; the AppHost sets it. Empty leaves those two scans out.
    /// </summary>
    public string? LighthouseUrl { get; set; }

    /// <summary>
    /// How many browser scans run at the same time across every project and instance: their own slots, apart from
    /// <see cref="ScanParallelism"/>'s, since the Lighthouse service runs one page at a time and a page takes a Chrome.
    /// </summary>
    public int BrowserScanParallelism { get; set; } = 1;

    /// <summary>How long one browser scan may take in all: a few pages, each loaded on a phone and a computer.</summary>
    public TimeSpan BrowserScanTimeout { get; set; } = TimeSpan.FromMinutes(6);

    /// <summary>How long one page's run may take, its wait for its turn in the Lighthouse service included.</summary>
    public TimeSpan LighthouseRequestTimeout { get; set; } = TimeSpan.FromMinutes(4);
}
