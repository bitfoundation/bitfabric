using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Features.Identity.Components;

public partial class OtpTemplate
{
    [Parameter] public OtpTemplateModel Model { get; set; } = default!;
    [Parameter] public HttpContext HttpContext { get; set; } = default!;
    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;
}

public partial class OtpTemplateModel
{
    public string? DisplayName { get; set; }
    public required string Token { get; set; }
    public required Uri Link { get; set; }

    /// <summary>How long the code works, from the identity settings, which the email says.</summary>
    public TimeSpan? ValidFor { get; set; }
}
