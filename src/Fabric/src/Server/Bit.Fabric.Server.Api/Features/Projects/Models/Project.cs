using Bit.Fabric.Server.Api.Features.Licensing.Models;
using Bit.Fabric.Server.Api.Features.Monitoring.Models;
using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Projects.Models;

/// <summary>
/// A team's project (bitfabric-internal/specs/monitoring.md, Projects, environments and their people): its name, the id in
/// its <c>bit.json</c> once a license or its app names it, its people, the licenses the team holds for it, and one
/// monitoring per environment of its app. Every member of the team sees it; its members are who works on it.
/// </summary>
public partial class Project : ITenantAware
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required, MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>
    /// The id <c>bit new</c> wrote into the project's <c>bit.json</c>, which its licenses name and its app reports in
    /// <c>/healthz</c>; null until one of them names it. One project per id in a team; another team may have its own.
    /// </summary>
    public Guid? BitProjectId { get; set; }

    /// <summary>The full sha of the first commit its newest license names.</summary>
    [MaxLength(64)]
    public string? FirstCommit { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public List<ProjectMonitoring> Monitorings { get; set; } = [];

    public List<ProjectMember> Members { get; set; } = [];

    public List<IssuedLicense> Licenses { get; set; } = [];

    public long Version { get; set; }
}
