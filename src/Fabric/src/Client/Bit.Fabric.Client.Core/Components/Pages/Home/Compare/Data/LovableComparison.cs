using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare/lovable. Every fact about Lovable is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.1, with its source id, all read on 19 November 2026, and none from its section 9: Lovable exports the code (two-way Git sync on every
/// plan) and its export carries the users and their password hashes, so the page says so first, and its case is what pauses
/// and what stays behind (LOV-3, LOV-5). Training is left out, as whether Lovable's default excludes European accounts wasn't
/// verified (section 11), and so are its funding and its reviews.
/// </summary>
public static class LovableComparison
{
    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.CompareLovable, "Lovable")
    {
        PageTitle = l[nameof(AppStrings.CompareLovablePageTitle)],
        Description = l[nameof(AppStrings.CompareLovableDescription)],
        Title = l[nameof(AppStrings.CompareLovableTitle)],
        Answer = l[nameof(AppStrings.CompareLovableAnswer)],
        CardLine = l[nameof(AppStrings.CompareLovableCard)],
        // A plain icon for every hosted builder, nothing that recalls its own mark (bitfabric-internal/.agents/gtm/research/seo-geo.md, section 4.5)
        CardIcon = BitIconName.BrowserTab,
        ProductUrl = "https://lovable.dev",
        TableTitle = l[nameof(AppStrings.CompareSideBySide)],
        Columns = [new("Lovable"), FabricColumn],
        Rows =
        [
            Row(l, CompareTopic.DoesWell, F(l[nameof(AppStrings.CompareLovableDoesWell)], "LOV-1", "LOV-4", "LOV-7")),
            Row(l, CompareTopic.DayOne, F(l[nameof(AppStrings.CompareLovableDayOne)], "LOV-7")),
            Row(l, CompareTopic.Where, F(l[nameof(AppStrings.CompareLovableWhere)], "LOV-7", "LOV-5")),
            Row(l, CompareTopic.StopPaying, F(l[nameof(AppStrings.CompareLovableStopPaying)], "LOV-3")),
            Row(l, CompareTopic.Platforms, F(l[nameof(AppStrings.CompareLovablePlatforms)], "LOV-9")),
            Row(l, CompareTopic.Cost, F(l[nameof(AppStrings.CompareLovableCost)], "LOV-1", "LOV-7", "LOV-14")),
            Row(l, CompareTopic.Ai, F(l[nameof(AppStrings.CompareLovableAi)], "LOV-14", "LOV-5")),
            Row(l, CompareTopic.GoingLive, F(l[nameof(AppStrings.CompareLovableGoingLive)], "LOV-1", "LOV-14")),
            Row(l, CompareTopic.Leaving, F(l[nameof(AppStrings.CompareLovableLeaving)], "LOV-4", "LOV-5"))
        ],
        SourceLine = l[nameof(AppStrings.CompareLovableSourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions = HostedQuestions(l, l[nameof(AppStrings.CompareLovableMoveQuestion)], l[nameof(AppStrings.CompareLovableMoveAnswer)]),
        CostSources = CompareShared.CostSources
    };
}
