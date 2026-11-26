namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>
/// Whether a project bit CLI made has a trial or a license in force, for what serves projects by their id, such as the
/// docs MCP server's <c>project</c> query value.
/// </summary>
public interface IProjectLicenseStatus
{
    /// <summary>
    /// True while the project's trial lasts, and while a license any team holds for it lasts, plus the weeks of grace bit CLI
    /// gives after one ends.
    /// </summary>
    Task<bool> IsLicensed(Guid projectId, CancellationToken cancellationToken);

    /// <summary><see cref="IsLicensed(Guid, CancellationToken)"/> for an id as a query string has it; false for one that isn't an id.</summary>
    Task<bool> IsLicensed(string? projectId, CancellationToken cancellationToken)
        => Guid.TryParse(projectId, out var id) ? IsLicensed(id, cancellationToken) : Task.FromResult(false);
}

public partial class ProjectLicenseStatus : IProjectLicenseStatus
{
    /// <summary>How long after a license ends bit CLI keeps working, with a line asking to renew it.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromDays(14);

    [AutoInject] private AppDbContext dbContext = default!;

    [AutoInject] private TimeProvider timeProvider = default!;

    /// <param name="projectId">The id in the project's bit.json, which teams' projects keep as their BitProjectId.</param>
    public async Task<bool> IsLicensed(Guid projectId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        if (await dbContext.ProjectTrials.AnyAsync(trial => trial.Id == projectId && trial.ExpiresOn > now, cancellationToken))
            return true;

        var endedAfter = now - Grace;

        // Any team's: the server answers the codebase, with no sign-in and no team.
        return await dbContext.IssuedLicenses.IgnoreQueryFilters()
            .AnyAsync(license => license.Project!.BitProjectId == projectId && license.ExpiresOn > endedAfter, cancellationToken);
    }
}
