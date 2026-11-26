using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Projects.Models;

namespace Bit.Fabric.Server.Api.Features.Projects;

/// <summary>
/// Links an environment to the project its app says it is (bitfabric-internal/specs/monitoring.md, The approval links an
/// app to a project), after every round that read the app's report, the first round of connecting included. Only the
/// report, which the app's own approval lets bit Fabric read, links an app to an id; nothing is guessed from names or
/// addresses. It runs in background jobs, so every query names its team and ignores the tenant filter.
/// </summary>
public partial class ProjectLinker(AppDbContext dbContext, TimeProvider timeProvider, ILogger<ProjectLinker> logger)
{
    public async Task Link(ProjectMonitoring monitoring, Guid reportedProjectId, CancellationToken cancellationToken)
    {
        var tenantId = monitoring.TenantId;

        var project = await dbContext.Projects.IgnoreQueryFilters()
            .FirstAsync(project => project.TenantId == tenantId && project.Id == monitoring.ProjectId, cancellationToken);

        if (project.BitProjectId == reportedProjectId)
            return;

        var other = await dbContext.Projects.IgnoreQueryFilters()
            .FirstOrDefaultAsync(other => other.TenantId == tenantId && other.BitProjectId == reportedProjectId && other.Id != project.Id, cancellationToken);

        try
        {
            if (other is not null)
            {
                await Move(monitoring, project, other, cancellationToken);
            }
            else if (project.BitProjectId is null || await MayFollow(project, monitoring, cancellationToken))
            {
                project.BitProjectId = reportedProjectId;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        catch (DbUpdateException exp)
        {
            // Another round linked the same id a moment ago (the team's ids are unique); the next round sees it done.
            LogLinkRaced(logger, monitoring.Id, exp.Message);
            dbContext.ChangeTracker.Clear();
        }
    }

    /// <summary>
    /// Whether a project with another id takes the one its app reports now: only while it holds no license, which names
    /// its id, and no other environment of it still reports the id it has. A demo made again on each deploy gets a new id
    /// until its fixed one is set (bitfabric-internal/specs/product.md, The template's demos keep one identity).
    /// </summary>
    private async Task<bool> MayFollow(Project project, ProjectMonitoring monitoring, CancellationToken cancellationToken)
        => await dbContext.IssuedLicenses.IgnoreQueryFilters().AnyAsync(license => license.TenantId == project.TenantId && license.ProjectId == project.Id, cancellationToken) is false
           && await dbContext.Monitorings.IgnoreQueryFilters().AnyAsync(other => other.TenantId == project.TenantId && other.ProjectId == project.Id && other.Id != monitoring.Id && other.ReportedProjectId == project.BitProjectId, cancellationToken) is false;

    /// <summary>
    /// The environment joins the project of the id its app reports. Every table of its history is keyed by the environment,
    /// so only its project changes. A project left with no environment and no license goes, its members joining the other.
    /// </summary>
    private async Task Move(ProjectMonitoring monitoring, Project from, Project to, CancellationToken cancellationToken)
    {
        var tenantId = monitoring.TenantId;

        var environments = await dbContext.Monitorings.IgnoreQueryFilters()
            .Where(other => other.TenantId == tenantId && other.ProjectId == to.Id)
            .Select(other => new { other.Id, other.Environment })
            .ToListAsync(cancellationToken);

        // A project made on the license page has only bit.json's name; the team's own name for the app is better.
        if (environments.Count is 0)
        {
            to.Name = from.Name;
        }

        // One of each environment in a project: a second Production takes its address's host as its name.
        if (environments.Any(other => string.Equals(other.Environment, monitoring.Environment, StringComparison.OrdinalIgnoreCase)))
        {
            monitoring.Environment = Uri.TryCreate(monitoring.Address, UriKind.Absolute, out var address) ? address.Host : monitoring.Id.ToString("N")[..8];
        }

        monitoring.ProjectId = to.Id;
        monitoring.Project = to;
        monitoring.Name = ProjectMonitoring.NameOf(to.Name, monitoring.Environment);

        var now = timeProvider.GetUtcNow();
        var members = await dbContext.ProjectMembers.IgnoreQueryFilters().Where(member => member.TenantId == tenantId && (member.ProjectId == from.Id || member.ProjectId == to.Id)).ToListAsync(cancellationToken);

        foreach (var joining in members.Where(member => member.ProjectId == from.Id && members.Any(existing => existing.ProjectId == to.Id && existing.UserId == member.UserId) is false))
        {
            await dbContext.ProjectMembers.AddAsync(new() { ProjectId = to.Id, TenantId = tenantId, UserId = joining.UserId, AddedOn = now }, cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        var isLeftEmpty = await dbContext.Monitorings.IgnoreQueryFilters().AnyAsync(other => other.TenantId == tenantId && other.ProjectId == from.Id, cancellationToken) is false
                          && await dbContext.IssuedLicenses.IgnoreQueryFilters().AnyAsync(license => license.TenantId == tenantId && license.ProjectId == from.Id, cancellationToken) is false;

        if (isLeftEmpty)
        {
            // Its members go with it (cascade).
            await dbContext.Projects.IgnoreQueryFilters().Where(project => project.TenantId == tenantId && project.Id == from.Id).ExecuteDeleteAsync(cancellationToken);
        }

        LogMoved(logger, monitoring.Id, from.Id, to.Id);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Monitoring {MonitoringId} moved from project {From} to {To}, the project of the id its app reports.")]
    private static partial void LogMoved(ILogger logger, Guid monitoringId, Guid from, Guid to);

    [LoggerMessage(Level = LogLevel.Information, Message = "Linking monitoring {MonitoringId} raced another round: {Problem}")]
    private static partial void LogLinkRaced(ILogger logger, Guid monitoringId, string problem);
}
