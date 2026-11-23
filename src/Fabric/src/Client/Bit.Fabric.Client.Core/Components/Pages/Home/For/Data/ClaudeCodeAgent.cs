using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/claude-code. Claude Code reads CLAUDE.md (which brings in AGENTS.md), the skills in .claude/skills as
/// slash commands, the tools in .mcp.json and the project's settings in .claude/settings.json. Other models' makers each
/// publish how to run it on theirs, and Ollama on a local model. All read on 21 November 2026.
/// </summary>
public static class ClaudeCodeAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Claude Code";
        const string call = "/{0}";

        return new(PageUrls.ForClaudeCode, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForClaudeDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForClaudeLead)]),
            CardLine = l[nameof(AppStrings.ForClaudeCard)],
            CardIcon = BitIconName.ChatBot,
            ProductUrl = "https://claude.com/product/claude-code",
            DoesWell = F(l[nameof(AppStrings.ForClaudeDoesWell)], "CC-DOCS"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                new(AgentLineKind.Instructions, "CLAUDE.md", l[nameof(AppStrings.ForLineClaudeMd)], ["CC-MEM"]),
                new(AgentLineKind.Skills, ".claude/skills/", l[nameof(AppStrings.ForLineClaudeSkills)], ["CC-SKILLS"]),
                ToolsLine(l, ".mcp.json", "CC-MCP"),
                new(AgentLineKind.Tools, ".claude/settings.json", l[nameof(AppStrings.ForLineClaudeSettings)], ["CC-DIR"])
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForClaudeSkillLine)], "CC-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForClaudeStepTitle)], l[nameof(AppStrings.ForClaudeStepText)], [], "claude"),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForClaudeCostValue)], l[nameof(AppStrings.ForClaudeCostText)], ["CL-PRICING"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForClaudeQuestionModels)], l[nameof(AppStrings.ForClaudeAnswerModels)], ["DS-CC", "KIMI-CC", "ALI-CC", "ZAI-PLAN", "MM-CC", "OL-CC"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
