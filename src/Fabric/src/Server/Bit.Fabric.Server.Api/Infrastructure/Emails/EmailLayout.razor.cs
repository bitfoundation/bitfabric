using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

public partial class EmailLayout
{
    // Keeps the inbox's preview to the preheader, rather than the email's next lines after it.
    private static readonly string PreheaderFiller = string.Concat(Enumerable.Repeat("&#847;&zwnj;&nbsp;", 60));

    /// <summary>The document's title, the email's subject.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    /// <summary>The line an inbox shows after the subject; never shown in the email itself.</summary>
    [Parameter, EditorRequired] public string Preheader { get; set; } = default!;

    [Parameter, EditorRequired] public string Heading { get; set; } = default!;

    /// <summary>bit Fabric's own address, which the logo, the footer and the logo's image come from.</summary>
    [Parameter, EditorRequired] public Uri WebAppUrl { get; set; } = default!;

    /// <summary>Why the reader got this email, under the card.</summary>
    [Parameter] public string? Why { get; set; }

    [Parameter] public RenderFragment? ChildContent { get; set; }

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;

    /// <summary>
    /// The web app's logo, 94 by 63 pixels, shown at 36 by 24. The web app serves it, under a path base too; the api's own
    /// images/icon.png, which the emails showed before, isn't served there (bitfabric.co/images/icon.png redirects to not-found).
    /// </summary>
    private string LogoUrl => $"{WebAppUrl.AbsoluteUri.TrimEnd('/')}/images/icons/bit-logo.png";
}
