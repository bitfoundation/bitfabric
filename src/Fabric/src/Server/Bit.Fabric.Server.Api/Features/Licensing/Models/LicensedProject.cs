namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

/// <summary>
/// A project bit CLI made or licensed, by the id <c>bit new</c> writes into its bit.json. It starts with its trial and no
/// person attached; signing in on the license page makes it that account's. Global, not per tenant: a project is a
/// person's, whichever team they are in.
/// </summary>
public partial class LicensedProject
{
    /// <summary>The project's id in its bit.json, never one this server makes.</summary>
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>The full sha of the first commit of the project's git history, the last one a license was issued for.</summary>
    [Required, MaxLength(64)]
    public string? FirstCommit { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    /// <summary>When <c>bit new</c> got the project's trial; null for a project that never had one.</summary>
    public DateTimeOffset? TrialIssuedOn { get; set; }

    public DateTimeOffset? TrialExpiresOn { get; set; }

    /// <summary>The trial as it was signed, given again to the same project rather than a new one.</summary>
    [MaxLength(2000)]
    public string? TrialLicense { get; set; }

    /// <summary>The account the project belongs to, from its first free license on; null before.</summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    public List<IssuedLicense> Licenses { get; set; } = [];

    public long Version { get; set; }
}
