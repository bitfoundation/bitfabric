using Bit.Fabric.Server.Api.Features.Projects.Models;
using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

/// <summary>
/// A license a team holds for one of its projects, as the license page issued and signed it, naming the team as its
/// holder (bitfabric-internal/specs/product.md, A License belongs to a Team).
/// </summary>
public partial class IssuedLicense : ITenantAware
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    /// <summary>Who got it on the license page; null once their account is deleted, as the license stays the team's.</summary>
    public Guid? IssuedByUserId { get; set; }

    public User? IssuedBy { get; set; }

    /// <summary><c>free</c>, the only plan today.</summary>
    [Required, MaxLength(20)]
    public string? Plan { get; set; }

    /// <summary>The first commit the license names, as the project had it when it was issued.</summary>
    [Required, MaxLength(64)]
    public string? FirstCommit { get; set; }

    public DateTimeOffset IssuedOn { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }

    [Required, MaxLength(2000)]
    public string? Document { get; set; }

    /// <summary>
    /// The SHA-256 of the one-time code bit CLI waits for this license with, never the code itself; null when it was issued
    /// without one.
    /// </summary>
    [MaxLength(64)]
    public string? ClaimCodeHash { get; set; }

    /// <summary>Until when the code gives the license.</summary>
    public DateTimeOffset? ClaimExpiresOn { get; set; }

    /// <summary>When bit CLI read the license with its code, which spent the code.</summary>
    public DateTimeOffset? ClaimedOn { get; set; }

    public long Version { get; set; }
}
