using Bit.Fabric.Shared.Features.Licensing.Dtos;

namespace Bit.Fabric.Shared.Features.Licensing;

/// <summary>
/// Projects' licenses, which bit CLI keeps in a project's bit.json and checks on its own: a trial for each new project,
/// asked for by <c>bit new</c> without an account, and a free license for a signed-in account from the license page.
/// </summary>
[Route("api/v1/[controller]/[action]/")]
public interface ILicenseController : IAppController
{
    /// <summary>
    /// A new project's trial. Anonymous and limited per address; a project asking again gets the trial it already has.
    /// </summary>
    [HttpPost]
    Task<LicenseResponseDto> Trial(TrialLicenseRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// A free license held by one of the signed-in account's teams, for that team's project of the id the link names,
    /// which is made when the team has none (bitfabric-internal/specs/product.md, A License belongs to a Team). Any team
    /// gets one for any id while every license is free; asked again, for the same project in a new repository, it issues
    /// one for the new first commit.
    /// </summary>
    [HttpPost, AuthorizedApi]
    Task<IssuedLicenseDto> Issue(IssueLicenseRequestDto request, CancellationToken cancellationToken);

    /// <summary>The teams the signed-in account may get a license for, the one it accepted first first.</summary>
    [HttpGet, AuthorizedApi]
    Task<List<LicenseTeamDto>> GetTeams(CancellationToken cancellationToken);

    /// <summary>Every license of each team the signed-in account is a member of, newest first.</summary>
    [HttpGet, AuthorizedApi]
    Task<List<LicenseDto>> GetLicenses(CancellationToken cancellationToken);

    /// <summary>
    /// The license issued with <paramref name="code"/>, which bit CLI waits for: given once, then the code is spent. Null
    /// until the license is issued, and once the code is spent or too old.
    /// </summary>
    [HttpGet("{?code}")]
    Task<LicenseResponseDto> Claim(string? code, CancellationToken cancellationToken);
}
