using Bit.Fabric.Server.Api.Features.Identity.Models;
using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// The OAuth tokens a project's owner granted bit Fabric in their app, kept apart from the project so listing projects
/// never loads them. Both tokens are stored protected with Data Protection (See MonitoringTokenProtector).
/// </summary>
public partial class ProjectConnection : ITenantAware
{
    public Guid MonitoringId { get; set; }

    public ProjectMonitoring? Monitoring { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>The customer app's issuer, which every token answer must name (RFC 9207).</summary>
    [Required, MaxLength(300)]
    public string? Issuer { get; set; }

    [Required, MaxLength(500)]
    public string? TokenEndpoint { get; set; }

    /// <summary>The RFC 8707 resource the tokens are for: the server's <c>/healthz</c>.</summary>
    [Required, MaxLength(500)]
    public string? Resource { get; set; }

    [Required, MaxLength(200)]
    public string? Scopes { get; set; }

    /// <summary>The client id the approval was given to, which every refresh must present again.</summary>
    [Required, MaxLength(500)]
    public string? ClientId { get; set; }

    [Required]
    public string? ProtectedAccessToken { get; set; }

    public string? ProtectedRefreshToken { get; set; }

    public DateTimeOffset AccessTokenExpiresAt { get; set; }

    /// <summary>
    /// Since when the report couldn't be read for a reason that may pass: a server error, no answer or a 404 from the token
    /// endpoint or <c>/healthz</c>. After an hour of it the environment needs reconnecting; a report read clears it
    /// (bitfabric-internal/specs/monitoring.md, Monitoring keeps watching when the approval breaks).
    /// </summary>
    public DateTimeOffset? ReportFailingSince { get; set; }

    /// <summary>While the environment needs reconnecting, when its round tries the token again: every 15 minutes.</summary>
    public DateTimeOffset? NextTokenAttemptAt { get; set; }

    /// <summary>The bit Fabric user who connected it; null once their account is deleted.</summary>
    public Guid? ConnectedByUserId { get; set; }

    public User? ConnectedBy { get; set; }

    public DateTimeOffset ConnectedOn { get; set; }

    public long Version { get; set; }
}
