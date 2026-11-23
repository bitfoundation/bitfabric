using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

public partial class EmailButton
{
    [Parameter, EditorRequired] public Uri Href { get; set; } = default!;

    [Parameter, EditorRequired] public string Text { get; set; } = default!;

    /// <summary>A short link under the button, for a client that shows the button as something else.</summary>
    [Parameter] public bool WithFallback { get; set; } = true;

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}
