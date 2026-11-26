namespace Bit.Fabric.Server.Api.Features.Projects;

/// <summary>
/// What someone leaving a team, or being removed or kicked out of it, takes with them (bitfabric-internal/specs/
/// monitoring.md, Leaving a Team ends its alerts): their place on the team's projects and every alert route that reaches
/// them there, on every channel. Called inside the transaction that ends the membership, so both go or neither does.
/// </summary>
public partial class TeamDeparture(AppDbContext dbContext)
{
    public async Task Leave(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        await dbContext.ProjectAlertRoutes.IgnoreQueryFilters()
            .Where(route => route.TenantId == tenantId && route.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);

        await dbContext.ProjectMembers.IgnoreQueryFilters()
            .Where(member => member.TenantId == tenantId && member.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
