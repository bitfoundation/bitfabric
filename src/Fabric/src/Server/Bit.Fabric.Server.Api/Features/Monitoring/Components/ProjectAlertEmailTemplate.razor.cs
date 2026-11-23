using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Components;

/// <summary>An alert by email (See ProjectAlerter): what broke or got better, in the route's language, and where to look.</summary>
public partial class ProjectAlertEmailTemplate
{
    [Parameter] public ProjectAlertEmailModel Model { get; set; } = default!;

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}

public sealed class ProjectAlertEmailModel
{
    /// <summary>The alert's title, which is the email's subject too.</summary>
    public required string Title { get; init; }

    public required string Body { get; init; }

    public required string ProjectName { get; init; }

    /// <summary>The project's page, its Incidents tab for an incident.</summary>
    public required Uri Link { get; init; }

    /// <summary>bit Fabric's own address: a job has no request to read it from.</summary>
    public required Uri WebAppUrl { get; init; }
}
