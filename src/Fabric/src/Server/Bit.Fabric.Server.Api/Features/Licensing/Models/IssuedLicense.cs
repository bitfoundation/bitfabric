namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

/// <summary>A license the license page issued to a signed-in account, kept as it was signed.</summary>
public partial class IssuedLicense
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public LicensedProject? Project { get; set; }

    public Guid UserId { get; set; }

    public User? User { get; set; }

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
