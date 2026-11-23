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

    /// <summary>Whom the license names: the account's name, or its email when it has none.</summary>
    public string? Holder { get; set; }

    public DateTimeOffset IssuedOn { get; set; }

    public DateTimeOffset ExpiresOn { get; set; }
}
