using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// What bitfabric.co itself pays to run in Production, each month. The prices are written here and updated by hand: the
/// server, the messages, OpenAI and the storage as of 2026-11-09. Used as it is by the app and by its
/// design in the Workbench.
/// </summary>
public partial class AppHomeCostCalculator
{
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    private readonly HashSet<string> changed = [];

    [Parameter] public string HeadingElement { get; set; } = "h4";

    private AppHomeBillItem[] Paid => PaidOf(Localizer);

    private (string Title, string? Text)[] Free => FreeOf(Localizer);

    /// <summary>
    /// The bill, item by item, which /pricing.md writes out as text from here rather than from a copy.
    /// The items on to begin with add up to what this site pays each month.
    /// </summary>
    public static AppHomeBillItem[] PaidOf(IStringLocalizer<AppStrings> localizer) =>
    [
        new("server", "Linux VM", localizer[nameof(AppStrings.AppHomeCostServerText)], 4, 100) { IsRequired = true, Spec = localizer[nameof(AppStrings.AppHomeCostServerSpec)] },
        new("messages", localizer[nameof(AppStrings.AppHomeCostMessages)], localizer[nameof(AppStrings.AppHomeCostMessagesText)], 1, 45) { IsOptional = true },
        new("ai", "OpenAI", localizer[nameof(AppStrings.AppHomeCostAiText)], 3, 80) { IsOptional = true },
        new("storage", localizer[nameof(AppStrings.AppHomeCostStorage)], localizer[nameof(AppStrings.AppHomeCostStorageText)], 1, 35) { IsOptional = true },
        new("domain", localizer[nameof(AppStrings.AppHomeCostDomain)], localizer[nameof(AppStrings.AppHomeCostDomainText)], 2, 60),
        new("stores", localizer[nameof(AppStrings.AppHomeCostStores)], localizer[nameof(AppStrings.AppHomeCostStoresText)], 8, 25, StartsOn: false)
    ];

    /// <summary>Whatever a visitor would expect to pay for and doesn't. Only what needs saying has a line.</summary>
    public static (string Title, string? Text)[] FreeOf(IStringLocalizer<AppStrings> localizer) =>
    [
        ("Cloudflare", localizer[nameof(AppStrings.AppHomeCostCloudflareText)]),
        ("GitHub", localizer[nameof(AppStrings.AppHomeCostGitHubText)]),
        (localizer[nameof(AppStrings.AppHomeTelemetry)], localizer[nameof(AppStrings.AppHomeCostTelemetryText)]),
        (localizer[nameof(AppStrings.NewProjectPush)], null),
        (localizer[nameof(AppStrings.AppHomeCostHttps)], null),
        (localizer[nameof(AppStrings.AppHomeCostDashboardAccess)], null),
        (localizer[nameof(AppStrings.AppHomeCostSignIn)], localizer[nameof(AppStrings.AppHomeCostSignInText)])
    ];

    private int Total => Paid.Where(IsOn).Sum(item => item.Euros);

    private bool IsOn(AppHomeBillItem item) => item.StartsOn != changed.Contains(item.Id);

    private void Set(AppHomeBillItem item, bool on)
    {
        if (on == item.StartsOn)
        {
            changed.Remove(item.Id);
        }
        else
        {
            changed.Add(item.Id);
        }
    }
}

/// <summary>A line of this site's bill: what it is, what it's for and what it costs each month, in euros.</summary>
public sealed record AppHomeBillItem(string Id, string Title, string Text, int Euros, int Shade, bool StartsOn = true)
{
    /// <summary>Nothing runs without it, so it can't come off the bill.</summary>
    public bool IsRequired { get; init; }

    public bool IsOptional { get; init; }

    public string? Spec { get; init; }
}
