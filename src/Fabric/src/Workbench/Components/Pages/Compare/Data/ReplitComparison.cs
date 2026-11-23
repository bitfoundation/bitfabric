using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/replit. Every fact about Replit is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.4, with its source id, all read on 19 November 2026. Its separate development and production databases are said
/// as the strength they are, and the incident that led to them isn't told. Left out because the research couldn't verify
/// them: Google Play publishing, the plan custom domains need, and Core's yearly price (its two pages disagree).
/// </summary>
public static class ReplitComparison
{
    public const string Key = "replit";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, "Replit")
    {
        TableTitle = [l["bit Fabric and Replit, side by side"]],
        Answer = l["Replit is the better choice if you want to build and host in one place, with separate development and production databases. bit Fabric is the better choice if you want your app on your own server on any plan, with Windows and macOS apps too."],
        FitTitle = [l["Replit or bit Fabric?"]],
        FitLead = l["Both get you an app built with AI. They differ in where it lives, what it runs on and who you pay."],
        CardLine = l["Hosting and databases on Replit, next to the same on your own server"],
        CardIcon = BitIconName.BrowserTab,
        Columns = ["Replit", "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["Separate development and production databases; mobile apps with Expo, with TestFlight and App Store submission documented; work in its Power and Max modes priced before it starts"], "INC-7", "REP-9", "REP-3")),
            Row(l, CompareTopic.DayOne,
                F(l["An app built from your prompts, hosted by Replit with its own development and production databases"], "REP-7", "INC-7")),
            Row(l, CompareTopic.Where,
                F(l["Replit hosts the app and its databases. Your own cloud is for enterprise customers, in private preview or early access"], "REP-7")),
            Row(l, CompareTopic.StopPaying,
                F(l["Usage limits block usage-based services until the next billing cycle, and a database can be paused for an unpaid invoice. On the free plan, the one published app goes down after 30 days"], "REP-5", "REP-13", "REP-4")),
            Row(l, CompareTopic.Platforms,
                F(l["Web, and mobile apps with Expo, with TestFlight and App Store submission documented. Desktop apps for your users weren't found in its docs"], "REP-9")),
            Row(l, CompareTopic.Cost,
                F(l["Core $20 a month with $20 of credits that don't roll over; Pro $100. The AI in your app is billed from your credits at the provider's API rate"], "REP-1", "REP-2", "REP-3")),
            Row(l, CompareTopic.Ai,
                F(l["Replit's agent, so your prompts go to Replit; the AI in your app is billed through Replit too"], "REP-3")),
            Row(l, CompareTopic.Production,
                F(l["Publishing and hosting are built in, and its cloud prices dropped from 1 August 2026"], "REP-7", "REP-8")),
            Row(l, CompareTopic.VendorStops,
                F(l["You can connect GitHub or GitLab for your code. The hosting and databases run on Replit; your own cloud is for enterprise customers"], "REP-10", "REP-7"))
        ],
        SourceLine = l["Every fact about Replit comes from its own pricing, documentation and blog, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose Replit if"],
            [
                l["You want to build, host and run your databases in one place"],
                l["You want separate development and production databases, set up for you"],
                l["You want an iPhone app through Expo and TestFlight"],
                l["You'd like the price of bigger work confirmed before it starts"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want the hosting, the database and your users on your own server, on any plan"],
                l["You need Windows and macOS apps as well as phones and the web"],
                l["Nothing of your app should pause over a usage limit or an invoice"],
                l["You'd rather pay for a server and your own AI plan than for credits"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Can I move my Replit app to bit Fabric?"],
                l["Yes, by rebuilding rather than importing. Connect your Replit app to GitHub or GitLab, and your AI can read its code, rebuild the features on bit Fabric with the port skill, and then help bring your data over."]),
            Hosting(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
