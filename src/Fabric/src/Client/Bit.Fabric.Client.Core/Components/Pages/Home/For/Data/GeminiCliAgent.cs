using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/gemini-cli. Gemini CLI reads GEMINI.md (which brings in AGENTS.md), the skills in .agents/skills, a
/// command for each in .gemini/commands and the tools in .gemini/settings.json. Since 18 June 2026 it serves Gemini Code
/// Assist licenses and paid API keys, no longer Google AI Pro, Ultra or free personal use (GEM-MOVE). All read on 21 November
/// 2026.
/// </summary>
public static class GeminiCliAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Gemini CLI";
        const string call = "/{0}";

        return new(PageUrls.ForGeminiCli, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForGeminiDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForGeminiLead)]),
            CardLine = l[nameof(AppStrings.ForGeminiCard)],
            CardIcon = BitIconName.Flow,
            ProductUrl = "https://geminicli.com/",
            DoesWell = F(l[nameof(AppStrings.ForGeminiDoesWell)], "GEM-MOVE"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                new(AgentLineKind.Instructions, "GEMINI.md", l[nameof(AppStrings.ForLineGeminiMd)], ["GEM-MD"]),
                SkillsLine(l, ".agents/skills/", "GEM-SKILLS"),
                new(AgentLineKind.Skills, ".gemini/commands/", l[nameof(AppStrings.ForLineGeminiCommands)], ["GEM-CMD"]),
                ToolsLine(l, ".gemini/settings.json", "GEM-MCP")
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForGeminiSkillLine)], "GEM-CMD"),
            Steps =
            [
                new(l[nameof(AppStrings.ForGeminiStepTitle)], l[nameof(AppStrings.ForGeminiStepText)], [], "gemini"),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForGeminiCostValue)], l[nameof(AppStrings.ForGeminiCostText)], ["GEM-MOVE"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForGeminiQuestionAccount)], l[nameof(AppStrings.ForGeminiAnswerAccount)], ["GEM-MOVE"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
