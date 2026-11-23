using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/cursor. Cursor reads AGENTS.md, the skills in .agents/skills (as slash commands in Agent chat) and the
/// tools in .cursor/mcp.json. Its plans changed in 2026: Plus has Cursor's own models, Premium and above add other makers'.
/// All read on 21 November 2026.
/// </summary>
public static class CursorAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Cursor";
        const string call = "/{0}";

        return new(PageUrls.ForCursor, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForCursorDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForCursorLead)]),
            CardLine = l[nameof(AppStrings.ForCursorCard)],
            CardIcon = BitIconName.Edit,
            ProductUrl = "https://cursor.com/",
            DoesWell = F(l[nameof(AppStrings.ForCursorDoesWell)], "CU-PRICING"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "CU-RULES"),
                SkillsLine(l, ".agents/skills/", "CU-SKILLS"),
                ToolsLine(l, ".cursor/mcp.json", "CU-MCP")
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForCursorSkillLine)], "CU-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForCursorStepTitle)], l[nameof(AppStrings.ForCursorStepText)], []),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForCursorCostValue)], l[nameof(AppStrings.ForCursorCostText)], ["CU-PRICING"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForCursorQuestionModels)], l[nameof(AppStrings.ForCursorAnswerModels)], ["CU-PRICING"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
