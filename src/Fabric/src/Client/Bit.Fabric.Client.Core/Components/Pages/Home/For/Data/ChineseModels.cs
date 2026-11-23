using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/chinese-models: DeepSeek, Qwen, Kimi, GLM and MiniMax with the visitor's own key, in GitHub Copilot (its
/// Custom Endpoint takes Chat Completions, Responses and Anthropic Messages, and works without a Copilot plan) or in Claude
/// Code (each maker's own guide). Their key, their promotions, their terms, working natively
/// in their agent; prices as each maker lists them, dated, and never the argument that a cheaper token makes a cheaper
/// project, which is bit Fabric's own case on /cost. Nothing is said of how a maker handles data: the visitor reads its terms.
/// All read on 21 November 2026.
/// </summary>
public static class ChineseModels
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        var name = l[nameof(AppStrings.ForChineseName)].Value;
        const string call = "/{0}";

        return new(PageUrls.ForChineseModels, name)
        {
            PageTitle = l[nameof(AppStrings.ForChinesePageTitle)],
            Description = l[nameof(AppStrings.ForChineseDescription)],
            Title = Title(l, name),
            Lead = l[nameof(AppStrings.ForChineseLead)],
            CardLine = l[nameof(AppStrings.ForChineseCard)],
            CardIcon = BitIconName.Globe,
            DoesWell = F(l[nameof(AppStrings.ForChineseDoesWell)], "DS-CC", "ALI-CC", "KIMI-CC", "ZAI-PLAN", "MM-CC"),
            LinesTitle = l[nameof(AppStrings.ForChineseLinesTitle)],
            LinesIntro = l[nameof(AppStrings.ForChineseLinesIntro)],
            Lines =
            [
                new(AgentLineKind.Agent, "GitHub Copilot", l[nameof(AppStrings.ForChineseLineCopilot)], ["VSC-MODELS", "GH-BYOK"]),
                new(AgentLineKind.Agent, "Copilot CLI", l[nameof(AppStrings.ForChineseLineCopilotCli)], ["GH-CLI-BYOK"]),
                new(AgentLineKind.Agent, "Claude Code", l[nameof(AppStrings.ForChineseLineClaude)], ["DS-CC", "ALI-CC", "KIMI-CC", "ZAI-PLAN", "MM-CC"]),
                new(AgentLineKind.Agent, "OpenRouter", l[nameof(AppStrings.ForChineseLineOpenRouter)], ["OR-MODELS", "GH-BYOK-VSC"])
            ],
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForModelsSkillLine)], "VSC-SKILLS", "CC-SKILLS", "CX-SKILLS"),
            Steps =
            [
                new(l[nameof(AppStrings.ForChineseStepKeyTitle)], l[nameof(AppStrings.ForChineseStepKeyText)], ["OR-MODELS"]),
                new(l[nameof(AppStrings.ForChineseStepAddTitle)], l[nameof(AppStrings.ForChineseStepAddText)], ["VSC-MODELS"]),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForChineseCostValue)], l[nameof(AppStrings.ForChineseCostText)], []),
            Models = (l[nameof(AppStrings.ForChineseModelsTitle)], l[nameof(AppStrings.ForChineseModelsIntro)],
            [
                new("DeepSeek", "DeepSeek", l[nameof(AppStrings.ForChineseDeepSeekValue)], l[nameof(AppStrings.ForChineseDeepSeekText)], ["DS-PRICE"]),
                new("GLM", "Z.ai", l[nameof(AppStrings.ForChineseGlmValue)], l[nameof(AppStrings.ForChineseGlmText)], ["ZAI-PLAN"]),
                new("Qwen", "Alibaba Cloud", null, l[nameof(AppStrings.ForChineseQwenText)], ["ALI-CC"]),
                new("Kimi", "Moonshot AI", null, l[nameof(AppStrings.ForChineseKimiText)], ["KIMI-CC"]),
                new("MiniMax", "MiniMax", null, l[nameof(AppStrings.ForChineseMiniMaxText)], ["MM-CC"])
            ]),
            Questions =
            [
                new(l[nameof(AppStrings.ForChineseQuestionWho)], l[nameof(AppStrings.ForChineseAnswerWho)], []),
                new(l[nameof(AppStrings.ForChineseQuestionPlan)], l[nameof(AppStrings.ForChineseAnswerPlan)], ["GH-BYOK", "GH-BYOK-VSC"]),
                new(l[nameof(AppStrings.ForChineseQuestionMix)], l[nameof(AppStrings.ForChineseAnswerMix)], ["VSC-MODELS"]),
                Switch(l)
            ]
        };
    }
}
