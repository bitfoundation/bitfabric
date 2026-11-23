using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/junie. Junie reads the project's root AGENTS.md (no .junie/AGENTS.md overrides it), the skills in
/// .agents/skills once the project is trusted, and the tools in .junie/mcp/mcp.json; a model of the visitor's own goes in a
/// custom profile with an OpenAI-compatible address. All read on 21 November 2026.
/// </summary>
public static class JunieAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Junie";
        const string call = "/{0}";

        return new(PageUrls.ForJunie, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForJunieDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForJunieLead)]),
            CardLine = l[nameof(AppStrings.ForJunieCard)],
            CardIcon = BitIconName.Puzzle,
            ProductUrl = "https://www.jetbrains.com/junie/",
            DoesWell = F(l[nameof(AppStrings.ForJunieDoesWell)], "JN-SKILLS"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "JN-PLUGIN"),
                new(AgentLineKind.Skills, ".agents/skills/", l[nameof(AppStrings.ForLineSkillsTrusted)], ["JN-SKILLS"]),
                ToolsLine(l, ".junie/mcp/mcp.json", "JN-MCP")
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForJunieSkillLine)], "JN-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForJunieStepTitle)], l[nameof(AppStrings.ForJunieStepText)], []),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForJunieCostValue)], l[nameof(AppStrings.ForJunieCostText)], ["JN-SKILLS"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForJunieQuestionModels)], l[nameof(AppStrings.ForJunieAnswerModels)], ["JN-LLM"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
