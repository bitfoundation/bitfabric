using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/starter-kits. bit Fabric doesn't sell code (bitfabric-internal/.agents/gtm/README.md), so the page
/// compares only what the research holds of the kits (bitfabric-internal/.agents/gtm/research/competitors.md, section 6:
/// ShipFast, Makerkit, supastarter, read on 19 November 2026) and says what bit Fabric adds beyond code in a band of its own,
/// about bit Fabric alone: whether a kit has a design tool, an AI process, deployment, upgrades or monitoring wasn't
/// researched, so the page claims nothing of it. Their platforms weren't checked either, so that row is left out.
/// </summary>
public static class StarterKitsComparison
{
    public const string Key = "starter-kits";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, l["A starter kit"])
    {
        TableTitle = [l["bit Fabric and SaaS starter kits"]],
        Answer = l["A starter kit is the better choice if you want to pay once for code and take it from there. bit Fabric is the better choice if you want the whole way: pages designed first, an AI that tests and reviews, deployment, upgrades and monitoring."],
        FitTitle = [l["A starter kit or bit Fabric?"]],
        FitLead = l["A kit sells you code. bit Fabric doesn't sell code: it's the whole way from an idea to a live app."],
        CardLine = l["Code you buy once, next to the whole way from an idea to a live app"],
        CardIcon = BitIconName.Package,
        Columns = [l["A starter kit"], "bit Fabric"],
        ColumnNotes = [l["ShipFast, Makerkit, supastarter"], null],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["You pay once and get updates for life; ShipFast and Makerkit allow unlimited projects"], "KIT-1", "KIT-2", "KIT-3")),
            Row(l, CompareTopic.DayOne,
                F(l["The kit's code, for you and your AI to build on"])),
            Row(l, CompareTopic.Where,
                F(l["Wherever you deploy it, under the kit's license"], "KIT-1")),
            Row(l, CompareTopic.StopPaying,
                F(l["Nothing stops: you paid once"], "KIT-1")),
            Row(l, CompareTopic.Cost,
                F(l["Once: ShipFast from $199, Makerkit $349 for one user or $649 for five, supastarter from €349 for one seat to €1,499 for ten"], "KIT-1", "KIT-2", "KIT-3")),
            Row(l, CompareTopic.License,
                F(l["Makerkit and supastarter by user or seat. ShipFast and supastarter forbid reselling the code; with Makerkit, a client who receives the code needs a license of their own"], "KIT-1", "KIT-2", "KIT-3")),
            Row(l, CompareTopic.Updates,
                F(l["Updates for life"], "KIT-1", "KIT-2", "KIT-3")),
            Row(l, CompareTopic.Ai,
                F(l["The agent you pick. Makerkit's license allows Copilot or Cursor while you code, and forbids training AI on its code"], "KIT-2"))
        ],
        SourceLine = l["Every fact about the kits comes from their own pages and licenses, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose a starter kit if"],
            [
                l["You'd rather pay once for code you own"],
                l["You're a developer who'll take it from there in your own way"],
                l["You want updates for life in that one price"],
                l["You build many projects and want one price for all of them"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want web, Android, iOS, Windows and macOS from one codebase"],
                l["You want your pages designed on every device before they're built"],
                l["You want your AI to plan, test and review like a team, and deploy to your own server"],
                l["You want new versions brought in without losing your own changes"]
            ])
        ],
        Extra = (l["More than code: the whole way to a live app"],
            l["bit Fabric doesn't sell code. Every project comes with the way from an idea to an app that's live and watched."],
            [
                new(BitIconName.Design, l["Design in the Workbench"], l["Your pages are designed on sample data, on every device, theme, language and data state, before they're built."]),
                new(BitIconName.People, l["An AI that works like a team"], l["It asks, plans, designs, builds, runs the tests and reviews its own work before it says it's done."]),
                new(BitIconName.Rocket, l["Deployment to your own server"], l["Your AI sets up the pipeline that builds your app and deploys it to a server you rent, through GitHub."]),
                new(BitIconName.Sync, l["Upgrades that keep your changes"], l["Your AI brings in each new version, 500+ improvements a month, and keeps what you changed."]),
                new(BitIconName.Health, l["Your live app, watched"], l["Health checks, alerts and a status page, reading only your app's health report, never its data."])
            ]),
        Gaps =
        [
            new(BitIconName.Money, l["No one-time price"], l["Starting is free during the beta. A paid license comes after it, and its terms aren't set yet."]),
            StackGap(l),
            PaymentsGap(l)
        ],
        Questions =
        [
            new(l["Is bit Fabric a starter kit?"],
                l["No. bit Fabric doesn't sell code. Your project is made on your computer and in your own accounts, and what comes with it is the way from an idea to a live app: design, an AI that works like a team, deployment, upgrades and monitoring."]),
            new(l["How do new versions reach code I've changed?"],
                l["Your AI brings each new version in with the template-diff skill, keeping the changes you made, and runs the tests before it says it's done."]),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
