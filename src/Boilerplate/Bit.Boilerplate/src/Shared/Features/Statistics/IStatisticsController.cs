namespace Boilerplate.Shared.Features.Statistics;

[Route("api/v1/[controller]/[action]/")]
public interface IStatisticsController : IAppController
{
    [HttpGet("{packageId}")]
    Task<NugetStatsDto> GetNugetStats(string packageId, CancellationToken cancellationToken);

    [HttpGet, Route("https://api.github.com/repos/bitfoundation/bitfabric"), ExternalApi]
    Task<GitHubStats> GetGitHubStats(CancellationToken cancellationToken) => default!;
}
