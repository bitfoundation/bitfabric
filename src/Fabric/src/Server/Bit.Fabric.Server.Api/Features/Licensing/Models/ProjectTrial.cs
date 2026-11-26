namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

/// <summary>
/// The trial of a project bit CLI made, by the id <c>bit new</c> writes into its bit.json, asked for with no sign-in. It
/// belongs to no team: the codebase has it, whichever team later gets the project its license. Asked again, the same
/// trial is given back rather than a new one, signed for the first commit it was first asked with.
/// </summary>
public partial class ProjectTrial
{
    /// <summary>The project's id in its bit.json, never one this server makes.</summary>
    public Guid Id { get; set; }

    [Required, MaxLength(100)]
    public string? Name { get; set; }

    /// <summary>The full sha of the first commit of the project's git history, as the trial names it.</summary>
    [Required, MaxLength(64)]
    public string? FirstCommit { get; set; }

    public DateTimeOffset IssuedOn { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }

    /// <summary>The trial as it was signed, given again to the same project rather than a new one.</summary>
    [Required, MaxLength(2000)]
    public string? Document { get; set; }

    /// <summary>
    /// The CI bit CLI asked from, by the name it gives it (<c>github-actions</c>, <c>other</c>, ...); null outside CI. It only
    /// keeps such trials out of the trial and conversion figures: no check, length or allowance depends on it.
    /// </summary>
    [MaxLength(40)]
    public string? Ci { get; set; }

    public long Version { get; set; }
}
