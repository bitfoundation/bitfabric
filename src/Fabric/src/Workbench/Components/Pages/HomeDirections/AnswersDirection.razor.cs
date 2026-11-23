using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Answer first (2026-11-19): written to be quoted by an answer engine or a search result. It names Lovable, Bolt and Base44 on
/// purpose, under a source line the research fills; the hosted column says only what the app's own comparison says of a
/// hosted builder, and nothing about any one of them that bitfabric-internal/.agents/product-marketing.md doesn't hold as
/// sourced.
/// </summary>
public partial class AnswersDirection
{
    private string[] TitleLines => [Localizer["bit Fabric is an AI app builder you own: full featured on day one, built by your own AI, running on your own server."]];

    private DirectionFact[] KeyFacts =>
    [
        new(Localizer["Platforms"], Localizer["Web, Android, iOS, Windows and macOS, from one codebase"]),
        new(Localizer["To start"], Localizer["Free during the beta, with no account needed"]),
        new(Localizer["To run"], Localizer["From about €4 a month for a server, plus a domain and your AI plan"]),
        new(Localizer["Built on"], Localizer["bit platform, open source on GitHub: {0} stars, {1} contributors, {2} downloads", AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads]),
        new(Localizer["Made by"], Localizer["BitPlatform, in Leeuwarden, the Netherlands: the team behind bit platform"])
    ];

    private string[] CompareColumns => ["Lovable", "Bolt", "Base44", "bit Fabric"];

    /// <summary>
    /// The three named builders, each fact from their own pages as bitfabric-internal/.agents/gtm/research/competitors.md has
    /// them (sections 1a and 3, every page read 2026-11-19), within its section 8 and clear of its section 9: they all export
    /// code, so the lock-in is the backend, the secrets, the hosting and the users. "Not found in its docs" is what the
    /// research saw, not proof of absence.
    /// </summary>
    private DirectionRow[] Compare =>
    [
        new(Localizer["Entry plan"],
        [
            new(DirectionMark.None, Localizer["$25 a month (Pro), 100 credits"]),
            new(DirectionMark.None, Localizer["$25 a month (Pro), from 10M tokens"]),
            new(DirectionMark.None, Localizer["$16 a month (Starter, billed yearly), 100 message credits"]),
            new(DirectionMark.Good, Localizer["Free to start during the beta; your AI plan from $10 a month and a server from about €4"])
        ]),
        new(Localizer["Paid in"],
        [
            new(DirectionMark.Partly, Localizer["Credits: the same balance pays for building, hosting, the backend and the AI in your app"]),
            new(DirectionMark.Partly, Localizer["Tokens: larger projects use more of them per message"]),
            new(DirectionMark.Partly, Localizer["Message and integration credits"]),
            new(DirectionMark.Good, Localizer["Your AI plan and your server; no credits from us"])
        ]),
        new(Localizer["Your code"],
        [
            new(DirectionMark.Good, Localizer["Two-way Git sync, on every plan"]),
            new(DirectionMark.Good, Localizer["Synced with GitHub"]),
            new(DirectionMark.Partly, Localizer["Synced with GitHub from the $40 Builder plan"]),
            new(DirectionMark.Good, Localizer["In your own repository from the first line"])
        ]),
        new(Localizer["The backend and your users"],
        [
            new(DirectionMark.Partly, Localizer["Its built-in backend; with no credits left it pauses, and the data can't be exported until it runs again"]),
            new(DirectionMark.Partly, Localizer["Bolt Database; it moves to your own Supabase only on Pro or Teams"]),
            new(DirectionMark.Bad, Localizer["Base44's own backend; ejecting copies the schemas, not the data"]),
            new(DirectionMark.Good, Localizer["Your own database, on your own server"])
        ]),
        new(Localizer["Leaving"],
        [
            new(DirectionMark.Partly, Localizer["By hand, to Supabase only; secrets and AI gateway calls don't move"]),
            new(DirectionMark.Partly, Localizer["Cancelling forfeits every token, rollover included"]),
            new(DirectionMark.Bad, Localizer["No self-hosted production backend found"]),
            new(DirectionMark.Good, Localizer["Nothing to move: it's all in your accounts already"])
        ]),
        new(Localizer["Desktop apps for your users"],
        [
            new(DirectionMark.None, Localizer["Not found in its docs"]),
            new(DirectionMark.None, Localizer["Not found in its docs"]),
            new(DirectionMark.None, Localizer["Not found in its docs"]),
            new(DirectionMark.Good, Localizer["Windows and macOS, beside web, Android and iOS"])
        ])
    ];

    private DirectionTile[] Costs =>
    [
        new(string.Empty, Localizer["Free"], Localizer["Starting with bit Fabric, during the beta"]),
        new(string.Empty, "€4", Localizer["A Linux server, a month, and up"]),
        new(string.Empty, "€2", Localizer["A domain, a month"]),
        new(string.Empty, "$10", Localizer["An AI plan, a month and up: Copilot Pro is $10, Claude Pro $20"]),
        new(string.Empty, "$99", Localizer["Apple's developer account, a year, only to publish to the App Store; Google Play is $25 once"])
    ];

    private DirectionList[] WhoFor =>
    [
        new(Localizer["It's for"], DirectionMark.Good,
        [
            Localizer["Founders launching a SaaS for business customers without a development team"],
            Localizer["Agencies and freelancers who deliver each client's app on the client's own server"],
            Localizer["In-house IT teams building internal tools on company servers"]
        ]),
        new(Localizer["It's not for"], DirectionMark.Bad,
        [
            Localizer["Anyone who won't paste one line into their computer"],
            Localizer["Anyone who wants an instant preview in the browser with nothing to install"],
            Localizer["Anyone who wants us to host their app: we don't, by design"],
            Localizer["A one-page site"]
        ])
    ];

    private DirectionList[] Sees =>
    [
        new(Localizer["Only when you allow it"], DirectionMark.Partly,
        [
            Localizer["Your email, if you create an account"],
            Localizer["Your project's name and options, from the Get started page"],
            Localizer["Your app's health report, if you connect monitoring, never its data or error texts"],
            Localizer["What any visitor sees, if you ask for scans"]
        ]),
        new(Localizer["Never"], DirectionMark.Good,
        [
            Localizer["Your code and its history"],
            Localizer["Your prompts and conversations with your AI"],
            Localizer["Your database, files and your users' data"],
            Localizer["Your keys, secrets, logs and errors"]
        ])
    ];

    private DirectionList[] IfWeStop =>
    [
        new(AppLocalizer[nameof(AppStrings.AppHomeKeepsRunning)], DirectionMark.Good,
        [
            AppLocalizer[nameof(AppStrings.AppHomeKeepsRunningApp)],
            AppLocalizer[nameof(AppStrings.AppHomeKeepsRunningCode)],
            AppLocalizer[nameof(AppStrings.AppHomeKeepsRunningPipeline)],
            AppLocalizer[nameof(AppStrings.AppHomeKeepsRunningAI)]
        ]),
        new(AppLocalizer[nameof(AppStrings.AppHomeStops)], DirectionMark.Bad,
            [AppLocalizer[nameof(AppStrings.AppHomeStopsWhat)]],
            AppLocalizer[nameof(AppStrings.AppHomeStopsHealth)])
    ];

    /// <summary>The agents a project works with, in order (2026-11-19: Gemini stays, Antigravity joins it).</summary>
    private (string, string)[] Agents =>
    [
        (BitIconName.Robot, "GitHub Copilot"),
        (BitIconName.Robot, "Codex"),
        (BitIconName.Robot, "Claude Code"),
        (BitIconName.Robot, "Gemini"),
        (BitIconName.Robot, "Antigravity"),
        (BitIconName.Robot, "Cursor"),
        (BitIconName.ThisPC, Localizer["A model on your own computer"])
    ];

    private DirectionTile[] Steps =>
    [
        new(string.Empty, AppLocalizer[nameof(AppStrings.AppHomeHowAnswer)], AppLocalizer[nameof(AppStrings.AppHomeHowAnswerText)]),
        new(string.Empty, AppLocalizer[nameof(AppStrings.AppHomeHowPaste)], AppLocalizer[nameof(AppStrings.AppHomeHowPasteText)]),
        new(string.Empty, AppLocalizer[nameof(AppStrings.AppHomeHowBuild)], AppLocalizer[nameof(AppStrings.AppHomeHowBuildText)]),
        new(string.Empty, AppLocalizer[nameof(AppStrings.AppHomeHowDeploy)], AppLocalizer[nameof(AppStrings.AppHomeHowDeployText)])
    ];

    /// <summary>The glossary of bitfabric-internal/.agents/product-marketing.md, in the words a visitor needs.</summary>
    private DirectionFact[] Glossary =>
    [
        new("bit Fabric", Localizer["The product: the Get started page, the toolkit your AI builds with, and the portal that watches your live app."]),
        new("bit platform", Localizer["The open-source libraries bit Fabric is built on, on GitHub."]),
        new(Localizer["Your AI"], Localizer["The agent you build with: Copilot, Codex, Claude Code, Gemini, Antigravity, Cursor or a model on your own computer."]),
        new(Localizer["Workbench"], Localizer["Where your pages are designed on sample data, on every device, theme, language and data state, before they're built."]),
        new(Localizer["Skills"], Localizer["Ready instructions your AI runs: rebrand, port, deploy, troubleshoot and more."]),
        new(Localizer["Health check"], Localizer["Your app's health report, the only thing monitoring reads."])
    ];
}
