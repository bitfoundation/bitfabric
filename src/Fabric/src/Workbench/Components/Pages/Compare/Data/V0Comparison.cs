using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/v0. Every fact about v0 is from bitfabric-internal/.agents/gtm/research/competitors.md, section 3.5,
/// with its source id, all read on 19 November 2026. v0 exports its code and lets you deploy it elsewhere, so the page says so
/// first. Training is left out: Vercel's own two pages disagree on its default. What happens to a deployed app when credits
/// run out isn't stated by v0, so the page doesn't say it.
/// </summary>
public static class V0Comparison
{
    public const string Key = "v0";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, "v0")
    {
        TableTitle = [l["bit Fabric and v0, side by side"]],
        Answer = l["v0 is the better choice if you build web apps and want two-way GitHub sync and Vercel's hosting. bit Fabric is the better choice if you want phone and desktop apps from the same code, with your data on your own server."],
        FitTitle = [l["v0 or bit Fabric?"]],
        FitLead = l["Both get you an app built with AI. They differ in where it lives, what it runs on and who you pay."],
        CardLine = l["Web apps on Vercel, next to five platforms on your own server"],
        CardIcon = BitIconName.BrowserTab,
        Columns = ["v0", "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["Code syncs both ways with GitHub, and you \"can still export code and deploy elsewhere\"; databases from Neon, Supabase or Upstash"], "V0-2")),
            Row(l, CompareTopic.DayOne,
                F(l["A web app built from your prompts, deployed to Vercel, with a database from the Vercel Marketplace"], "V0-2")),
            Row(l, CompareTopic.Where,
                F(l["The hosting and your environment variables stay on Vercel; the database comes from a Vercel Marketplace provider"], "V0-2")),
            Row(l, CompareTopic.StopPaying,
                F(l["Generation pauses when credits are used up. Monthly credits expire after 65 days, and bought credits need an active paid plan"], "V0-2")),
            Row(l, CompareTopic.Platforms,
                F(l["Web. Its own iOS app is for building; app builds for your users weren't found in its docs"], "V0-2")),
            Row(l, CompareTopic.Cost,
                F(l["Plus $30 a user a month, with $30 of credits each and $2 a day for logging in; tokens are billed at each model's rate"], "V0-1", "V0-2")),
            Row(l, CompareTopic.Ai,
                F(l["v0's own models, so your prompts go to Vercel"], "V0-1")),
            Row(l, CompareTopic.Production,
                F(l["Deploys to Vercel, or export the code and deploy it elsewhere"], "V0-2")),
            Row(l, CompareTopic.VendorStops,
                F(l["Your code is in GitHub and deploys elsewhere; the hosting and environment variables are on Vercel"], "V0-2"))
        ],
        SourceLine = l["Every fact about v0 comes from its own pricing and documentation, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose v0 if"],
            [
                l["You build web apps and want to start in your browser"],
                l["You want two-way GitHub sync and Vercel's hosting"],
                l["You want to be free to export the code and deploy it elsewhere"],
                l["You'd like your database from Neon, Supabase or Upstash"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You need apps on Android, iOS, Windows and macOS from the same code as the web"],
                l["You want your app's database and your users' accounts on your own server from day one"],
                l["You want identity, tenants and an admin panel built and tested before your first prompt"],
                l["You'd rather pay for a server and your own AI plan than per user and per token"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Can I move my v0 app to bit Fabric?"],
                l["Yes, by rebuilding rather than importing. Your v0 code is in GitHub, so your AI can read it, rebuild the features on bit Fabric with the port skill, and then help bring your data over."]),
            Hosting(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
