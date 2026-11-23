using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Features.Identity.Components;

public partial class TwoFactorTokenTemplate
{
    [Parameter] public TwoFactorTokenTemplateModel Model { get; set; } = default!;
    [Parameter] public HttpContext HttpContext { get; set; } = default!;
    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}

public partial class TwoFactorTokenTemplateModel
{
    public required string DisplayName { get; set; }

    public required string Token { get; set; }

    /// <summary>How long the code works, from the identity settings, which the email says.</summary>
    public TimeSpan? ValidFor { get; set; }
}
