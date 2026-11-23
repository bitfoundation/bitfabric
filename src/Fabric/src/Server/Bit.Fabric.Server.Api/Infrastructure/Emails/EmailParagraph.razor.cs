using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

/// <summary>A paragraph of an email's card, in its color for light and dark mode.</summary>
public partial class EmailParagraph
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
