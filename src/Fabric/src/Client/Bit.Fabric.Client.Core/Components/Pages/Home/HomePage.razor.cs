using System.Text.Json.Nodes;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co's home page: a hero that leads with ownership, the numbers under it, then seven
/// bands that each answer one doubt and lead to "Get started": where each piece lives, what every project starts with,
/// the comparison, this site's own bill, what happens if bit Fabric stops, how it works and the questions asked first.
/// Everything else every project comes with is on <c>/features</c> (See FeaturesPage), and the home page as it was on <c>/contents</c> (See ContentsPage).
/// </summary>
public partial class HomePage
{
    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        await StartOnboarding();
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        StopWatchingOnboarding();

        await base.DisposeAsync(disposing);
    }

    /// <summary>
    /// The site itself, as JSON-LD: the Organization behind it with the company's details, the
    /// WebSite, bit Fabric as a SoftwareApplication, and this page. Only what the page and the legal pages show: no rating,
    /// review or price, as there are none (a price of 0 would read as the whole thing being free, which the claims table of
    /// bitfabric-internal/.agents/product-marketing.md rules out), and the features are the page's own day-one cards, in the
    /// page's language.
    /// </summary>
    private string BuildSiteJsonLd()
    {
        // The origin, not this page's url: the same nodes are the site's identity under every culture prefix.
        var siteUrl = AppHomeJsonLd.SiteUrlOf(NavigationManager);
        var language = string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name) ? null : CultureInfo.CurrentUICulture.Name;
        var description = Localizer[nameof(AppStrings.DescriptionMetaTagValue)].Value;

        var organization = AppHomeJsonLd.Organization(siteUrl);

        var webSite = new JsonObject
        {
            ["@type"] = "WebSite",
            ["@id"] = $"{siteUrl}/#website",
            ["name"] = "bit Fabric",
            ["url"] = CanonicalUrl,
            ["description"] = description,
            ["publisher"] = new JsonObject { ["@id"] = $"{siteUrl}/#organization" }
        };

        var software = new JsonObject
        {
            ["@type"] = "SoftwareApplication",
            ["@id"] = $"{siteUrl}/#software",
            ["name"] = "bit Fabric",
            ["url"] = CanonicalUrl,
            ["description"] = description,
            ["applicationCategory"] = "DeveloperApplication",
            // Where bit Fabric itself runs while the visitor's AI builds; the apps it makes run on five platforms
            ["operatingSystem"] = "Windows, macOS, Linux",
            ["featureList"] = new JsonArray([.. DayOne.Select(feature => (JsonNode?)JsonValue.Create(feature.Title))]),
            ["publisher"] = new JsonObject { ["@id"] = $"{siteUrl}/#organization" }
        };

        var webPage = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{CanonicalUrl}#webpage",
            ["url"] = CanonicalUrl,
            ["name"] = Localizer[nameof(AppStrings.HomePageTitle)].Value,
            ["description"] = description,
            ["isPartOf"] = new JsonObject { ["@id"] = $"{siteUrl}/#website" },
            ["about"] = new JsonObject { ["@id"] = $"{siteUrl}/#software" }
        };

        if (language is not null)
        {
            webSite["inLanguage"] = language;
            webPage["inLanguage"] = language;
        }

        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new JsonArray(organization, webSite, software, webPage)
        }.ToJsonString();
    }

    /// <summary>The three values of the hero, which the onboarding's first step points at.</summary>
    private (string Icon, string Title)[] Pills =>
    [
        (BitIconName.Lock, Localizer[nameof(AppStrings.AppHomePrivacyFirst)]),
        (BitIconName.Savings, Localizer[nameof(AppStrings.AppHomeCostOptimized)]),
        (BitIconName.ThisPC, Localizer[nameof(AppStrings.AppHomeYouOwnItAll)])
    ];

    /// <summary>Where each piece lives. Every line stays within the say/don't-say table of bitfabric-internal/specs/product.md.</summary>
    private AppHomeTile[] Owns =>
    [
        new(BitIconName.Code, Localizer[nameof(AppStrings.AppHomeCompareYourCode)], Localizer[nameof(AppStrings.AppHomeOwnCodeText)]),
        new(BitIconName.People, Localizer[nameof(AppStrings.AppHomeCompareYourUsers)], Localizer[nameof(AppStrings.AppHomeOwnUsersText)]),
        new(BitIconName.Permissions, Localizer[nameof(AppStrings.AppHomeOwnKeys)], Localizer[nameof(AppStrings.AppHomeOwnKeysText)]),
        new(BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeOwnAI)], Localizer[nameof(AppStrings.AppHomeOwnAIText)])
    ];

    /// <summary>
    /// What every project starts with, as eight cards, each playing the film its id names (See AppHomeFilms). The website's is
    /// the one the onboarding plays. Every other feature, with its own card and film, is on /features.
    /// </summary>
    private AppHomeFeature[] DayOne =>
    [
        new("website", BitIconName.Globe, Localizer[nameof(AppStrings.AppHomeAFastWebsiteSearchEnginesLove)], Localizer[nameof(AppStrings.AppHomePagesThatOpenFastAndAreReadyFor)]),
        new("two-step", BitIconName.Fingerprint, Localizer[nameof(AppStrings.AppHomeDayOneIdentity)], Localizer[nameof(AppStrings.AppHomeDayOneIdentityText)]),
        new("tenants", BitIconName.Org, Localizer[nameof(AppStrings.AppHomeManyCompaniesOneApp)], Localizer[nameof(AppStrings.AppHomeDayOneTenantsText)]),
        new("admin-panel", BitIconName.BarChartVerticalFill, Localizer[nameof(AppStrings.NewProjectSummaryLiveDashboard)], Localizer[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamInThe)]),
        new("focused", BitIconName.Chat, Localizer[nameof(AppStrings.AppHomeDayOneAssistant)], Localizer[nameof(AppStrings.AppHomeDayOneAssistantText)]),
        new("rtl", BitIconName.LocaleLanguage, Localizer[nameof(AppStrings.AppHomeDayOneLanguages)], Localizer[nameof(AppStrings.AppHomeDayOneLanguagesText)]),
        new("offline", BitIconName.CloudDownload, Localizer[nameof(AppStrings.AppHomeWorksOffline)], Localizer[nameof(AppStrings.AppHomeItKeepsWorkingWithoutAConnectionAnd)]),
        new("gdpr", BitIconName.ShieldSolid, Localizer[nameof(AppStrings.AppHomeGDPRFriendly)], Localizer[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)])
    ];

    /// <summary>How it works, in the order of bitfabric-internal/specs/getting-started.md. "Paste one line", never "run a command".</summary>
    private (string Title, string Text)[] HowSteps =>
    [
        (Localizer[nameof(AppStrings.AppHomeHowAnswer)], Localizer[nameof(AppStrings.AppHomeHowAnswerText)]),
        (Localizer[nameof(AppStrings.AppHomeHowPaste)], Localizer[nameof(AppStrings.AppHomeHowPasteText)]),
        (Localizer[nameof(AppStrings.AppHomeHowBuild)], Localizer[nameof(AppStrings.AppHomeHowBuildText)]),
        (Localizer[nameof(AppStrings.AppHomeHowDeploy)], Localizer[nameof(AppStrings.AppHomeHowDeployText)])
    ];

    /// <summary>
    /// What a visitor who doubts it asks first (bitfabric-internal/.agents/product-marketing.md, Objections). The license
    /// after the beta isn't among them: the page says nothing more about it until it is decided.
    /// </summary>
    private (string Question, string Answer)[] Questions =>
    [
        (Localizer[nameof(AppStrings.AppHomeQuestionAISeesCode)],
         Localizer[nameof(AppStrings.AppHomeAnswerAISeesCode)]),
        (Localizer[nameof(AppStrings.AppHomeQuestionDeveloper)],
         Localizer[nameof(AppStrings.AppHomeAnswerDeveloper)]),
        (Localizer[nameof(AppStrings.AppHomeQuestionBuiltOn)],
         Localizer[nameof(AppStrings.AppHomeAnswerBuiltOn), AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads]),
        (Localizer[nameof(AppStrings.AppHomeQuestionMac)],
         Localizer[nameof(AppStrings.AppHomeAnswerMac)])
    ];
}
