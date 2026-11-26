using Bit.Fabric.Server.Api.Features.Licensing;
using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Shared.Features.Licensing.Dtos;

namespace Bit.Fabric.Server.Api.Features.Projects;

/// <summary>
/// What a team's projects' licenses are today, for the projects' Licenses tab, a project's page and the AI tools alike: the
/// newest license the team holds for each, or else the trial of its codebase. A license of another team for the same id is
/// never read here, so nothing of one team's shows to another.
/// </summary>
public partial class ProjectLicenses(AppDbContext dbContext, TimeProvider timeProvider)
{
    /// <summary>bit CLI says a license is about to end from this long before (See Bit.Cli's LicenseCheck.RenewalNotice).</summary>
    public static readonly TimeSpan RenewalNotice = TimeSpan.FromDays(30);

    public static LicenseState StateOf(DateTimeOffset expiresOn, DateTimeOffset now)
        => now < expiresOn - RenewalNotice ? LicenseState.InForce
         : now < expiresOn ? LicenseState.EndingSoon
         : now < expiresOn + ProjectLicenseStatus.Grace ? LicenseState.InGrace
         : LicenseState.Ended;

    /// <summary>The license of each of <paramref name="projects"/>, by the project's id; every one of them is in the dictionary.</summary>
    public async Task<Dictionary<Guid, ProjectLicenseDto>> Of(IReadOnlyCollection<Project> projects, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        Guid[] projectIds = [.. projects.Select(project => project.Id)];
        Guid[] bitIds = [.. projects.Where(project => project.BitProjectId is not null).Select(project => project.BitProjectId!.Value)];

        // The newest license of each, of the project's own team: a license always is, through the tenant-scoped key.
        var newest = (await dbContext.IssuedLicenses.IgnoreQueryFilters().AsNoTracking()
            .Where(license => projectIds.Contains(license.ProjectId))
            .Select(license => new { license.Id, license.ProjectId, license.ExpiresOn, license.FirstCommit })
            .ToListAsync(cancellationToken))
            .GroupBy(license => license.ProjectId)
            .ToDictionary(group => group.Key, group => group.MaxBy(license => license.ExpiresOn)!);

        var trials = await dbContext.ProjectTrials.AsNoTracking()
            .Where(trial => bitIds.Contains(trial.Id))
            .ToDictionaryAsync(trial => trial.Id, trial => new { trial.ExpiresOn, trial.FirstCommit }, cancellationToken);

        return projects.ToDictionary(project => project.Id, project =>
        {
            if (newest.TryGetValue(project.Id, out var license))
            {
                var state = StateOf(license.ExpiresOn, now);

                return new ProjectLicenseDto
                {
                    Kind = state is LicenseState.Ended ? ProjectLicenseKind.LicenseEnded : ProjectLicenseKind.Licensed,
                    Until = license.ExpiresOn,
                    State = state,
                    LicenseId = license.Id,
                    FirstCommit = license.FirstCommit
                };
            }

            if (project.BitProjectId is { } bitId && trials.TryGetValue(bitId, out var trial))
                return new ProjectLicenseDto
                {
                    Kind = now < trial.ExpiresOn ? ProjectLicenseKind.Trial : ProjectLicenseKind.TrialEnded,
                    Until = trial.ExpiresOn,
                    FirstCommit = trial.FirstCommit
                };

            return new ProjectLicenseDto { Kind = project.BitProjectId is null ? ProjectLicenseKind.Unknown : ProjectLicenseKind.None };
        });
    }
}
