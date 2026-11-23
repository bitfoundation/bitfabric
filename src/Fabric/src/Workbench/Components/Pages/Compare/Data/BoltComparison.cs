using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/bolt. Every fact about Bolt is from bitfabric-internal/.agents/gtm/research/competitors.md, section
/// 3.2, with its source id, all read on 19 November 2026. Left out because the research couldn't verify them: Bolt's funding
/// and whether it exports secrets. Training is left out too: Bolt excludes EEA, UK and Swiss accounts from it.
/// </summary>
public static class BoltComparison
{
    public const string Key = "bolt";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, "Bolt")
    {
        TableTitle = [l["bit Fabric and Bolt, side by side"]],
        Answer = l["Bolt is the better choice if you want to build in your browser, with hosting and a database run for you and mobile apps through Expo. bit Fabric is the better choice if you want your app in your own accounts, on desktops too, with no tokens to buy."],
        FitTitle = [l["Bolt or bit Fabric?"]],
        FitLead = l["Both get you an app built with AI. They differ in where it lives, what it runs on and who you pay."],
        CardLine = l["Hosting, Bolt Database and tokens, next to your own server and your own AI plan"],
        CardIcon = BitIconName.BrowserTab,
        Columns = ["Bolt", "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["Mobile apps through Expo, published to the App Store and Google Play; GitHub sync both ways; editing code by hand spends no tokens"], "BOLT-6", "BOLT-7", "BOLT-2")),
            Row(l, CompareTopic.DayOne,
                F(l["A project built from your prompts, with Bolt Database built in: tables, authentication, storage, server functions and secrets"], "BOLT-5")),
            Row(l, CompareTopic.Where,
                F(l["Bolt Cloud hosts the app and Bolt Database holds your data, users and secrets. Your own Supabase, or moving the database into it, needs Pro or Teams"], "BOLT-4", "BOLT-5")),
            Row(l, CompareTopic.StopPaying,
                F(l["Cancelling a paid plan forfeits every token, rollover included. A free site stops serving at its monthly data limit; on Pro, a site pauses past its spending cap"], "BOLT-2", "BOLT-4")),
            Row(l, CompareTopic.Platforms,
                F(l["Web, and mobile through Expo for the App Store and Google Play. Desktop apps for your users weren't found in its docs"], "BOLT-6")),
            Row(l, CompareTopic.Cost,
                F(l["Pro from $25 a month, starting at 10M tokens. Larger projects use more tokens per message, as Bolt reads and syncs your files"], "BOLT-1", "BOLT-2")),
            Row(l, CompareTopic.Ai,
                F(l["Bolt's agent, so your prompts go to Bolt"], "BOLT-2")),
            Row(l, CompareTopic.Production,
                F(l["Hosting is built in: 10 GB and 333,333 requests a month free, 30 GB and 1M on Pro, then pay as you go up to a cap. Custom domains start on Pro"], "BOLT-4", "BOLT-1")),
            Row(l, CompareTopic.VendorStops,
                F(l["Your commits sync to GitHub. The hosting and Bolt Database run on Bolt; the database leaves by moving into your own Supabase, on Pro or Teams"], "BOLT-7", "BOLT-5"))
        ],
        SourceLine = l["Every fact about Bolt comes from its own pricing and help center, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose Bolt if"],
            [
                l["You want to start in your browser today, with nothing to install"],
                l["You want a mobile app in the App Store and Google Play through Expo"],
                l["You'd like hosting and a database run for you"],
                l["You want to edit code by hand without spending tokens"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want your hosting, your database and your users in your own accounts, on any plan"],
                l["You need Windows and macOS apps as well as phones and the web"],
                l["You'd rather pay for a server and your own AI plan than for tokens"],
                l["Nothing of your app should stop when you stop paying"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Can I move my Bolt app to bit Fabric?"],
                l["Yes, by rebuilding rather than importing. Your Bolt commits are in GitHub, so your AI can read them, rebuild the features on bit Fabric with the port skill, and then help bring your data over."]),
            Hosting(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
