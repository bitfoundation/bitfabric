using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Components;

/// <summary>
/// The weekly scans email (See ProjectScansEmailJobRunner), in the frame every email shares (See EmailLayout), such as the team
/// invitation, and in the reader's language, which the job sets before rendering it.
/// </summary>
public partial class ProjectScansEmailTemplate
{
    [Parameter] public ProjectScansEmailModel Model { get; set; } = default!;

    [AutoInject] public IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}

public sealed class ProjectScansEmailModel
{
    public required string DisplayName { get; init; }

    public required string TeamName { get; init; }

    /// <summary>bit Fabric's own address, which its logo and name link to: a job has no request to read it from.</summary>
    public required Uri WebAppUrl { get; init; }

    public required ProjectScansEmailProject[] Projects { get; init; }
}

/// <param name="Name">The project's name.</param>
/// <param name="Host">The app's host name, such as <c>bluebird.app</c>.</param>
/// <param name="ScansLink">The project's Scans tab.</param>
/// <param name="Scans">Each kind's latest run, in the order the tab shows them.</param>
/// <param name="Urgent">What each urgent finding means, in the reader's words.</param>
public sealed record ProjectScansEmailProject(string Name, string Host, Uri ScansLink, ProjectScansEmailScan[] Scans, string[] Urgent);

/// <param name="Kind">The scan's name, such as "Security".</param>
/// <param name="Result">"B+, needs work", or why there is none.</param>
/// <param name="Color">Its verdict's color, written into the email, which reads no style sheet.</param>
public sealed record ProjectScansEmailScan(string Kind, string Result, string Color);
