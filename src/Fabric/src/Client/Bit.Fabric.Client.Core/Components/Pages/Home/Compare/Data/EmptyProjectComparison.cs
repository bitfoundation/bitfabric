using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/empty-project: the visitor's own AI agent on an empty project. bit Fabric doesn't replace the agent, it
/// is what the agent starts from, so the page says so first. The AI plans' prices are from
/// bitfabric-internal/.agents/gtm/research/competitors.md, sections 1b and 4, and the leak figure from its section 7 (UpGuard,
/// which left out apps on builders' domains, so it speaks of AI-built apps and is no argument against hosted builders), all
/// read on 19 November 2026. The AI plans that changed in 2026 (Copilot's billing, Gemini CLI) are left out of the table: they
/// change under bit Fabric's customers too, so they set nothing apart. The tokens question's answer is bit Fabric's own case:
/// a task looks like more tokens, but across a real product the plan, the spec, the Workbench's data states, the review
/// and the tests cut the back-and-forth, so the project costs less. That many people build on Claude's $20 plan rests on
/// no source; the plan's price is AG-1.
/// </summary>
public static class EmptyProjectComparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l)
    {
        var column = l[nameof(AppStrings.AppHomeCompareEmptyProject)].Value;

        return new(PageUrls.CompareEmptyProject, column)
        {
            PageTitle = l[nameof(AppStrings.CompareEmptyPageTitle)],
            Description = l[nameof(AppStrings.CompareEmptyDescription)],
            Title = l[nameof(AppStrings.CompareEmptyTitle)],
            Answer = l[nameof(AppStrings.CompareEmptyAnswer)],
            CardLine = l[nameof(AppStrings.CompareEmptyCard)],
            CardIcon = BitIconName.NewFolder,
            LeadsToAgents = true,
            TableTitle = l[nameof(AppStrings.CompareSideBySide)],
            Columns = [new(column), FabricColumn],
            Rows =
            [
                Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareEmptyDoesWell)])),
                Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareEmptyDayOne)])),
                Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareEmptyWhere)])),
                Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareEmptyStopPaying)]), F(l[nameof(AppStrings.CompareEmptyStopPayingOurs)])),
                Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareEmptyPlatforms)])),
                Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareEmptyCost)], "AG-5", "AG-1", "AG-8")),
                Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareEmptyAi)]), F(l[nameof(AppStrings.CompareEmptyAiOurs)])),
                Row(l, CompareTopic.Security, F(l[nameof(AppStrings.CompareEmptySecurity)], "INC-4")),
                Row(l, CompareTopic.Updates, F(l[nameof(AppStrings.CompareEmptyUpdates)])),
                Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareEmptyLeaving)]))
            ],
            SourceLine = l[nameof(AppStrings.CompareEmptySourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
            Gaps =
            [
                StackGap(l),
                MoreInstallGap(l)
            ],
            Questions =
            [
                ReplaceAgent(l),
                new(l[nameof(AppStrings.CompareQuestionChangeAnything)], l[nameof(AppStrings.CompareAnswerChangeAnything)], []),
                new(l[nameof(AppStrings.CompareQuestionTokens)], l[nameof(AppStrings.CompareAnswerTokens)], ["AG-1"]),
                WhichAi(l),
                AfterBeta(l)
            ],
            CostSources = CompareShared.CostSources
        };
    }
}
