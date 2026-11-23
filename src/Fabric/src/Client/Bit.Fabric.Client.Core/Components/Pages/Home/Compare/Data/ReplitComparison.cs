using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/replit. Every fact about Replit is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.4, with its source id, all read on 19 November 2026. Its separate development and production databases are said as the strength they
/// are, and the incident that led to them isn't told. Left out because the research couldn't verify them (section 11): Google
/// Play publishing, the plan custom domains need, and Core's yearly price (its two pages disagree). Training is left out: it
/// differs by plan (section 9).
/// </summary>
public static class ReplitComparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.CompareReplit, "Replit")
    {
        PageTitle = l[nameof(AppStrings.CompareReplitPageTitle)],
        Description = l[nameof(AppStrings.CompareReplitDescription)],
        Title = l[nameof(AppStrings.CompareReplitTitle)],
        Answer = l[nameof(AppStrings.CompareReplitAnswer)],
        CardLine = l[nameof(AppStrings.CompareReplitCard)],
        CardIcon = BitIconName.BrowserTab,
        ProductUrl = "https://replit.com",
        TableTitle = l[nameof(AppStrings.CompareSideBySide)],
        Columns = [new("Replit"), FabricColumn],
        Rows =
        [
            Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareReplitDoesWell)], "INC-7", "REP-9", "REP-3")),
            Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareReplitDayOne)], "REP-7", "INC-7")),
            Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareReplitWhere)], "REP-7")),
            Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareReplitStopPaying)], "REP-5", "REP-13", "REP-4")),
            Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareReplitPlatforms)], "REP-9")),
            Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareReplitCost)], "REP-4", "REP-1", "REP-2", "REP-3")),
            Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareReplitAi)], "REP-3")),
            Row(l, CompareTopic.GoingLive, F(l[nameof(AppStrings.CompareReplitGoingLive)], "REP-7", "REP-8")),
            Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareReplitLeaving)], "REP-10", "REP-7"))
        ],
        SourceLine = l[nameof(AppStrings.CompareReplitSourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions = HostedQuestions(l, l[nameof(AppStrings.CompareReplitMoveQuestion)], l[nameof(AppStrings.CompareReplitMoveAnswer)]),
        CostSources = CompareShared.CostSources
    };
}
