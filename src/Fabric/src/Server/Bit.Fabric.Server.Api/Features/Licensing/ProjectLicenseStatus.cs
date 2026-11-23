namespace Bit.Fabric.Server.Api.Features.Licensing;

/// <summary>
/// Whether a project bit CLI made has a trial or a license in force, for what serves projects by their id, such as the
/// docs MCP server's <c>project</c> query value.
/// </summary>
public interface IProjectLicenseStatus
{
    /// <summary>
    /// True while the project's trial lasts, and while a license issued for it lasts, plus the weeks of grace bit CLI gives
    /// after one ends.
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

    public async Task<bool> IsLicensed(Guid projectId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var project = await dbContext.LicensedProjects.AsNoTracking()
            .Where(project => project.Id == projectId)
            .Select(project => new { project.TrialExpiresOn })
            .FirstOrDefaultAsync(cancellationToken);

        if (project is null)
            return false;

        if (project.TrialExpiresOn > now)
            return true;

        var endedAfter = now - Grace;

        return await dbContext.IssuedLicenses.AnyAsync(license => license.ProjectId == projectId && license.ExpiresOn > endedAfter, cancellationToken);
    }
}
