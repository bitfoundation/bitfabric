using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/bolt. Every fact about Bolt is from bitfabric-internal/.agents/gtm/research/competitors.md, section
/// 3.2, with its source id, all read on 19 November 2026. Left out because the research couldn't verify them (section 11): Bolt's funding and
/// whether it exports secrets. Training is left out too: Bolt excludes EEA, UK and Swiss accounts from it, and it differs by
/// region (section 9).
/// </summary>
public static class BoltComparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.CompareBolt, "Bolt")
    {
        PageTitle = l[nameof(AppStrings.CompareBoltPageTitle)],
        Description = l[nameof(AppStrings.CompareBoltDescription)],
        Title = l[nameof(AppStrings.CompareBoltTitle)],
        Answer = l[nameof(AppStrings.CompareBoltAnswer)],
        CardLine = l[nameof(AppStrings.CompareBoltCard)],
        CardIcon = BitIconName.BrowserTab,
        ProductUrl = "https://bolt.new",
        TableTitle = l[nameof(AppStrings.CompareSideBySide)],
        Columns = [new("Bolt"), FabricColumn],
        Rows =
        [
            Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareBoltDoesWell)], "BOLT-6", "BOLT-7", "BOLT-2")),
            Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareBoltDayOne)], "BOLT-5")),
            Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareBoltWhere)], "BOLT-4", "BOLT-5")),
            Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareBoltStopPaying)], "BOLT-2", "BOLT-4")),
            Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareBoltPlatforms)], "BOLT-6")),
            Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareBoltCost)], "BOLT-1", "BOLT-2")),
            Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareBoltAi)], "BOLT-2")),
            Row(l, CompareTopic.GoingLive, F(l[nameof(AppStrings.CompareBoltGoingLive)], "BOLT-4", "BOLT-1")),
            Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareBoltLeaving)], "BOLT-7", "BOLT-5"))
        ],
        SourceLine = l[nameof(AppStrings.CompareBoltSourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions = HostedQuestions(l, l[nameof(AppStrings.CompareBoltMoveQuestion)], l[nameof(AppStrings.CompareBoltMoveAnswer)]),
        CostSources = CompareShared.CostSources
    };
}
