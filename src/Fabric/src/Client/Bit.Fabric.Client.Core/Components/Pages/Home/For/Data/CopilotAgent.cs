using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/github-copilot. Copilot reads AGENTS.md and its own instructions file, the project's skills (it supports
/// .github/skills, .claude/skills and .agents/skills) in VS Code, Visual Studio, JetBrains IDEs, the CLI and on GitHub.com,
/// the skills as custom agents in .github/agents, and the tools VS Code passes on to it. A model of the visitor's own, with
/// its own key, works without a Copilot plan (GH-BYOK). All read on 21 November 2026.
/// </summary>
public static class CopilotAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "GitHub Copilot";
        const string call = "/{0}";

        return new(PageUrls.ForGitHubCopilot, name)
        {
            PageTitle = PageTitle(l, name),
            Description = l[nameof(AppStrings.ForCopilotDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForCopilotLead)]),
            CardLine = l[nameof(AppStrings.ForCopilotCard)],
            CardIcon = BitIconName.Robot,
            ProductUrl = "https://github.com/features/copilot",
            DoesWell = F(l[nameof(AppStrings.ForCopilotDoesWell)], "GH-PLANS"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "VSC-INSTR"),
                new(AgentLineKind.Instructions, ".github/copilot-instructions.md", l[nameof(AppStrings.ForLineCopilotInstructions)], ["VSC-INSTR"]),
                SkillsLine(l, ".agents/skills/", "GH-SKILLS"),
                new(AgentLineKind.Skills, ".github/agents/", l[nameof(AppStrings.ForLineCopilotAgents)], ["VSC-AGENTS"]),
                ToolsLine(l, ".vscode/mcp.json", "VSC-MCP")
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForCopilotSkillLine)], "VSC-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForCopilotStepTitle)], l[nameof(AppStrings.ForCopilotStepText)], ["GH-SKILLS"]),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForCopilotCostValue)], l[nameof(AppStrings.ForCopilotCostText)], ["GH-PLANS"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForCopilotQuestionPlan)], l[nameof(AppStrings.ForCopilotAnswerPlan)], ["GH-PLANS", "GH-BYOK"]),
                new(l[nameof(AppStrings.ForCopilotQuestionModels)], l[nameof(AppStrings.ForCopilotAnswerModels)], ["VSC-MODELS", "GH-BYOK-VSC"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
