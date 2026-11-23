using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare: bit Fabric next to the three ways people build an app with AI today, then a card for each named
/// comparison. A hosted builder's column speaks for the five compared one by one (Lovable, Bolt, Base44, Replit and v0), and
/// says only what holds for them, or names the one it holds for, with its source. Whether a starter kit builds for mobile or
/// the desktop wasn't researched, so its cell says it isn't compared. Which AI builds it and getting to production are
/// left to each comparison, to keep the hub's table short.
/// </summary>
public static class HubComparison
{
    public const string Key = "hub";

    /// <summary>The comparisons the hub's cards lead to, in their order.</summary>
    public static CompareSubject[] Subjects(IStringLocalizer l) =>
    [
        LovableComparison.Build(l),
        BoltComparison.Build(l),
        Base44Comparison.Build(l),
        ReplitComparison.Build(l),
        V0Comparison.Build(l),
        EmptyProjectComparison.Build(l),
        StarterKitsComparison.Build(l)
    ];

    public static CompareSubject Build(IStringLocalizer l) => new(Key, l["Compare"])
    {
        TableTitle = [l["How bit Fabric compares"]],
        Answer = l["A hosted builder is the quickest start in a browser, an empty project keeps everything yours but starts from nothing, and a starter kit sells you code. bit Fabric starts your own AI on a full app, in your own accounts."],
        FitTitle = [l["Four ways to build an app with AI"]],
        FitLead = l["Each is the right choice for someone. Here is who."],
        Columns = [l["A hosted AI builder"], l["Your AI on an empty project"], l["A starter kit"], "bit Fabric"],
        ColumnNotes = ["Lovable, Bolt, Base44, Replit, v0", null, "ShipFast, Makerkit, supastarter", null],
        Rows =
        [
            Hub(l, CompareTopic.DoesWell,
                F(l["Your app, its database and your users' accounts run for you, on one platform"], "LOV-7", "BOLT-5", "REP-7"),
                F(l["Every part is yours, in any stack you pick"]),
                F(l["You pay once and keep updates for life"], "KIT-1", "KIT-2", "KIT-3")),
            Hub(l, CompareTopic.DayOne,
                F(l["What you prompt, running on the platform"], "LOV-7", "BOLT-5"),
                F(l["An empty folder"]),
                F(l["The kit's code, to build on"])),
            Hub(l, CompareTopic.Where,
                F(l["Your code syncs to Git from all five, on Base44 from its $40 plan; your app and its data run on the platform"], "LOV-4", "BOLT-7", "B44-2", "REP-10", "V0-2"),
                F(l["Wherever you and your AI put them"]),
                F(l["Wherever you deploy it"])),
            Hub(l, CompareTopic.StopPaying,
                F(l["On Lovable, at zero credits your app's database, files and users' accounts pause, and the data can't be exported until they run again"], "LOV-3"),
                F(l["Nothing of your app pauses; only the building waits"]),
                F(l["Nothing stops: you paid once"], "KIT-1")),
            Hub(l, CompareTopic.Platforms,
                F(l["Web; Bolt and Replit also ship mobile apps through Expo, and Base44 a mobile app of your web app. Desktop apps for your users weren't found in any of the five's docs"], "BOLT-6", "REP-9", "B44-5"),
                F(l["Each platform is another app to build and test"]),
                F(l["Not compared here"])),
            Hub(l, CompareTopic.Cost,
                F(l["From $16 to $30 a month to start; on Lovable, the same credits pay for running your app"], "B44-1", "V0-2", "LOV-7"),
                F(l["Your AI plan, from $10 a month"], "AG-5"),
                F(l["From $199 to €1,499, once"], "KIT-1", "KIT-3")),
            Hub(l, CompareTopic.VendorStops,
                F(l["Platforms do close: Firebase Studio shuts down on 22 March 2027 and deletes what remains"], "INC-10"),
                F(l["Your code is yours, whatever happens to your AI's maker"]),
                F(l["The code you bought stays with you"]))
        ],
        SourceLine = l["Every fact about a named product comes from its maker's own pages, read on {0}; each comparison below goes into one of them. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose a hosted AI builder if"],
            [
                l["You want to start in a browser tab today, with nothing to install"],
                l["You'd like your app, its database and your users' accounts run for you"],
                l["You're happy for your app to live on the builder's platform"]
            ]),
            new(l["Choose your AI on an empty project if"],
            [
                l["You want to pick every part of the stack yourself"],
                l["You enjoy building the foundations with your AI"],
                l["Your app is small, with no need for roles, tenants or several platforms"]
            ]),
            new(l["Choose a starter kit if"],
            [
                l["You'd rather pay once for code you own"],
                l["You're a developer who'll take it from there in your own way"],
                l["You build many projects and want one price for all of them"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want everything in your own accounts from day one"],
                l["You need web, Android, iOS, Windows and macOS from one codebase"],
                l["You want your AI to start from what every app needs, built and tested"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Which one should I choose?"],
                l["If you want to start in a browser now and don't mind your app living on someone else's platform, a hosted builder. If you want to choose every part and build the foundations yourself, your AI on an empty project. If you want code to build on, a starter kit. If you want all of it in your own accounts and full featured on five platforms from day one, bit Fabric."]),
            new(l["Does bit Fabric replace my AI agent?"],
                l["No. bit Fabric is what your agent starts from. Your agent still does the building, on your computer, guided by the project's own instructions and skills."]),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };

    /// <summary>A row of the hub: a hosted builder, an empty project and a starter kit, then bit Fabric's own fact.</summary>
    private static CompareRow Hub(IStringLocalizer l, CompareTopic topic, CompareFact hosted, CompareFact empty, CompareFact kit)
        => new(topic, [hosted, empty, kit, Ours(l, topic)]);
}
