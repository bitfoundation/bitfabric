using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

/// <summary>"Hi Sara," or, with no name known, "Hi,".</summary>
public partial class EmailGreeting
{
    [Parameter] public string? Name { get; set; }

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}
