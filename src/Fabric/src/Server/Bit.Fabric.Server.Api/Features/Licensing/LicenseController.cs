using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Bit.Fabric.Server.Api.Features.Licensing.Models;
using Bit.Fabric.Server.Api.Features.Projects;
using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>
/// Projects' trials and licenses (bitfabric-internal/specs/product.md, A License belongs to a Team). A trial is the
/// codebase's, by the id in its bit.json, and needs no sign-in; a license is a team's, for its project of that id. Every
/// query here names its team itself, ignoring the tenant filter: a trial and a claim come from bit CLI with no session, and
/// the license page and the Licenses page reach every team of the signed-in account, not only the one it is signed into.
/// </summary>
[ApiVersion(1)]
[ApiController, Route("api/v{v:apiVersion}/[controller]/[action]")]
public partial class LicenseController : AppControllerBase, ILicenseController
{
    [AutoInject] private LicenseSigner signer = default!;

    [AutoInject] private ProjectLicenses projectLicenses = default!;

    [HttpPost, AllowAnonymous, EnableRateLimiting(RateLimitOptionsExtensions.LICENSE_TRIAL)]
    public async Task<LicenseResponseDto> Trial(TrialLicenseRequestDto request, CancellationToken cancellationToken)
    {
        var (name, commit) = Validate(request.Project, request.Name, request.Commit);

        if (await DbContext.ProjectTrials.AsNoTracking().FirstOrDefaultAsync(trial => trial.Id == request.Project, cancellationToken) is { } existing)
            return new() { License = existing.Document };

        // A project licensed before it ever had a trial, through the license page and bit license, gets none afterwards.
        if (await DbContext.IssuedLicenses.IgnoreQueryFilters().AnyAsync(license => license.Project!.BitProjectId == request.Project, cancellationToken))
            throw new ConflictException(Localizer["This project already has its license."]);

        var now = LicenseSigner.ToSeconds(TimeProvider.GetUtcNow());
        var expires = now.AddDays(AppSettings.Licensing.TrialDays);

        var trial = new ProjectTrial
        {
            Id = request.Project,
            Name = name,
            FirstCommit = commit,
            IssuedOn = now,
            ExpiresOn = expires,
            Document = signer.Sign(request.Project, name, commit, LicenseSigner.TrialPlan, holder: "", now, expires),
            // Kept only to count CI's trials apart: the trial above is the same with it or without it.
            Ci = string.IsNullOrWhiteSpace(request.Ci) ? null : request.Ci.Trim()
        };

        await DbContext.ProjectTrials.AddAsync(trial, cancellationToken);

        try
        {
            await DbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The same project asked twice at once: the first one's trial is the project's.
            DbContext.Entry(trial).State = EntityState.Detached;

            var raced = await DbContext.ProjectTrials.AsNoTracking().FirstOrDefaultAsync(other => other.Id == request.Project, cancellationToken);

            if (raced is null)
                throw;

            return new() { License = raced.Document };
        }

        return new() { License = trial.Document };
    }

    [HttpPost, Authorize]
    public async Task<IssuedLicenseDto> Issue(IssueLicenseRequestDto request, CancellationToken cancellationToken)
    {
        var (name, commit) = Validate(request.Project, request.Name, request.Commit);
        var code = request.Code?.Trim();

        if (string.IsNullOrEmpty(code) is false && OneTimeCode().IsMatch(code) is false)
            throw new BadRequestException(Localizer["This link's code isn't one bit CLI makes. Run the bit command again for a new link."]);

        var userId = User.GetUserId();

        // Only a team the account has joined, whichever team it is signed into now.
        var team = await TeamsOf(userId).FirstOrDefaultAsync(team => team.Id == request.TeamId, cancellationToken)
            ?? throw new BadRequestException(Localizer["Pick one of your teams to hold the license."]);

        var now = LicenseSigner.ToSeconds(TimeProvider.GetUtcNow());

        var project = await DbContext.Projects.IgnoreQueryFilters()
            .Include(project => project.Members.Where(member => member.UserId == userId))
            .FirstOrDefaultAsync(project => project.TenantId == team.Id && project.BitProjectId == request.Project, cancellationToken);

        if (project is null)
        {
            project = new Project
            {
                Id = Guid.CreateSequentialGuid(),
                TenantId = team.Id,
                Name = name,
                BitProjectId = request.Project,
                CreatedOn = now
            };
            await DbContext.Projects.AddAsync(project, cancellationToken);
        }

        // A new first commit is the same project in a new repository, which the license moves on to.
        project.FirstCommit = commit;

        if (project.Members.Count is 0)
        {
            await DbContext.ProjectMembers.AddAsync(new() { ProjectId = project.Id, TenantId = team.Id, UserId = userId, AddedOn = now }, cancellationToken);
        }

        var holder = team.Title ?? "";
        var expires = now.AddDays(AppSettings.Licensing.FreeDays);

        var license = new IssuedLicense
        {
            Id = Guid.CreateSequentialGuid(),
            TenantId = team.Id,
            ProjectId = project.Id,
            IssuedByUserId = userId,
            Plan = LicenseSigner.FreePlan,
            FirstCommit = commit,
            IssuedOn = now,
            ExpiresOn = expires,
            Document = signer.Sign(request.Project, name, commit, LicenseSigner.FreePlan, holder, now, expires),
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
            ExpiresOn = license.ExpiresOn,
            ProjectId = project.Id
        };
    }

    [HttpGet, Authorize]
    public Task<List<LicenseTeamDto>> GetTeams(CancellationToken cancellationToken)
        => TeamsOf(User.GetUserId()).ToListAsync(cancellationToken);

    [HttpGet, Authorize]
    public async Task<List<LicenseDto>> GetLicenses(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        var now = TimeProvider.GetUtcNow();

        var teams = DbContext.TenantMemberships.Where(membership => membership.UserId == userId && membership.AcceptedOn != null && membership.Tenant!.IsActive)
                                               .Select(membership => membership.TenantId);

        var licenses = await DbContext.IssuedLicenses.IgnoreQueryFilters().AsNoTracking()
            .Where(license => teams.Contains(license.TenantId))
            .OrderByDescending(license => license.IssuedOn)
            .Select(license => new
            {
                license.Id,
                license.TenantId,
                TeamTitle = license.Tenant!.Title ?? license.Tenant.Name,
                license.ProjectId,
                ProjectName = license.Project!.Name,
                BitProjectId = license.Project.BitProjectId,
                license.Plan,
                license.FirstCommit,
                license.IssuedOn,
                license.ExpiresOn,
                IssuedBy = license.IssuedBy == null ? null : license.IssuedBy.FullName ?? license.IssuedBy.UserName,
                license.Document
            })
            .ToListAsync(cancellationToken);

        return [.. licenses.Select(license => new LicenseDto
        {
            Id = license.Id,
            TeamId = license.TenantId,
            TeamTitle = license.TeamTitle,
            ProjectId = license.ProjectId,
            ProjectName = license.ProjectName,
            BitProjectId = license.BitProjectId ?? Guid.Empty,
            Plan = license.Plan,
            FirstCommit = license.FirstCommit,
            IssuedOn = license.IssuedOn,
            ExpiresOn = license.ExpiresOn,
            State = ProjectLicenses.StateOf(license.ExpiresOn, now),
            IssuedBy = license.IssuedBy,
            License = license.Document
        })];
    }

    [HttpGet, AllowAnonymous, EnableRateLimiting(RateLimitOptionsExtensions.LICENSE_CLAIM)]
    public async Task<LicenseResponseDto> Claim([FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code) || OneTimeCode().IsMatch(code.Trim()) is false)
            return new();

        var hash = Hash(code.Trim());
        var now = TimeProvider.GetUtcNow();

        var waiting = await DbContext.IssuedLicenses.IgnoreQueryFilters()
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

    /// <summary>The active teams <paramref name="userId"/> has joined, the one joined first first, with what a license names them by.</summary>
    private IQueryable<LicenseTeamDto> TeamsOf(Guid userId)
        => DbContext.TenantMemberships
            .Where(membership => membership.UserId == userId && membership.AcceptedOn != null && membership.Tenant!.IsActive)
            .OrderBy(membership => membership.AcceptedOn)
            .ThenBy(membership => membership.Tenant!.Name)
            .Select(membership => new LicenseTeamDto
            {
                Id = membership.TenantId,
                Title = string.IsNullOrEmpty(membership.Tenant!.Title) ? membership.Tenant.Name : membership.Tenant.Title
            });

    private (string Name, string Commit) Validate(Guid project, string? name, string? commit)
    {
        if (project == Guid.Empty)
            throw new BadRequestException(Localizer["The project's id is missing. Run the bit command in the project's folder again for a new link."]);

        commit = commit?.Trim().ToLowerInvariant();

        if (commit is null || CommitSha().IsMatch(commit) is false)
            throw new BadRequestException(Localizer["The project's first commit is missing. Commit the project to git, then run the bit command in its folder again."]);

        return (name!.Trim(), commit);
    }

    private static string Hash(string code) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(code)));

    /// <summary>A full sha of git's, SHA-1 or SHA-256.</summary>
    [GeneratedRegex("^([0-9a-f]{40}|[0-9a-f]{64})$")]
    private static partial Regex CommitSha();

    /// <summary>What bit CLI makes: random, URL-safe, long enough not to be guessed in the code's few minutes.</summary>
    [GeneratedRegex("^[A-Za-z0-9_-]{16,64}$")]
    private static partial Regex OneTimeCode();
}
