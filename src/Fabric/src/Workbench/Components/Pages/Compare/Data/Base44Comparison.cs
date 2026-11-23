using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/base44. Every fact about Base44 is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.3, with its source id, all read on 19 November 2026. Base44 shows only yearly prices, so they are given as billed
/// yearly. Left out because the research couldn't verify them: its monthly prices, whether Free and Starter can export code,
/// its acquisition price and its revenue. Its security incident is left out: one fixed in a day says nothing about a buyer's
/// choice today.
/// </summary>
public static class Base44Comparison
{
    public const string Key = "base44";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, "Base44")
    {
        TableTitle = [l["bit Fabric and Base44, side by side"]],
        Answer = l["Base44 is the better choice if you want everything your app needs run for you on one plan, from $16 a month billed yearly. bit Fabric is the better choice if you want it all on your own server, on every device."],
        FitTitle = [l["Base44 or bit Fabric?"]],
        FitLead = l["Both get you an app built with AI. They differ in where it lives, what it runs on and who you pay."],
        CardLine = l["An all-in-one plan from $16, next to everything your app needs on your own server"],
        CardIcon = BitIconName.BrowserTab,
        Columns = ["Base44", "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["A database, your users' accounts and file storage run for you on every plan; a custom domain from Starter, its first year free; store files for iOS and Android on Builder"], "B44-1", "B44-4", "B44-5")),
            Row(l, CompareTopic.DayOne,
                F(l["An app built from your prompts, with its database, your users' accounts and file storage run by Base44"], "B44-4")),
            Row(l, CompareTopic.Where,
                F(l["Base44 runs your app, its data and your users' accounts, a service in beta paid from integration credits. A way to run that on your own server wasn't found in its docs"], "B44-4", "B44-6")),
            Row(l, CompareTopic.StopPaying,
                F(l["At your monthly credit limit, the way on is a higher plan; no top-ups are described"], "B44-1")),
            Row(l, CompareTopic.Platforms,
                F(l["Web, and a mobile app of your published app, with files for the stores on Builder. Desktop apps for your users weren't found in its docs"], "B44-5")),
            Row(l, CompareTopic.Cost,
                F(l["Starter $16 a month billed yearly, with 100 message credits; GitHub sync from Builder, $40 a month billed yearly"], "B44-1", "B44-2")),
            Row(l, CompareTopic.Ai,
                F(l["Base44's agent, so your prompts go to Base44"], "B44-7")),
            Row(l, CompareTopic.Production,
                F(l["Publishing is built in, with a custom domain from Starter, its first year free"], "B44-1")),
            Row(l, CompareTopic.VendorStops,
                F(l["Two-way GitHub sync needs Builder or higher. Ejecting makes a new app with your schemas copied and an empty database"], "B44-2", "B44-3"))
        ],
        SourceLine = l["Every fact about Base44 comes from its own pricing, documentation and privacy policy, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose Base44 if"],
            [
                l["You want to start in your browser today, with nothing to install"],
                l["You want your app's database, users' accounts and storage run for you on one plan"],
                l["A low yearly price matters most: Starter is $16 a month, billed yearly"],
                l["You want store files for iOS and Android without leaving the browser"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want your app's database and your users' accounts on your own server, ready to take with you"],
                l["You want apps for Android, iOS, Windows and macOS from the same code"],
                l["You want your code in your own repository on every plan"],
                l["You'd rather pay for a server and your own AI plan than for credits"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Can I move my Base44 app to bit Fabric?"],
                l["Yes, by rebuilding rather than importing. With Base44's GitHub sync your code is in your repository, so your AI can read it, rebuild the features on bit Fabric with the port skill, and then help bring your data over."]),
            Hosting(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
