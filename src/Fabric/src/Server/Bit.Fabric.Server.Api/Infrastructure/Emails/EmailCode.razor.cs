using Bit.Fabric.Server.Api.Features.Identity.Resources;
using Microsoft.AspNetCore.Components;

namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

public partial class EmailCode
{
    [Parameter, EditorRequired] public string Code { get; set; } = default!;

    /// <summary>How long the code works, from the identity settings' lifetime of its kind.</summary>
    [Parameter] public TimeSpan? ValidFor { get; set; }

    [AutoInject] public IStringLocalizer<EmailStrings> EmailLocalizer { get; set; } = default!;

    private string ValidForText(TimeSpan validFor)
    {
        var minutes = (int)Math.Round(validFor.TotalMinutes);

        if (minutes <= 1)
            return EmailLocalizer[nameof(EmailStrings.EmailCodeValidOneMinute)];

        if (minutes < 120)
            return EmailLocalizer[nameof(EmailStrings.EmailCodeValidMinutes), minutes];

        return EmailLocalizer[nameof(EmailStrings.EmailCodeValidHours), (int)Math.Round(validFor.TotalHours)];
    }
}
