using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Projects.Models;

/// <summary>
/// Someone of the team who works on a project: who added an environment of it, got it a license, was given one of its
/// alert routes or was added on its Settings tab. Not who may see it: every member of the team sees every project. Its keys
/// are real, to the project, the account and the person's membership of the team, so it never outlives any of them.
/// </summary>
public partial class ProjectMember : ITenantAware
{
    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

    public TenantMembership? Membership { get; set; }

    public DateTimeOffset AddedOn { get; set; }
}
