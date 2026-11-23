using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/antigravity. Antigravity reads AGENTS.md (and GEMINI.md) as rules and the skills in .agents/skills, which
/// it follows by their description rather than by a command. It reads tools from .agents/mcp_config.json, which projects don't
/// ship yet, so the page says where they are added in Antigravity. Its CLI is agy. All read on 21 November 2026.
/// </summary>
public static class AntigravityAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Antigravity";

        return new(PageUrls.ForAntigravity, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForAntigravityDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForAntigravityLead)]),
            CardLine = l[nameof(AppStrings.ForAntigravityCard)],
            CardIcon = BitIconName.Rocket,
            ProductUrl = "https://antigravity.google/",
            DoesWell = F(l[nameof(AppStrings.ForAntigravityDoesWell)], "AGY-PRICING"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "AGY-RULES"),
                SkillsLine(l, ".agents/skills/", "AGY-SKILLS")
            ],
            ToolsNote = F(l[nameof(AppStrings.ForAntigravityTools)], "AGY-MCP"),
            SkillCall = null,
            SkillLine = F(l[nameof(AppStrings.ForAntigravitySkillLine)], "AGY-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForAntigravityStepTitle)], l[nameof(AppStrings.ForAntigravityStepText)], ["AGY-CLI"], "agy"),
                Ask(l, null)
            ],
            Cost = new(l[nameof(AppStrings.ForAntigravityCostValue)], l[nameof(AppStrings.ForAntigravityCostText)], ["AGY-PRICING"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForAntigravityQuestionGemini)], l[nameof(AppStrings.ForAntigravityAnswerGemini)], ["AGY-RULES", "GEM-MOVE"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
