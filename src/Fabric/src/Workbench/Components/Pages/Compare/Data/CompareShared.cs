using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// What every comparison says the same way: the rows' topics, bit Fabric's side of each row (within the claims table of
/// bitfabric-internal/.agents/product-marketing.md), what bit Fabric doesn't have, what it costs, and the questions the pages
/// share.
/// </summary>
public static class CompareShared
{
    /// <summary>The day every source was read (bitfabric-internal/.agents/gtm/research/competitors.md).</summary>
    public const string ReadOn = "19 November 2026";

    /// <summary>The sources of the cost band and of the question on which AI to use, on every page.</summary>
    public static readonly string[] PageSources = ["AG-5", "AG-1", "AG-10"];

    /// <summary>A fact and the ids of the sources it rests on.</summary>
    public static CompareFact F(string text, params string[] sources) => new(text, sources);

    /// <summary>A row of a page that sets one other column next to bit Fabric's; bit Fabric's own fact unless one is given.</summary>
    public static CompareRow Row(IStringLocalizer l, CompareTopic topic, CompareFact theirs, CompareFact? ours = null)
        => new(topic, [theirs, ours ?? Ours(l, topic)]);

    public static string TopicLabel(IStringLocalizer l, CompareTopic topic) => topic switch
    {
        CompareTopic.DoesWell => l["What it does well"],
        CompareTopic.DayOne => l["What you have on day one"],
        CompareTopic.Where => l["Where your app runs, and its data and keys live"],
        CompareTopic.StopPaying => l["When you stop paying or run out of credits"],
        CompareTopic.Platforms => l["Web, mobile and desktop"],
        CompareTopic.Cost => l["What it costs, and how the bill grows"],
        CompareTopic.Ai => l["Which AI builds it, and who sees your prompts"],
        CompareTopic.Production => l["Getting to production"],
        CompareTopic.VendorStops => l["If the company behind it stops"],
        CompareTopic.License => l["The license"],
        _ => l["Updates"]
    };

    /// <summary>bit Fabric's side of a row, said the same way on every page.</summary>
    public static CompareFact Ours(IStringLocalizer l, CompareTopic topic) => topic switch
    {
        CompareTopic.DoesWell => F(l["Starts full featured on five platforms, with every piece of it in your own accounts"]),
        CompareTopic.DayOne => F(l["A complete identity solution, multi-tenancy, an admin panel, an AI assistant, many languages and offline sync, built and tested"]),
        CompareTopic.Where => F(l["Your code in your repository, your users in your own database on your own server, your keys with you"]),
        CompareTopic.StopPaying => F(l["Nothing pauses: your app runs on your server, and there are no credits from us to run out of"]),
        CompareTopic.Platforms => F(l["Web, Android, iOS, Windows and macOS, from one codebase"]),
        CompareTopic.Cost => F(l["Free to start during the beta. Then a server from about €4 a month and your AI plan from $10; your prompts never add to the server's bill"], "AG-5"),
        CompareTopic.Ai => F(l["Your choice: Copilot, Codex, Claude, Gemini, Antigravity or Cursor, or a model on your own computer. bit Fabric never sees your prompts"]),
        CompareTopic.Production => F(l["Your AI sets up deployment to a Linux server you rent, through GitHub. Production is read-only for the AI"]),
        CompareTopic.VendorStops => F(l["Your app keeps running unchanged. Only monitoring, alerts and status pages stop"]),
        CompareTopic.License => F(l["Starting is free during the beta. A paid license comes after it, on terms not set yet; versions already published under MIT stay MIT"]),
        _ => F(l["Your AI brings in each new version, 500+ improvements a month, and keeps the changes you made"])
    };

    /// <summary>What bit Fabric doesn't have next to a hosted builder, written plainly.</summary>
    public static DirectionTile[] HostedGaps(IStringLocalizer l) =>
    [
        new(BitIconName.BrowserTab, l["No instant preview in the browser"], l["Your app first runs on your computer, after you paste one line."]),
        new(BitIconName.Download, l["Something to install"], l["The line installs what your project needs into your user folder, with no administrator rights."]),
        new(BitIconName.Server, l["A server to rent"], l["From about €4 a month, at a host you choose. Your AI sets it up with you."]),
        PaymentsGap(l)
    ];

    public static DirectionTile PaymentsGap(IStringLocalizer l)
        => new(BitIconName.PaymentCard, l["Payments aren't built in yet"], l["Taking your customers' payments isn't part of what's built and tested yet, so your AI adds it like any other feature."]);

    public static DirectionTile StackGap(IStringLocalizer l)
        => new(BitIconName.Code, l["A stack you don't pick"], l["Your app is built on one stack, chosen and tested for you, rather than on languages and frameworks you choose."]);

    /// <summary>What bit Fabric costs, with the real numbers: the home page's calculator and bitfabric-internal/.agents/product-marketing.md, Objections.</summary>
    public static DirectionTile[] Costs(IStringLocalizer l) =>
    [
        new(string.Empty, l["Free"], l["Starting with bit Fabric, during the beta"]),
        new(string.Empty, "€4", l["A Linux server, a month, and up"]),
        new(string.Empty, "€2", l["A domain, a month"]),
        new(string.Empty, "$10", l["Your AI plan, a month, and up: Copilot Pro is $10, Claude Pro $20"]),
        new(string.Empty, "$99", l["Apple's developer account, a year, only to publish to the App Store; Google Play is $25 once"])
    ];

    public static string CostNote(IStringLocalizer l)
        => l["bitfabric.co, built with bit Fabric, runs on €11 a month with every optional item on, and there are no credits from us. A paid license comes after the beta; its terms aren't set yet."];

    public static string CostSourceLine(IStringLocalizer l)
        => l["AI plan prices from GitHub's and Anthropic's own pricing pages, read on {0}.", ReadOn];

    public static DirectionQuestion WhichAi(IStringLocalizer l) => new(l["Which AI can I use?"],
        l["The agent you already use: Copilot, Codex, Claude, Gemini, Antigravity or Cursor, or a model on your own computer, which keeps everything there. The project carries its own instructions and skills, so it works the same whichever you pick. Gemini CLI has served only API keys and organisation licences since 18 June 2026, when Google pointed personal accounts to Antigravity."]);

    public static DirectionQuestion AfterBeta(IStringLocalizer l) => new(l["What happens after the beta?"],
        l["A paid license comes after the beta, and its terms aren't set yet. Whatever it becomes, the app you built keeps running on your server, and versions already published under MIT stay MIT."]);

    public static DirectionQuestion Hosting(IStringLocalizer l) => new(l["Does bit Fabric host my app?"],
        l["No, by design. Your app runs on a Linux server you rent at a host you choose, Hetzner or Contabo in Europe for example, or on one in your own building, so nothing of it is with us. Your AI sets up the deployment through GitHub."]);
}
