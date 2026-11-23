using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/v0. Every fact about v0 is from bitfabric-internal/.agents/gtm/research/competitors.md, section 3.5,
/// with its source id, all read on 19 November 2026. v0 exports its code and lets you deploy it elsewhere, so the page says so first. Training is
/// left out: Vercel's own two pages disagree on its default (section 11). What happens to a deployed app when credits run out
/// isn't stated by v0, so the page doesn't say it.
/// </summary>
public static class V0Comparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.CompareV0, "v0")
    {
        PageTitle = l[nameof(AppStrings.CompareV0PageTitle)],
        Description = l[nameof(AppStrings.CompareV0Description)],
        Title = l[nameof(AppStrings.CompareV0Title)],
        Answer = l[nameof(AppStrings.CompareV0Answer)],
        CardLine = l[nameof(AppStrings.CompareV0Card)],
        CardIcon = BitIconName.BrowserTab,
        ProductUrl = "https://v0.app",
        TableTitle = l[nameof(AppStrings.CompareSideBySide)],
        Columns = [new("v0"), FabricColumn],
        Rows =
        [
            Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareV0DoesWell)], "V0-2")),
            Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareV0DayOne)], "V0-2")),
            Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareV0Where)], "V0-2")),
            Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareV0StopPaying)], "V0-2")),
            Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareV0Platforms)], "V0-2")),
            Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareV0Cost)], "V0-1", "V0-2")),
            Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareV0Ai)], "V0-1")),
            Row(l, CompareTopic.GoingLive, F(l[nameof(AppStrings.CompareV0GoingLive)], "V0-2")),
            Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareV0Leaving)], "V0-2"))
        ],
        SourceLine = l[nameof(AppStrings.CompareV0SourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions = HostedQuestions(l, l[nameof(AppStrings.CompareV0MoveQuestion)], l[nameof(AppStrings.CompareV0MoveAnswer)]),
        CostSources = CompareShared.CostSources
    };
}
