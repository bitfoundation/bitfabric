using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/local-models: a model on the visitor's own computer, through Ollama or LM Studio, in the agents that take
/// one. It is the visitor's choice, said as one (what a project costs is tokens per task
/// and per project, bit Fabric's own case on /cost, never a cheaper token). It never suggests buying a computer.
/// All read on 21 November 2026.
/// </summary>
public static class LocalModels
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        var name = l[nameof(AppStrings.ForLocalName)].Value;
        const string call = "/{0}";

        return new(PageUrls.ForLocalModels, name)
        {
            PageTitle = l[nameof(AppStrings.ForLocalPageTitle)],
            Description = l[nameof(AppStrings.ForLocalDescription)],
            Title = Title(l, name),
            Lead = l[nameof(AppStrings.ForLocalLead)],
            CardLine = l[nameof(AppStrings.ForLocalCard)],
            CardIcon = BitIconName.ThisPC,
            DoesWell = F(l[nameof(AppStrings.ForLocalDoesWell)], "OL-CC", "OL-CX", "VSC-MODELS"),
            LinesTitle = l[nameof(AppStrings.ForLocalLinesTitle)],
            LinesIntro = l[nameof(AppStrings.ForLocalLinesIntro)],
            Lines =
            [
                new(AgentLineKind.Agent, "GitHub Copilot", l[nameof(AppStrings.ForLocalLineCopilot)], ["VSC-MODELS", "LM-OAI", "GH-BYOK"]),
                new(AgentLineKind.Agent, "Copilot CLI", l[nameof(AppStrings.ForLocalLineCopilotCli)], ["GH-CLI-BYOK"]),
                new(AgentLineKind.Agent, "Claude Code", l[nameof(AppStrings.ForLocalLineClaude)], ["OL-CC"], "ollama launch claude"),
                new(AgentLineKind.Agent, "Codex", l[nameof(AppStrings.ForLocalLineCodex)], ["OL-CX"], "ollama launch codex")
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForModelsSkillLine)], "VSC-SKILLS", "CC-SKILLS", "CX-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForLocalStepInstallTitle)], l[nameof(AppStrings.ForLocalStepInstallText)], ["OL-CX"]),
                new(l[nameof(AppStrings.ForLocalStepStartTitle)], l[nameof(AppStrings.ForLocalStepStartText)], ["OL-CC"], "ollama launch claude"),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForLocalCostValue)], l[nameof(AppStrings.ForLocalCostText)], []),
            Questions =
            [
                new(l[nameof(AppStrings.ForLocalQuestionPrivate)], l[nameof(AppStrings.ForLocalAnswerPrivate)], []),
                new(l[nameof(AppStrings.ForLocalQuestionPlan)], l[nameof(AppStrings.ForLocalAnswerPlan)], ["GH-BYOK"]),
                Switch(l)
            ]
        };
    }
}
