using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/base44. Every fact about Base44 is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.3, with its source id, all read on 19 November 2026. Base44 shows only yearly prices, so they are given as billed yearly. Left out
/// because the research couldn't verify them (section 11): its monthly prices, whether Free and Starter can export code, its
/// acquisition price and its revenue. Its security incident is left out too: one fixed in a day says nothing about a choice
/// today, and an incident is no argument against a hosted builder (section 9).
/// </summary>
public static class Base44Comparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.CompareBase44, "Base44")
    {
        PageTitle = l[nameof(AppStrings.CompareBase44PageTitle)],
        Description = l[nameof(AppStrings.CompareBase44Description)],
        Title = l[nameof(AppStrings.CompareBase44Title)],
        Answer = l[nameof(AppStrings.CompareBase44Answer)],
        CardLine = l[nameof(AppStrings.CompareBase44Card)],
        CardIcon = BitIconName.BrowserTab,
        ProductUrl = "https://base44.com",
        TableTitle = l[nameof(AppStrings.CompareSideBySide)],
        Columns = [new("Base44"), FabricColumn],
        Rows =
        [
            Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareBase44DoesWell)], "B44-1", "B44-4", "B44-5")),
            Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareBase44DayOne)], "B44-4")),
            Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareBase44Where)], "B44-4", "B44-6")),
            Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareBase44StopPaying)], "B44-1")),
            Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareBase44Platforms)], "B44-5")),
            Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareBase44Cost)], "B44-1", "B44-2")),
            Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareBase44Ai)], "B44-7")),
            Row(l, CompareTopic.GoingLive, F(l[nameof(AppStrings.CompareBase44GoingLive)], "B44-1")),
            Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareBase44Leaving)], "B44-2", "B44-3"))
        ],
        SourceLine = l[nameof(AppStrings.CompareBase44SourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions = HostedQuestions(l, l[nameof(AppStrings.CompareBase44MoveQuestion)], l[nameof(AppStrings.CompareBase44MoveAnswer)]),
        CostSources = CompareShared.CostSources
    };
}
