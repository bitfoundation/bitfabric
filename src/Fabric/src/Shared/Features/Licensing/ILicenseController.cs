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
    /// A free license for the signed-in account, which any account gets for any project while every license is free: the
    /// first one to ask is recorded as the project's, and keeps no one else from it. Asked again, for the same project in a
    /// new repository, it issues one for the new first commit.
    /// </summary>
    [HttpPost, AuthorizedApi]
    Task<IssuedLicenseDto> Issue(IssueLicenseRequestDto request, CancellationToken cancellationToken);

    /// <summary>
    /// The license issued with <paramref name="code"/>, which bit CLI waits for: given once, then the code is spent. Null
    /// until the license is issued, and once the code is spent or too old.
    /// </summary>
    [HttpGet("{?code}")]
    Task<LicenseResponseDto> Claim(string? code, CancellationToken cancellationToken);
}
