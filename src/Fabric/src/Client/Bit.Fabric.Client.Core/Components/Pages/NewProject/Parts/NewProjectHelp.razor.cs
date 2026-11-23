using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// The side panel behind every "Help me choose": what the question means in plain words, our advice, the prompt that changes
/// the answer later, the live demos and the common questions. Its content is written for someone who has never set up a
/// project: what the choice means for their product, never how the template implements it.
/// </summary>
public partial class NewProjectHelp
{
    private sealed record HelpContent(string Title,
                                      string? Lead,
                                      string? Advice,
                                      string? Later,
                                      IReadOnlyList<(string Text, string Url)> Demos,
                                      IReadOnlyList<(string Question, string Answer)> Faq);

    private void Close(bool isOpen)
    {
        if (isOpen is false)
        {
            Store.OpenHelp(null);
        }
    }

    private HelpContent? Content(string? topic) => topic switch
    {
        NewProjectHelpTopics.Name => new(
            Localizer[nameof(AppStrings.NewProjectHelpNameTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpNameLead)],
            Localizer[nameof(AppStrings.NewProjectHelpNameAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpNameLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqRename)], Localizer[nameof(AppStrings.NewProjectFaqRenameAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqEnglishLetters)], Localizer[nameof(AppStrings.NewProjectFaqEnglishLettersAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqKeepName)], Localizer[nameof(AppStrings.NewProjectFaqKeepNameAnswer)])
            ]),

        NewProjectHelpTopics.Goal => new(
            Localizer[nameof(AppStrings.NewProjectHelpGoalTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpGoalLead)],
            Localizer[nameof(AppStrings.NewProjectHelpGoalAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpGoalLater)],
            [(Localizer[nameof(AppStrings.NewProjectDemoSamplePages)], "https://todo.bitplatform.dev/todo")],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqTryToReal)], Localizer[nameof(AppStrings.NewProjectFaqTryToRealAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqRealInstallsMore)], Localizer[nameof(AppStrings.NewProjectFaqRealInstallsMoreAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqGoalCost)], Localizer[nameof(AppStrings.NewProjectFaqGoalCostAnswer)])
            ]),

        NewProjectHelpTopics.Start => new(
            Localizer[nameof(AppStrings.NewProjectHelpStartTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpStartLead)],
            Localizer[nameof(AppStrings.NewProjectHelpStartAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpStartLater)],
            [(Localizer[nameof(AppStrings.NewProjectDemoAdminPanel)], "https://adminpanel.bitplatform.dev"), (Localizer[nameof(AppStrings.NewProjectDemoStore)], "https://sales.bitplatform.dev")],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqAdminPanel)], Localizer[nameof(AppStrings.NewProjectFaqAdminPanelAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqStore)], Localizer[nameof(AppStrings.NewProjectStartStoreText)]),
                (Localizer[nameof(AppStrings.NewProjectFaqBoth)], Localizer[nameof(AppStrings.NewProjectFaqBothAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqCodeMine)], Localizer[nameof(AppStrings.NewProjectFaqCodeMineAnswer)])
            ]),

        NewProjectHelpTopics.Customers => new(
            Localizer[nameof(AppStrings.NewProjectCustomersMany)],
            Localizer[nameof(AppStrings.NewProjectHelpCustomersLead)],
            Localizer[nameof(AppStrings.NewProjectHelpCustomersAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpCustomersLater)],
            [(Localizer[nameof(AppStrings.NewProjectDemoTenants)], "https://adminpanel.bitplatform.dev")],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqTryDemo)], Localizer[nameof(AppStrings.NewProjectFaqTryDemoAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqDataApart)], Localizer[nameof(AppStrings.NewProjectFaqDataApartAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqCustomersLater)], Localizer[nameof(AppStrings.NewProjectFaqCustomersLaterAnswer)])
            ]),

        NewProjectHelpTopics.Features => new(
            Localizer[nameof(AppStrings.NewProjectHelpFeaturesTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpFeaturesLead)],
            Localizer[nameof(AppStrings.NewProjectHelpFeaturesAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpFeaturesLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqAssistantCost)], Localizer[nameof(AppStrings.NewProjectFaqAssistantCostAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqAssistantNotBuilder)], Localizer[nameof(AppStrings.NewProjectFaqAssistantNotBuilderAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqPushCost)], Localizer[nameof(AppStrings.NewProjectFaqPushCostAnswer)])
            ]),

        NewProjectHelpTopics.Errors => new(
            Localizer[nameof(AppStrings.NewProjectHelpErrorsTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpErrorsLead)],
            Localizer[nameof(AppStrings.NewProjectHelpErrorsAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpErrorsLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqErrorsCost)], Localizer[nameof(AppStrings.NewProjectFaqErrorsCostAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqErrorsAccount)], Localizer[nameof(AppStrings.NewProjectFaqErrorsAccountAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqErrorsSeen)], Localizer[nameof(AppStrings.NewProjectFaqErrorsSeenAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqErrorsLater)], Localizer[nameof(AppStrings.NewProjectFaqErrorsLaterAnswer)])
            ]),

        NewProjectHelpTopics.Devices => new(
            Localizer[nameof(AppStrings.NewProjectHelpDevicesTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpDevicesLead)],
            Localizer[nameof(AppStrings.NewProjectHelpDevicesAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpDevicesLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqPhone)], Localizer[nameof(AppStrings.NewProjectFaqPhoneAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqLoseApps)], Localizer[nameof(AppStrings.NewProjectFaqLoseAppsAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqIphoneOnWindows)], Localizer[nameof(AppStrings.NewProjectFaqIphoneOnWindowsAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqAndroidNeeds)], Localizer[nameof(AppStrings.NewProjectFaqAndroidNeedsAnswer)])
            ]),

        NewProjectHelpTopics.Database => new(
            Localizer[nameof(AppStrings.NewProjectHelpDatabaseTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpDatabaseLead)],
            Localizer[nameof(AppStrings.NewProjectHelpDatabaseAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpDatabaseLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqInstallDatabase)], Localizer[nameof(AppStrings.NewProjectFaqInstallDatabaseAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqSqlServer)], Localizer[nameof(AppStrings.NewProjectFaqSqlServerAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqMySql)], Localizer[nameof(AppStrings.NewProjectMySqlLicense)])
            ]),

        NewProjectHelpTopics.GitHub => new(
            Localizer[nameof(AppStrings.NewProjectHelpGitHubTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpGitHubLead)],
            Localizer[nameof(AppStrings.NewProjectHelpGitHubAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpGitHubLater)],
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqWhoSeesCode)], Localizer[nameof(AppStrings.NewProjectFaqWhoSeesCodeAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqGitLab)], Localizer[nameof(AppStrings.NewProjectFaqGitLabAnswer)])
            ]),

        NewProjectHelpTopics.Offline => new(
            Localizer[nameof(AppStrings.NewProjectHelpOfflineTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpOfflineLead)],
            Localizer[nameof(AppStrings.NewProjectHelpOfflineAdvice)],
            Localizer[nameof(AppStrings.NewProjectHelpOfflineLater)],
            [(Localizer[nameof(AppStrings.NewProjectDemoOffline)], "https://todo-offline.bitplatform.cc")],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqOfflineSync)], Localizer[nameof(AppStrings.NewProjectFaqOfflineSyncAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqOfflineSize)], Localizer[nameof(AppStrings.NewProjectFaqOfflineSizeAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqOfflineLater)], Localizer[nameof(AppStrings.NewProjectFaqOfflineLaterAnswer)])
            ]),

        NewProjectHelpTopics.Line => new(
            Localizer[nameof(AppStrings.NewProjectHelpLineTitle)],
            Localizer[nameof(AppStrings.NewProjectHelpLineLead)],
            null,
            null,
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqLineSafe)], Localizer[nameof(AppStrings.NewProjectFaqLineSafeAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqLineChanges)], Localizer[nameof(AppStrings.NewProjectFaqLineChangesAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqLineAdmin)], Localizer[nameof(AppStrings.NewProjectFaqLineAdminAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqLineFails)], Localizer[nameof(AppStrings.NewProjectFaqLineFailsAnswer)])
            ]),

        NewProjectHelpTopics.General => new(
            Localizer[nameof(AppStrings.NewProjectHelpGeneralTitle)],
            null,
            null,
            null,
            [],
            [
                (Localizer[nameof(AppStrings.NewProjectFaqCode)], Localizer[nameof(AppStrings.NewProjectFaqCodeAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqAccount)], Localizer[nameof(AppStrings.NewProjectFaqAccountAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqCost)], Localizer[nameof(AppStrings.NewProjectFaqCostAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqReady)], Localizer[nameof(AppStrings.NewProjectFaqReadyAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqWhatWeSee)], Localizer[nameof(AppStrings.NewProjectFaqWhatWeSeeAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqDisappears)], Localizer[nameof(AppStrings.NewProjectFaqDisappearsAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqComputer)], Localizer[nameof(AppStrings.NewProjectFaqComputerAnswer)]),
                (Localizer[nameof(AppStrings.NewProjectFaqOnPhone)], Localizer[nameof(AppStrings.NewProjectFaqOnPhoneAnswer)])
            ]),

        _ => null
    };
}
