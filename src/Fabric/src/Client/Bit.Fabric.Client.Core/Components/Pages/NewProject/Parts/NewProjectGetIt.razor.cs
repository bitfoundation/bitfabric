using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// The last step: how to open a terminal on the visitor's computer, the one line to paste with what it does, what happens
/// while it runs (<see cref="NewProjectLineSteps"/>), what the computer needs, where another computer or a teammate sets
/// the project up later, and what bitfabric.co keeps. A visitor on a phone gets a way to send the page to their computer first.
/// </summary>
public partial class NewProjectGetIt
{
    [AutoInject] private Clipboard clipboard = default!;

    private bool isLinkCopied;

    [Parameter] public string HeadingElement { get; set; } = "h2";

    private string SubheadingElement => HeadingElement is "h2" ? "h3" : "h4";

    private async Task CopyLink()
    {
        await clipboard.WriteText(Answers.ShareLink);
        isLinkCopied = true;
    }

    private string MailLink
    {
        get
        {
            var answers = Answers;
            var subject = Localizer[nameof(AppStrings.NewProjectMailSubject), answers.DisplayName].Value;
            var body = Localizer[nameof(AppStrings.NewProjectMailBody), answers.DisplayName, answers.ShareLink].Value;

            return $"mailto:?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
        }
    }

    private string ComputerNeed(NewProjectAnswers answers) => answers.Computer switch
    {
        NewProjectComputer.Windows => Localizer[nameof(AppStrings.NewProjectNeedWindows), answers.MemoryGb, answers.DiskGb],
        NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectNeedMac), answers.MemoryGb, answers.DiskGb],
        _ => Localizer[nameof(AppStrings.NewProjectNeedLinux), answers.MemoryGb, answers.DiskGb]
    };

    /// <summary>What the line does, step by step and in plain words, in the order it does it.</summary>
    private IEnumerable<string> PlanSteps(NewProjectAnswers answers)
    {
        yield return Localizer[nameof(AppStrings.NewProjectPlanSdk)];
        yield return Localizer[nameof(AppStrings.NewProjectPlanTools)];

        if (answers.NeedsContainers)
        {
            yield return answers.Computer switch
            {
                NewProjectComputer.Windows => Localizer[nameof(AppStrings.NewProjectPlanPodman)],
                NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectPlanColima)],
                _ => Localizer[nameof(AppStrings.NewProjectPlanDocker)]
            };
        }

        yield return Localizer[nameof(AppStrings.NewProjectPlanCreate), answers.CodeName];

        if (answers.HasGitHubRepo)
            yield return Localizer[nameof(AppStrings.NewProjectPlanGitHub)];

        yield return Localizer[nameof(AppStrings.NewProjectPlanStart)];

        if (answers.Devices.Count > 0)
            yield return Localizer[nameof(AppStrings.NewProjectPlanDevices), ListOf(answers.Devices.Select(DeviceName))];

        yield return Localizer[nameof(AppStrings.NewProjectPlanVsCode)];
        yield return Localizer[nameof(AppStrings.NewProjectPlanFailure)];
    }
}
