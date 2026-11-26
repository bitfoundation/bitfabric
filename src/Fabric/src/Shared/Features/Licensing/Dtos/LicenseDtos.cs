namespace Bit.Fabric.Shared.Features.Licensing.Dtos;

// A project's license is the text bit CLI keeps in the project's bit.json: bfl1.<payload>.<signature>, signed by bit Fabric
// and checked by bit CLI without asking anyone. A project is tied to its id in bit.json and to the first commit of its git
// history, so the same files in another repository need a license of their own.

/// <summary>What <c>bit new</c> sends for a new project's trial, right after the project's first commit.</summary>
[DtoResourceType(typeof(AppStrings))]
public partial class TrialLicenseRequestDto
{
    /// <summary>The project's id, which <c>bit new</c> writes into bit.json as <c>projectId</c>.</summary>
    public Guid Project { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Project name")]
    public string? Name { get; set; }

    /// <summary>The full sha of the project's first git commit.</summary>
    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(64, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "First commit")]
    public string? Commit { get; set; }

    /// <summary>
    /// The CI bit CLI asks from, by the name it gives it (<c>github-actions</c>, <c>other</c>, ...); null outside CI. bit
    /// Fabric keeps it only to count such trials apart: the trial is the same, as long, and allows the same.
    /// </summary>
    [MaxLength(40, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? Ci { get; set; }
}

/// <summary>What the license page sends for a project's free license, from the link bit CLI printed.</summary>
[DtoResourceType(typeof(AppStrings))]
public partial class IssueLicenseRequestDto
{
    public Guid Project { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Project name")]
    public string? Name { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(64, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "First commit")]
    public string? Commit { get; set; }

    /// <summary>
    /// The one-time code bit CLI made for the link and waits with: it reads the license once with it (See
    /// ILicenseController.Claim). Empty when there is no bit CLI waiting.
    /// </summary>
    [MaxLength(64, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    public string? Code { get; set; }

    /// <summary>The team that holds the license, one the signed-in account is a member of (See ILicenseController.GetTeams).</summary>
    public Guid TeamId { get; set; }
}

/// <summary>A license as bit CLI writes it into bit.json; null when there is none to give.</summary>
public partial class LicenseResponseDto
{
    public string? License { get; set; }
}

/// <summary>A free license the license page issued, with what it says, for the page to show.</summary>
public partial class IssuedLicenseDto
{
    public string? License { get; set; }

    public string? ProjectName { get; set; }

    /// <summary><c>free</c>.</summary>
    public string? Plan { get; set; }

    /// <summary>Whom the license names: the team that holds it, by its title. Never a person, since the license goes into git.</summary>
    public string? Holder { get; set; }

    public DateTimeOffset IssuedOn { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }

    /// <summary>The team's project the license is for, whose page shows it.</summary>
    public Guid ProjectId { get; set; }
}

/// <summary>A team the signed-in account may get a license for: an active one it accepted, the one it accepted first first.</summary>
public partial class LicenseTeamDto
{
    public Guid Id { get; set; }

    /// <summary>The team's title, or its name when it has none: what the license names as its holder.</summary>
    public string? Title { get; set; }
}

/// <summary>Where a license stands today, as the Licenses page and the projects' Licenses tab say it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LicenseState>))]
public enum LicenseState
{
    /// <summary>In force, with more than a month to go.</summary>
    InForce,
    /// <summary>In force, ending within a month: time to renew it.</summary>
    EndingSoon,
    /// <summary>Ended, within the two weeks bit CLI keeps working after.</summary>
    InGrace,
    Ended
}

/// <summary>A license a team holds, as the Licenses page lists it.</summary>
public partial class LicenseDto
{
    public Guid Id { get; set; }

    public Guid TeamId { get; set; }

    public string? TeamTitle { get; set; }

    /// <summary>The team's project it is for.</summary>
    public Guid ProjectId { get; set; }

    public string? ProjectName { get; set; }

    /// <summary>The project's id in its bit.json, which the license names.</summary>
    public Guid BitProjectId { get; set; }

    /// <summary><c>free</c>.</summary>
    public string? Plan { get; set; }

    public string? FirstCommit { get; set; }

    public DateTimeOffset IssuedOn { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }

    public LicenseState State { get; set; }

    /// <summary>Who got it; null once their account is gone.</summary>
    public string? IssuedBy { get; set; }

    /// <summary>The license itself, for the prompt the page copies again.</summary>
    public string? License { get; set; }
}

/// <summary>What a project's license is today: what the projects' Licenses tab and the AI tools say of it.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProjectLicenseKind>))]
public enum ProjectLicenseKind
{
    /// <summary>bit Fabric doesn't know the project's id yet: its app hasn't reported one and the team got no license for it.</summary>
    Unknown,
    /// <summary>The project's id is known, without a trial or a license.</summary>
    None,
    Trial,
    TrialEnded,
    Licensed,
    LicenseEnded
}

/// <summary>A project's license as the projects' Licenses tab shows it.</summary>
public partial class ProjectLicenseDto
{
    public ProjectLicenseKind Kind { get; set; }

    /// <summary>When the trial or the newest license ends or ended; null for none.</summary>
    public DateTimeOffset? Until { get; set; }

    /// <summary>Where the newest license stands; null without one.</summary>
    public LicenseState? State { get; set; }

    /// <summary>The newest license's id, for the Licenses page; null without one.</summary>
    public Guid? LicenseId { get; set; }

    public string? FirstCommit { get; set; }
}
