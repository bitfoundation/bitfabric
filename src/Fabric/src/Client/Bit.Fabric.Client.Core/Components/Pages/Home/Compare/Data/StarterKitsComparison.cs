using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;
using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/starter-kits. bit Fabric doesn't sell code, so the page compares only what the research holds of the
/// kits (bitfabric-internal/.agents/gtm/research/competitors.md, section 6: ShipFast, Makerkit and supastarter,
/// read on 19 November 2026) and says what bit Fabric adds beyond code in a band of its own, about bit Fabric alone: whether a
/// kit has a design tool, an AI process, deployment or monitoring wasn't researched, so the page claims nothing of it. Their
/// platforms weren't checked either, so that row is left out. Their sales aren't a buyer's concern and stay out.
/// </summary>
public static class StarterKitsComparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l)
    {
        var column = l[nameof(AppStrings.CompareColumnKit)].Value;

        return new(PageUrls.CompareStarterKits, column)
        {
            PageTitle = l[nameof(AppStrings.CompareKitsPageTitle)],
            Description = l[nameof(AppStrings.CompareKitsDescription)],
            Title = l[nameof(AppStrings.CompareKitsTitle)],
            Answer = l[nameof(AppStrings.CompareKitsAnswer)],
            CardLine = l[nameof(AppStrings.CompareKitsCard)],
            CardIcon = BitIconName.Package,
            LeadsToAgents = true,
            TableTitle = l[nameof(AppStrings.CompareSideBySide)],
            Columns = [new(column, "ShipFast, Makerkit, supastarter"), FabricColumn],
            Rows =
            [
                Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareKitsDoesWell)], "KIT-1", "KIT-2", "KIT-3")),
                Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareKitsDayOne)])),
                Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareKitsWhere)])),
                Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareKitsStopPaying)], "KIT-1")),
                Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareKitsCost)], "KIT-1", "KIT-2", "KIT-3")),
                Row(l, CompareTopic.Terms, F(l[nameof(AppStrings.CompareKitsTerms)], "KIT-1", "KIT-2", "KIT-3")),
                Row(l, CompareTopic.Updates, F(l[nameof(AppStrings.CompareKitsUpdates)], "KIT-1", "KIT-2", "KIT-3")),
                Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareKitsAi)], "KIT-2"))
            ],
            SourceLine = l[nameof(AppStrings.CompareKitsSourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
            Extra = (l[nameof(AppStrings.CompareKitsMoreTitle)], l[nameof(AppStrings.CompareKitsMoreIntro)],
            [
                new(BitIconName.Design, l[nameof(AppStrings.CompareKitsMoreDesignTitle)], l[nameof(AppStrings.CompareKitsMoreDesignText)]),
                new(BitIconName.People, l[nameof(AppStrings.CompareKitsMoreTeamTitle)], l[nameof(AppStrings.CompareKitsMoreTeamText)]),
                new(BitIconName.Rocket, l[nameof(AppStrings.CompareKitsMoreDeployTitle)], l[nameof(AppStrings.CompareKitsMoreDeployText)]),
                new(BitIconName.Sync, l[nameof(AppStrings.CompareKitsMoreUpgradesTitle)], l[nameof(AppStrings.CompareKitsMoreUpgradesText)]),
                new(BitIconName.Health, l[nameof(AppStrings.CompareKitsMoreWatchedTitle)], l[nameof(AppStrings.CompareKitsMoreWatchedText)])
            ]),
            Gaps =
            [
                StackGap(l),
                MoreInstallGap(l)
            ],
            Questions =
            [
                new(l[nameof(AppStrings.CompareQuestionIsKit)], l[nameof(AppStrings.CompareAnswerIsKit)], []),
                new(l[nameof(AppStrings.CompareQuestionNewVersions)], l[nameof(AppStrings.CompareAnswerNewVersions)], []),
                WhichAi(l),
                AfterBeta(l)
            ],
            CostSources = CompareShared.CostSources
        };
    }
}
