using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

public partial class DirectionFeatures
{
    /// <summary>The app's home page's eight (HomePage.DayOne), in its words and with its films.</summary>
    private AppHomeFeature[] DayOne =>
    [
        new("website", BitIconName.Globe, AppLocalizer[nameof(AppStrings.AppHomeAFastWebsiteSearchEnginesLove)], AppLocalizer[nameof(AppStrings.AppHomePagesThatOpenFastAndAreReadyFor)]),
        new("two-step", BitIconName.Fingerprint, AppLocalizer[nameof(AppStrings.AppHomeDayOneIdentity)], AppLocalizer[nameof(AppStrings.AppHomeDayOneIdentityText)]),
        new("tenants", BitIconName.Org, AppLocalizer[nameof(AppStrings.AppHomeManyCompaniesOneApp)], AppLocalizer[nameof(AppStrings.AppHomeDayOneTenantsText)]),
        new("admin-panel", BitIconName.BarChartVerticalFill, AppLocalizer[nameof(AppStrings.NewProjectSummaryLiveDashboard)], AppLocalizer[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamInThe)]),
        new("focused", BitIconName.Chat, AppLocalizer[nameof(AppStrings.AppHomeDayOneAssistant)], AppLocalizer[nameof(AppStrings.AppHomeDayOneAssistantText)]),
        new("rtl", BitIconName.LocaleLanguage, AppLocalizer[nameof(AppStrings.AppHomeDayOneLanguages)], AppLocalizer[nameof(AppStrings.AppHomeDayOneLanguagesText)]),
        new("offline", BitIconName.CloudDownload, AppLocalizer[nameof(AppStrings.AppHomeWorksOffline)], AppLocalizer[nameof(AppStrings.AppHomeItKeepsWorkingWithoutAConnectionAnd)]),
        new("gdpr", BitIconName.ShieldSolid, AppLocalizer[nameof(AppStrings.AppHomeGDPRFriendly)], AppLocalizer[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)])
    ];
}
