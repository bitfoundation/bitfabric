using System.Security.Cryptography;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Services;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// What the bitplatform Network (src/Network) reads and writes (bitfabric-internal/specs/monitoring.md, Germany, plus the
/// locations the customer picks): each location asks for the projects it times every minute and posts back how long each
/// took, from the data center it really ran in. Nobody signs in to it; it holds
/// <see cref="MonitoringOptions.NetworkSecret"/>, sent in the <see cref="SecretHeader"/> header, and with no secret set the
/// endpoints aren't there. The Network times anonymously, so a target carries no token, only the https address any
/// visitor opens, which bit Fabric checked when the project was added (See PublicAddressGuard).
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]"), AllowAnonymous]
public partial class MonitoringNetworkController : AppControllerBase
{
    public const string SecretHeader = "X-Network-Secret";

    /// <summary>One post is a batch of a location's minute, 50 projects at the most; far more isn't the Network.</summary>
    private const int MaxResultsPerPost = 200;

    /// <summary>A reading that ran longer ago than this, or later than now, would rewrite history rather than add to it.</summary>
    private static readonly TimeSpan maxAge = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan maxClockSkew = TimeSpan.FromMinutes(1);

    [AutoInject] private ILogger<MonitoringNetworkController> logger = default!;
    [AutoInject] private ProjectLandings landings = default!;

    /// <summary>The projects a location times every minute: what to open, and whether it is a static front end's server.</summary>
    [HttpGet]
    public async Task<NetworkTargetDto[]> Targets(string location, CancellationToken cancellationToken)
    {
        EnsureCalledByTheNetwork();

        var networkLocation = NetworkLocations.Find(location) ?? throw new BadRequestException();

        return [.. (await TargetsOf(networkLocation, cancellationToken)).Select(project => new NetworkTargetDto
        {
            ProjectId = project.Id,
            // The page the front door lands on, once a round from here found it, so a location times one request too.
            Url = landings.AddressOf(project),
            IsServerHealth = project.HasStaticFrontEnd
        })];
    }

    /// <summary>
    /// Stores each reading as a round of its project from the location's region, with the data center it ran in, and adds
    /// it to the home page's 15 minutes there, as bit Fabric's own rounds are (See ProjectProber). A reading of a project the
    /// location doesn't time is ignored.
    /// </summary>
    [HttpPost]
    public async Task<NetworkResultsResponseDto> Results(NetworkResultsDto request, CancellationToken cancellationToken)
    {
        EnsureCalledByTheNetwork();

        var location = NetworkLocations.Find(request.Location) ?? throw new BadRequestException();

        // Every reading names the data center it really ran in; a three-letter airport code, such as IAD.
        if (request.Colo is not { Length: >= 2 and <= 8 } colo || colo.All(char.IsAsciiLetterOrDigit) is false)
            throw new BadRequestException();

        if (request.Results.Length > MaxResultsPerPost)
            throw new BadRequestException();

        var targets = (await TargetsOf(location, cancellationToken)).ToDictionary(project => project.Id);
        var now = TimeProvider.GetUtcNow();
        List<ProjectProbe> probes = [];

        foreach (var result in request.Results)
        {
            if (targets.TryGetValue(result.ProjectId, out var project) is false)
                continue;

            var at = result.At.ToUniversalTime();
            if (at < now - maxAge || at > now + maxClockSkew)
                continue;

            var ms = result.ResponseMs is { } responseMs ? Math.Clamp(responseMs, 0, 120_000) : (int?)null;

            probes.Add(new()
            {
                Id = Guid.CreateSequentialGuid(),
                ProjectId = project.Id,
                TenantId = project.TenantId,
                At = at,
                Region = location.Name,
                Colo = colo.ToUpperInvariant(),
                Outcome = ms is null ? ProjectProbeOutcome.Unreachable : ProjectProbeOutcome.Reached,
                ResponseMs = ms,
                // As from Germany: a server's /health says nothing of a cache in front of the pages.
                HasCacheWarning = result.CacheHit && project.HasStaticFrontEnd is false,
                Problem = ms is null ? ProblemOf(result.Error) : null
            });
        }

        if (probes.Count > 0)
        {
            await DbContext.ProjectProbes.AddRangeAsync(probes, cancellationToken);
            await DbContext.SaveChangesAsync(cancellationToken);

            await ProjectRollupWriter.Add(DbContext, probes.Select(ProjectProber.HomePagePart), cancellationToken);
        }

        if (probes.Count < request.Results.Length)
        {
            logger.LogInformation("{Ignored} of {Total} readings from {Location} ({Colo}) were for projects it doesn't time, or out of time.",
                                  request.Results.Length - probes.Count, request.Results.Length, location.Code, colo);
        }

        return new() { Accepted = probes.Count };
    }

    /// <summary>
    /// The projects that picked the location, first added first, up to <see cref="MonitoringOptions.NetworkTargetsPerLocation"/>.
    /// Only https addresses: the Network opens what any visitor can, and never a local one bit Fabric allows itself in
    /// Development. A project waiting to be reconnected isn't watched from anywhere, as from Germany (See ProjectProbeJobRunner).
    /// </summary>
    private async Task<List<Project>> TargetsOf(NetworkLocation location, CancellationToken cancellationToken)
    {
        var projects = (await DbContext.Projects.IgnoreQueryFilters().AsNoTracking()
            .Where(project => project.Locations.Contains(location.Code) && project.ConnectionStatus != ProjectConnectionStatus.ReconnectNeeded && project.ConnectionStatus != ProjectConnectionStatus.Disconnected)
            .OrderBy(project => project.CreatedOn).ThenBy(project => project.Id)
            .ToListAsync(cancellationToken))
            .Where(project => ProjectProber.FrontDoorOf(project).StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var limit = AppSettings.Monitoring.NetworkTargetsPerLocation;

        if (projects.Count > limit)
        {
            logger.LogWarning("{Count} projects picked {Location}, which times the first {Limit}: raise Monitoring:NetworkTargetsPerLocation once the Network is on a paid plan.",
                              projects.Count, location.Code, limit);
            projects = projects[..limit];
        }

        return projects;
    }

    /// <summary>Why a reading got no answer, in words for the customer, from the few the Network sends.</summary>
    private static string ProblemOf(string? error) => error switch
    {
        null or "" => "Your app didn't answer.",
        "timeout" => "Your app didn't answer in time.",
        _ => $"Your app didn't answer: {(error.Length > 200 ? error[..200] : error)}"
    };

    /// <summary>
    /// The secret compared in constant time, through a hash of each side so its length doesn't leak either. No secret set
    /// turns the endpoints off: they answer as if they weren't there.
    /// </summary>
    private void EnsureCalledByTheNetwork()
    {
        var secret = AppSettings.Monitoring.NetworkSecret;

        if (string.IsNullOrEmpty(secret))
            throw new ResourceNotFoundException();

        var given = Request.Headers[SecretHeader].ToString();

        if (CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(given)), SHA256.HashData(Encoding.UTF8.GetBytes(secret))) is false)
            throw new UnauthorizedException();
    }
}
