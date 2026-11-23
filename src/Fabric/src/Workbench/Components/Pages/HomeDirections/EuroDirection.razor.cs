using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Day one, to the euro: Concept 2 of the concepts of 2026-11-18, with its hero, its calls to action, its seven bands and its
/// copy. The bill is bitfabric.co's own, as the app's calculator has it (AppHomeCostCalculator), never "your app costs €11".
/// </summary>
public partial class EuroDirection
{
    private sealed record Group(string Title, DirectionTile[] Items);

    private string[] TitleLines => [Localizer["Production‑ready on day one."], Localizer["A bill you can predict."]];

    /// <summary>bitfabric.co's monthly bill (bitfabric-internal/.agents/product-marketing.md, Claims), the calculator's items with every optional one on.</summary>
    private (string, int)[] Bill =>
    [
        (Localizer["Linux server, 2 GB RAM and 1 core"], 4),
        (Localizer["Email and SMS"], 1),
        (Localizer["OpenAI, for its assistant"], 3),
        (Localizer["Storage and backups"], 1),
        (Localizer["Domain"], 2)
    ];

    private Group[] Groups =>
    [
        new(Localizer["Your users"],
        [
            new(BitIconName.Fingerprint, AppLocalizer[nameof(AppStrings.AppHomeDayOneIdentity)], AppLocalizer[nameof(AppStrings.AppHomeDayOneIdentityText)], FilmId: "two-step"),
            new(BitIconName.Devices3, Localizer["A session per device"], Localizer["Each device has its own session, with a device limit if you want one."]),
            new(BitIconName.Permissions, Localizer["An extra check before sensitive actions"], Localizer["Users prove it's them again before what matters."], FilmId: "elevated")
        ]),
        new(Localizer["Your business"],
        [
            new(BitIconName.Org, AppLocalizer[nameof(AppStrings.AppHomeManyCompaniesOneApp)], AppLocalizer[nameof(AppStrings.AppHomeDayOneTenantsText)], FilmId: "tenants"),
            new(BitIconName.BarChartVerticalFill, AppLocalizer[nameof(AppStrings.NewProjectSummaryLiveDashboard)], AppLocalizer[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamInThe)], FilmId: "admin-panel"),
            new(BitIconName.ShieldSolid, Localizer["GDPR tools"], AppLocalizer[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)], FilmId: "gdpr")
        ]),
        new(Localizer["Your reach"],
        [
            new(BitIconName.Devices3, Localizer["Five platforms"], Localizer["Web, Android, iOS, Windows and macOS."]),
            new(BitIconName.CloudDownload, Localizer["Offline sync"], AppLocalizer[nameof(AppStrings.AppHomeItKeepsWorkingWithoutAConnectionAnd)], FilmId: "offline"),
            new(BitIconName.LocaleLanguage, AppLocalizer[nameof(AppStrings.AppHomeDayOneLanguages)], AppLocalizer[nameof(AppStrings.AppHomeDayOneLanguagesText)], FilmId: "rtl"),
            new(BitIconName.Ringer, Localizer["Email, SMS and push notifications"], Localizer["Your app reaches your users where they are."], FilmId: "push")
        ]),
        new(Localizer["Your releases"],
        [
            new(BitIconName.TestBeaker, Localizer["Tests and CI"], Localizer["So your AI's changes are checked."]),
            new(BitIconName.Deploy, Localizer["Deployment with no downtime"], Localizer["The new version starts next to the old one."], FilmId: "zero-downtime"),
            new(BitIconName.PostUpdate, Localizer["Force update"], Localizer["When a version has to go, everyone on it updates first."], FilmId: "force-update")
        ])
    ];

    private DirectionTile[] Security =>
    [
        new(BitIconName.Lock, Localizer["Checked on the server"], Localizer["No database key reaches the browser, and every request is checked on the server."]),
        new(BitIconName.Permissions, Localizer["Prove it's you, again"], Localizer["Sensitive actions ask your users to prove it's them again."], FilmId: "elevated"),
        new(BitIconName.Devices3, Localizer["A session per device"], Localizer["Each device has its own session, with a device limit if you want one."]),
        new(BitIconName.Shield, Localizer["A firewall in front"], Localizer["reCAPTCHA, and a web application firewall with DDoS protection in front of your app."], FilmId: "firewall"),
        new(BitIconName.View, Localizer["Production is read-only to your AI"], Localizer["It can read live data and logs to find a bug, and never change them."], FilmId: "dev-mcp"),
        new(BitIconName.SiteScan, Localizer["Scanned on every deployment"], Localizer["Once you're live, a security scanner checks each new version."], FilmId: "security-scan")
    ];

    private DirectionTile[] Platforms =>
    [
        new(BitIconName.Globe, Localizer["Web"], Localizer["Fast for search engines, and installable too."], FilmId: "website"),
        new(BitIconName.CellPhone, "Android", Localizer["Native features, from the same code."]),
        new(BitIconName.CellPhone, "iOS", Localizer["Native features, from the same code."]),
        new(BitIconName.WindowsLogo, "Windows", Localizer["Portable, from Windows 7 on, updates itself, with a kiosk mode."], FilmId: "portable"),
        new(BitIconName.ThisPC, "macOS", Localizer["A desktop app, from the same code."])
    ];

    private DirectionTile[] Relay =>
    [
        new(string.Empty, Localizer["You ask"], Localizer["Say what you want, in your own words."]),
        new(string.Empty, Localizer["It asks, then plans"], Localizer["What it needs to know first, then the work in steps."]),
        new(string.Empty, Localizer["You see the design"], Localizer["On every device, in light and dark, in the Workbench."]),
        new(string.Empty, Localizer["It builds and checks"], Localizer["It builds, runs the tests and reviews its own work."]),
        new(string.Empty, Localizer["You try it"], Localizer["On your Test site, before your users see it."])
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

    private DirectionTile[] Live =>
    [
        new(BitIconName.Deploy, Localizer["No downtime"], Localizer["To any Linux server: the new version starts next to the old one while the database updates."], FilmId: "zero-downtime"),
        new(BitIconName.Health, Localizer["Checked from three regions"], Localizer["Health checks from Germany, North America and Oceania."], FilmId: "health"),
        new(BitIconName.Ringer, Localizer["Alerts where you work"], Localizer["Slack, Teams and Discord, saying what broke for users and what still works."], FilmId: "alerts"),
        new(BitIconName.Website, Localizer["A status page on your domain"], Localizer["And SLA reports for your customers."], FilmId: "status-page"),
        new(BitIconName.SiteScan, Localizer["Scans on each deployment"], Localizer["Security, performance, SEO and accessibility, each finding with a fix your AI can apply."], FilmId: "scanner")
    ];

    private DirectionQuestion[] Questions =>
    [
        new(Localizer["What will my monthly bill be?"],
            Localizer["Your AI plan, a server from about €4, a domain, and store accounts only if you publish to the stores."]),
        new(AppLocalizer[nameof(AppStrings.AppHomeQuestionMac)], AppLocalizer[nameof(AppStrings.AppHomeAnswerMac)]),
        new(Localizer["What does bit Fabric cost?"], Localizer["Starting with bit Fabric is free during the beta."]),
        new(AppLocalizer[nameof(AppStrings.AppHomeQuestionBuiltOn)],
            AppLocalizer[nameof(AppStrings.AppHomeAnswerBuiltOn), AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads])
    ];
}
