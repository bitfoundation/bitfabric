using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Projects.Dtos;

/// <summary>
/// A team's project (bitfabric-internal/specs/monitoring.md, Projects, environments and their people): its card on the
/// projects page, its row on their Licenses tab, and the head of its page.
/// </summary>
public partial class ProjectDto
{
    public Guid Id { get; set; }

    public string? Name { get; set; }

    /// <summary>The id in the project's bit.json, once its app or a license named it.</summary>
    public Guid? BitProjectId { get; set; }

    /// <summary>Its environments, Production first, then by name; each as its card shows it.</summary>
    public List<MonitoringDto> Monitorings { get; set; } = [];

    public ProjectLicenseDto License { get; set; } = new();

    public int MemberCount { get; set; }

    public DateTimeOffset CreatedOn { get; set; }

    public long Version { get; set; }
}

[DtoResourceType(typeof(AppStrings))]
public partial class RenameProjectRequestDto
{
    public Guid Id { get; set; }

    [Required(ErrorMessage = nameof(AppStrings.RequiredAttribute_ValidationError))]
    [MaxLength(100, ErrorMessage = nameof(AppStrings.MaxLengthAttribute_ValidationError))]
    [Display(Name = "Project name")]
    public string? Name { get; set; }
}

/// <summary>Someone of the team, and whether they work on the project.</summary>
public partial class ProjectMemberDto
{
    public Guid UserId { get; set; }

    public string? Name { get; set; }

    /// <summary>A member of the project; otherwise someone of the team who can be added.</summary>
    public bool IsMember { get; set; }

    /// <summary>When they became a member; null for one who isn't.</summary>
    public DateTimeOffset? AddedOn { get; set; }

    /// <summary>The project's environments whose alerts reach them.</summary>
    public int AlertRouteCount { get; set; }
}

public partial class ProjectMemberRequestDto
{
    public Guid ProjectId { get; set; }

    public Guid UserId { get; set; }
}
