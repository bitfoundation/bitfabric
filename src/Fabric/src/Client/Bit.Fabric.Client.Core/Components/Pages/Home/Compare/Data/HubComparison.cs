using static Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data.CompareShared;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// bitfabric.co/compare: bit Fabric next to the three ways people build an app with AI today, then a card for each page. A
/// hosted builder's column speaks for the five compared one by one (Lovable, Bolt, Base44, Replit and v0) and says only what
/// holds for them all, or names the ones it holds for, with their sources: no fact of a builder that isn't among them, such as
/// a platform that closed. Whether a starter kit builds for phones or computers wasn't researched, so its cell says it isn't
/// compared. Which AI builds it and going live are left to each page, to keep the hub's table short.
/// </summary>
public static class HubComparison
{
    /// <summary>The pages the hub's cards lead to, in their order.</summary>
    public static CompareSubject[] Subjects(IStringLocalizer<AppStrings> l) =>
    [
        LovableComparison.Build(l),
        BoltComparison.Build(l),
        Base44Comparison.Build(l),
        ReplitComparison.Build(l),
        V0Comparison.Build(l),
        EmptyProjectComparison.Build(l),
        StarterKitsComparison.Build(l)
    ];

    public static CompareSubject Build(IStringLocalizer<AppStrings> l) => new(PageUrls.Compare, l[nameof(AppStrings.Compare)])
    {
        PageTitle = l[nameof(AppStrings.CompareHubPageTitle)],
        Description = l[nameof(AppStrings.CompareHubDescription)],
        Title = l[nameof(AppStrings.CompareHubTitle)],
        Answer = l[nameof(AppStrings.CompareHubAnswer)],
        TableTitle = l[nameof(AppStrings.CompareHubTableTitle)],
        Columns =
        [
            new(l[nameof(AppStrings.AppHomeCompareHostedBuilder)], "Lovable, Bolt, Base44, Replit, v0"),
            new(l[nameof(AppStrings.AppHomeCompareEmptyProject)]),
            new(l[nameof(AppStrings.CompareColumnKit)], "ShipFast, Makerkit, supastarter"),
            FabricColumn
        ],
        Rows =
        [
            Hub(l, CompareTopic.DoesWell,
                F(l[nameof(AppStrings.CompareHubHostedDoesWell)], "LOV-7", "BOLT-5", "REP-7"),
                F(l[nameof(AppStrings.CompareHubEmptyDoesWell)]),
                F(l[nameof(AppStrings.CompareHubKitDoesWell)], "KIT-1", "KIT-2", "KIT-3")),
            Hub(l, CompareTopic.DayOne,
                F(l[nameof(AppStrings.CompareHubHostedDayOne)], "LOV-7", "BOLT-5"),
                F(l[nameof(AppStrings.CompareHubEmptyDayOne)]),
                F(l[nameof(AppStrings.CompareHubKitDayOne)])),
            Hub(l, CompareTopic.Where,
                F(l[nameof(AppStrings.CompareHubHostedWhere)], "LOV-4", "BOLT-7", "B44-2", "REP-10", "V0-2"),
                F(l[nameof(AppStrings.CompareHubEmptyWhere)]),
                F(l[nameof(AppStrings.CompareHubKitWhere)])),
            Hub(l, CompareTopic.StopPaying,
                F(l[nameof(AppStrings.CompareHubHostedStopPaying)], "LOV-3"),
                F(l[nameof(AppStrings.CompareHubEmptyStopPaying)]),
                F(l[nameof(AppStrings.CompareHubKitStopPaying)], "KIT-1")),
            Hub(l, CompareTopic.Platforms,
                F(l[nameof(AppStrings.CompareHubHostedPlatforms)], "BOLT-6", "REP-9", "B44-5", "LOV-9", "V0-2"),
                F(l[nameof(AppStrings.CompareHubEmptyPlatforms)]),
                F(l[nameof(AppStrings.CompareHubKitPlatforms)])),
            Hub(l, CompareTopic.Cost,
                F(l[nameof(AppStrings.CompareHubHostedCost)], "B44-1", "V0-1", "LOV-7"),
                F(l[nameof(AppStrings.CompareHubEmptyCost)], "AG-5"),
                F(l[nameof(AppStrings.CompareHubKitCost)], "KIT-1", "KIT-3")),
            Hub(l, CompareTopic.Leaving,
                F(l[nameof(AppStrings.CompareHubHostedLeaving)], "LOV-5", "BOLT-5", "B44-3"),
                F(l[nameof(AppStrings.CompareHubEmptyLeaving)]),
                F(l[nameof(AppStrings.CompareHubKitLeaving)]))
        ],
        SourceLine = l[nameof(AppStrings.CompareHubSourceLine), l[nameof(AppStrings.CompareCheckedOnDate)]],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l[nameof(AppStrings.CompareQuestionWhichOne)], l[nameof(AppStrings.CompareAnswerWhichOne)], []),
            ReplaceAgent(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        CostSources = CompareShared.CostSources
    };

    /// <summary>A row of the hub: a hosted builder, an empty project and a starter kit, then bit Fabric's own fact.</summary>
    private static CompareRow Hub(IStringLocalizer<AppStrings> l, CompareTopic topic, CompareFact hosted, CompareFact empty, CompareFact kit)
        => new(topic, [hosted, empty, kit, Ours(l, topic)]);
}
