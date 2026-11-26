using Bit.Fabric.Shared.Features.Growth.Dtos;

namespace Bit.Fabric.Shared.Features.Growth;

/// <summary>
/// The growth page's figures: projects, Trials and Licenses over time, every team's together, for global admins only
/// (<see cref="AppFeatures.System.Growth_View"/>). Totals only, counted in the database.
/// </summary>
[Route("api/v1/[controller]/[action]/")]
public interface IGrowthController : IAppController
{
    /// <summary>
    /// Every day's counts in <paramref name="timeZone"/> (an IANA or Windows id; UTC when it isn't one), and today's totals.
    /// </summary>
    [HttpGet("{?timeZone}"), AuthorizedApi]
    Task<GrowthDataDto> GetData(string? timeZone, CancellationToken cancellationToken);
}
