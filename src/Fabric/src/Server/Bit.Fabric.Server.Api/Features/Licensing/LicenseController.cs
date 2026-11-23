using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Licensing.Models;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace Bit.Fabric.Server.Api.Features.Licensing;

[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
public partial class LicenseController : AppControllerBase, ILicenseController
{
    [AutoInject] private LicenseSigner signer = default!;

    [AutoInject] private UserManager<User> userManager = default!;

    [HttpPost, AllowAnonymous, EnableRateLimiting(RateLimitOptionsExtensions.LICENSE_TRIAL)]
    public async Task<LicenseResponseDto> Trial(TrialLicenseRequestDto request, CancellationToken cancellationToken)
    {
        var (name, commit) = Validate(request.Project, request.Name, request.Commit);

        if (await DbContext.LicensedProjects.AsNoTracking().FirstOrDefaultAsync(project => project.Id == request.Project, cancellationToken) is { } existing)
            return ExistingTrial(existing);

        var now = LicenseSigner.ToSeconds(TimeProvider.GetUtcNow());
        var expires = now.AddDays(AppSettings.Licensing.TrialDays);

        var project = new LicensedProject
        {
            Id = request.Project,
            Name = name,
            FirstCommit = commit,
            CreatedOn = now,
            TrialIssuedOn = now,
            TrialExpiresOn = expires,
            TrialLicense = signer.Sign(request.Project, name, commit, LicenseSigner.TrialPlan, holder: "", now, expires)
        };

        await DbContext.LicensedProjects.AddAsync(project, cancellationToken);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same project asked twice at once: the first one's trial is the project's.
            DbContext.Entry(project).State = EntityState.Detached;

            var raced = await DbContext.LicensedProjects.AsNoTracking().FirstOrDefaultAsync(other => other.Id == request.Project, cancellationToken);

            if (raced is null)
                throw;

            return ExistingTrial(raced);
        }

        return new() { License = project.TrialLicense };
    }

    [HttpPost, Authorize]
    public async Task<IssuedLicenseDto> Issue(IssueLicenseRequestDto request, CancellationToken cancellationToken)
    {
        var (name, commit) = Validate(request.Project, request.Name, request.Commit);
        var code = request.Code?.Trim();

        if (string.IsNullOrEmpty(code) is false && OneTimeCode().IsMatch(code) is false)
            throw new BadRequestException(Localizer["This link's code isn't one bit CLI makes. Run the bit command again for a new link."]);

        var userId = User.GetUserId();
        var user = await userManager.FindByIdAsync(userId.ToString()) ?? throw new UnauthorizedException();
        var now = LicenseSigner.ToSeconds(TimeProvider.GetUtcNow());

        var project = await DbContext.LicensedProjects.FirstOrDefaultAsync(project => project.Id == request.Project, cancellationToken);

        if (project is null)
        {
            project = new LicensedProject { Id = request.Project, Name = name, FirstCommit = commit, CreatedOn = now, UserId = userId };
            await DbContext.LicensedProjects.AddAsync(project, cancellationToken);
        }
        else
        {
            // The account that first got a license is recorded as the project's, which keeps no one else from getting one:
            // a public repository shows its id and first commit to anyone.
            project.UserId ??= userId;

            // That account asking for another first commit has made a new repository from the project's files, which takes
            // the license on to it.
            if (project.UserId == userId)
            {
                project.Name = name;
                project.FirstCommit = commit;
            }
        }

        var holder = HolderOf(user);
        var expires = now.AddDays(AppSettings.Licensing.FreeDays);

        var license = new IssuedLicense
        {
            Project = project,
            UserId = userId,
            Plan = LicenseSigner.FreePlan,
            FirstCommit = commit,
            IssuedOn = now,
            ExpiresOn = expires,
            Document = signer.Sign(project.Id, name, commit, LicenseSigner.FreePlan, holder, now, expires),
            ClaimCodeHash = string.IsNullOrEmpty(code) ? null : Hash(code),
            ClaimExpiresOn = string.IsNullOrEmpty(code) ? null : now + AppSettings.Licensing.ClaimCodeLifetime
        };

        await DbContext.IssuedLicenses.AddAsync(license, cancellationToken);

        await DbContext.SaveChangesAsync(cancellationToken);

        return new()
        {
            License = license.Document,
            ProjectName = name,
            Plan = license.Plan,
            Holder = holder,
            IssuedOn = license.IssuedOn,
            ExpiresOn = license.ExpiresOn
        };
    }

    [HttpGet, AllowAnonymous, EnableRateLimiting(RateLimitOptionsExtensions.LICENSE_CLAIM)]
    public async Task<LicenseResponseDto> Claim([FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || OneTimeCode().IsMatch(code.Trim()) is false)
            return new();

        var hash = Hash(code.Trim());
        var now = TimeProvider.GetUtcNow();

        var waiting = await DbContext.IssuedLicenses
            .Where(license => license.ClaimCodeHash == hash && license.ClaimedOn == null)
            .OrderByDescending(license => license.IssuedOn)
            .ToListAsync(cancellationToken);

        if (waiting.Count is 0)
            return new();

        var license = waiting.FirstOrDefault(license => license.ClaimExpiresOn > now);

        // Every license issued with the code is spent with it, the newest given.
        foreach (var spent in waiting)
        {
            spent.ClaimedOn = now;
        }

        await DbContext.SaveChangesAsync(cancellationToken);

        return new() { License = license?.Document };
    }

    private LicenseResponseDto ExistingTrial(LicensedProject project)
    {
        if (project.TrialLicense is null)
            throw new ConflictException(Localizer["This project already has its license."]);

        return new() { License = project.TrialLicense };
    }

    private (string Name, string Commit) Validate(Guid project, string? name, string? commit)
    {
        if (project == Guid.Empty)
            throw new BadRequestException(Localizer["The project's id is missing. Run the bit command in the project's folder again for a new link."]);

        commit = commit?.Trim().ToLowerInvariant();

        if (commit is null || CommitSha().IsMatch(commit) is false)
            throw new BadRequestException(Localizer["The project's first commit is missing. Commit the project to git, then run the bit command in its folder again."]);

        return (name!.Trim(), commit);
    }

    /// <summary>
    /// Whom the license names: the account's full name, or no one. Never an email address, since the license goes into the
    /// project's bit.json and its git history, often public.
    /// </summary>
    public static string HolderOf(User user) => user.FullName?.Trim() is { Length: > 0 } fullName && fullName.Contains('@') is false ? fullName : "";

    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>A full sha of git's, SHA-1 or SHA-256.</summary>
    [GeneratedRegex("^([0-9a-f]{40}|[0-9a-f]{64})$")]
    private static partial Regex CommitSha();

    /// <summary>What bit CLI makes: random, URL-safe, long enough not to be guessed in the code's few minutes.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex OneTimeCode();
}
