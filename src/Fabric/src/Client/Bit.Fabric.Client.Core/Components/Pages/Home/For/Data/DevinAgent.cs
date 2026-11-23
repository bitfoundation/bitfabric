using static Bit.Fabric.Client.Core.Components.Pages.Home.For.Data.ForShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.For.Data;

/// <summary>
/// bitfabric.co/for/windsurf: windsurf.com and Windsurf's documentation now lead to Devin Desktop (observed 21 November 2026,
/// WS-MOVE), so the page is Devin Desktop's at the address people search for. Its agent reads AGENTS.md and the skills in
/// .agents/skills (and the old .windsurf/skills), callable as slash commands; tools are added on its Customize page, as
/// projects ship no file it reads them from. All read on 21 November 2026.
/// </summary>
public static class DevinAgent
{
    public static AgentPage Build(IStringLocalizer<AppStrings> l)
    {
        const string name = "Devin Desktop";
        const string call = "/{0}";

        return new(PageUrls.ForWindsurf, name)
        {
            PageTitle = l[nameof(AppStrings.ForDevinPageTitle)],
            Description = l[nameof(AppStrings.ForDevinDescription)],
            Title = Title(l, name),
            Lead = Lead(l, name, l[nameof(AppStrings.ForDevinLead)]),
            CardLine = l[nameof(AppStrings.ForDevinCard)],
            CardIcon = BitIconName.LightningBolt,
            ProductUrl = "https://devin.ai/desktop",
            DoesWell = F(l[nameof(AppStrings.ForDevinDoesWell)], "DV-CUSTOM"),
            LinesTitle = ReadsTitle(l, name),
            LinesIntro = ReadsIntro(l, name),
            Lines =
            [
                AgentsMd(l, "DV-CUSTOM"),
                SkillsLine(l, ".agents/skills/", "DV-CUSTOM")
            ],
            ToolsNote = F(l[nameof(AppStrings.ForDevinTools)], "DV-CUSTOM"),
            SkillCall = call,
            SkillLine = F(l[nameof(AppStrings.ForDevinSkillLine)], "DV-CUSTOM"),
            Steps =
            [
                new(l[nameof(AppStrings.ForDevinStepTitle)], l[nameof(AppStrings.ForDevinStepText)], []),
                Ask(l, call)
            ],
            Cost = new(l[nameof(AppStrings.ForDevinCostValue)], l[nameof(AppStrings.ForDevinCostText)], ["DV-USAGE"]),
            Questions =
            [
                new(l[nameof(AppStrings.ForDevinQuestionWindsurf)], l[nameof(AppStrings.ForDevinAnswerWindsurf)], ["WS-MOVE", "DV-CUSTOM"]),
                Switch(l),
                Sees(l)
            ]
        };
    }
}
