using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/codex. Codex reads AGENTS.md, the skills in .agents/skills (called with $) and, once the project is
/// trusted, the tools in .codex/config.toml. Ollama starts it on a local model. All read on 21 November 2026.
/// </summary>
public static class CodexAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Codex";
        const string call = "${0}";

        return new(PageUrls.ForCodex, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForCodexDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForCodexLead)]),
            CardLine = l[nameof(AppStrings.ForCodexCard)],
            CardIcon = BitIconName.Code,
            ProductUrl = "https://openai.com/codex/",
            DoesWell = F(l[nameof(AppStrings.ForCodexDoesWell)], "CX-PRICING"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "CX-AGENTS"),
                SkillsLine(l, ".agents/skills/", "CX-SKILLS"),
                new(AgentLineKind.Tools, ".codex/config.toml", l[nameof(AppStrings.ForLineCodexConfig)], ["CX-CONFIG"])
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForCodexSkillLine)], "CX-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForCodexStepTitle)], l[nameof(AppStrings.ForCodexStepText)], ["CX-CONFIG"], "codex"),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForCodexCostValue)], l[nameof(AppStrings.ForCodexCostText)], ["CX-PRICING"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForCodexQuestionLocal)], l[nameof(AppStrings.ForCodexAnswerLocal)], ["OL-CX"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
