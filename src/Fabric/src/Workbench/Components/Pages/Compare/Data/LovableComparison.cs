using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/lovable. Every fact about Lovable is from bitfabric-internal/.agents/gtm/research/competitors.md,
/// section 3.1, with its source id, all read on 19 November 2026, and none from its section 9: Lovable exports code (two-way
/// Git sync on every plan) and its export carries the users and their password hashes, so the page's case is what stays
/// behind and what pauses (LOV-3, LOV-5). Training on content is left out, as whether Lovable's default excludes European
/// accounts wasn't verified.
/// </summary>
public static class LovableComparison
{
    public const string Key = "lovable";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, "Lovable")
    {
        TableTitle = [l["bit Fabric and Lovable, side by side"]],
        Answer = l["Lovable is the better choice if you want a web app built in your browser today, running on Lovable's cloud. bit Fabric is the better choice if you want your app in your own accounts, on every device, built by your own AI."],
        FitTitle = [l["Lovable or bit Fabric?"]],
        FitLead = l["Both get you an app built with AI. They differ in where it lives, what it runs on and who you pay."],
        CardLine = l["Where your app, its data and its keys live, and what pauses when credits run out"],
        CardIcon = BitIconName.BrowserTab,
        Columns = ["Lovable", "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["Two-way Git sync on every plan, Free included; its export carries your users and their password hashes; a Europe region for your app's data"], "LOV-1", "LOV-4", "LOV-7")),
            Row(l, CompareTopic.DayOne,
                F(l["A web app built from your prompts, with a database, users' accounts, file storage and server functions run by Lovable"], "LOV-7")),
            Row(l, CompareTopic.Where,
                F(l["Lovable Cloud holds the database, your users and your secrets, in the region you pick; or you connect your own Supabase"], "LOV-7", "LOV-5")),
            Row(l, CompareTopic.StopPaying,
                F(l["At zero credits, building stops, AI features in your app fail, and your app's database, files and users' accounts pause: the data can't be reached or exported until they run again. Published pages keep serving"], "LOV-3")),
            Row(l, CompareTopic.Platforms,
                F(l["Web. Its own iOS, Android and desktop apps are for building; publishing yours to the stores or the desktop wasn't found in its docs"], "LOV-9")),
            Row(l, CompareTopic.Cost,
                F(l["Pro from $25 a month for 100 credits, up to $2,250 for 10,000. The same credits pay for building, hosting, your app's database and the AI in your app"], "LOV-1", "LOV-14", "LOV-7")),
            Row(l, CompareTopic.Ai,
                F(l["Lovable's agent, so your prompts go to Lovable; the AI calls in your app run through its AI gateway"], "LOV-14", "LOV-5")),
            Row(l, CompareTopic.Production,
                F(l["Publishing is built in. Hosting is free for most small or new apps, heavier traffic is paid in credits, and custom domains start on Pro"], "LOV-1", "LOV-14")),
            Row(l, CompareTopic.VendorStops,
                F(l["Your code is in your Git repository. Your app's database and users' accounts move only to Supabase, by hand; its secrets, AI gateway calls and connector connections don't move"], "LOV-4", "LOV-5"))
        ],
        SourceLine = l["Every fact about Lovable comes from its own documentation and pricing, read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose Lovable if"],
            [
                l["You want to start in your browser today, with nothing to install"],
                l["A web app is all you need, and you're happy for Lovable to run it and its data"],
                l["You'd rather pay one plan for building and for running your app"],
                l["You want your code synced to Git from day one, even on the free plan"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want your app's database, your users' accounts and your keys in your own accounts from day one"],
                l["Nothing of your app should pause when credits or payments stop"],
                l["You need apps on Android, iOS, Windows and macOS, not only the web"],
                l["You'd rather pay for a server and your own AI plan than for credits"]
            ])
        ],
        Gaps = HostedGaps(l),
        Questions =
        [
            new(l["Can I move my Lovable app to bit Fabric?"],
                l["Yes, by rebuilding rather than importing. Your Lovable code is in your Git repository, so your AI can read it, rebuild its features on bit Fabric with the port skill, and then help bring your data over."]),
            Hosting(l),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
