namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Nothing to promise: Concept 3 of the concepts of 2026-11-18, with its hero, its calls to action, its seven bands and its copy.
/// Its fourth question, whether a deployed app calls anything of bit Fabric's, isn't asked: the concept leaves it to be checked
/// before it is written, so another question the specs answer takes its place.
/// </summary>
public partial class PromiseDirection
{
    private string[] TitleLines => [Localizer["Build with AI."], Localizer["We never see your code, your data or your prompts."]];

    private (string, string, string)[] Inside =>
    [
        (BitIconName.ThisPC, Localizer["Your computer"], Localizer["Your AI builds here, a local model if you like"]),
        (BitIconName.Server, Localizer["Your server"], Localizer["Your app, its database and your users"])
    ];

    private string[] WhoColumns => [Localizer["Your prompts"], Localizer["Your code"], Localizer["Your app's data and users"]];

    private DirectionRow[] Who =>
    [
        new("bit Fabric",
        [
            new(DirectionMark.Good, Localizer["Never"]),
            new(DirectionMark.Good, Localizer["Never"]),
            new(DirectionMark.Good, Localizer["Never: monitoring reads only the health report and what any visitor sees"])
        ]),
        new(Localizer["A cloud AI you choose"],
        [
            new(DirectionMark.Info, Localizer["Yes, on your plan's terms"]),
            new(DirectionMark.Info, Localizer["What your agent reads"]),
            new(DirectionMark.Info, Localizer["Only what you let it read, read-only"])
        ]),
        new(Localizer["A model on your computer"],
        [
            new(DirectionMark.Good, Localizer["Stays with you"]),
            new(DirectionMark.Good, Localizer["Stays with you"]),
            new(DirectionMark.Good, Localizer["Stays with you"])
        ]),
        new(Localizer["Your server"],
        [
            new(DirectionMark.None, Localizer["Not applicable"]),
            new(DirectionMark.None, Localizer["The app you deploy"]),
            new(DirectionMark.None, Localizer["Yes, in your database"])
        ])
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

    private DirectionTile[] AnyAi =>
    [
        new(BitIconName.ThisPC, Localizer["A model on your computer"],
            Localizer["Your prompts and your code never reach an AI provider. It's slower than the largest cloud models, so switch whenever a task needs more."], FilmId: "any-ai"),
        new(BitIconName.Devices3, Localizer["macOS, Windows or Linux"], Localizer["Whichever you use, your AI builds on your own computer."]),
        new(BitIconName.Lock, Localizer["Nobody in between"], Localizer["bit Fabric doesn't route, log or bill your prompts."])
    ];

    private DirectionTile[] YourUsers =>
    [
        new(BitIconName.Fingerprint, AppLocalizer[nameof(AppStrings.AppHomeDayOneIdentity)],
            Localizer["Accounts live in your own database, with no identity provider holding your users or charging per user."], FilmId: "two-step"),
        new(BitIconName.Insights, Localizer["Logs on your own server"], Localizer["Error logs and metrics are stored on your server, not ours."], FilmId: "telemetry"),
        new(BitIconName.ShieldSolid, Localizer["GDPR tools built in"], AppLocalizer[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)], FilmId: "gdpr"),
        new(BitIconName.Chat, Localizer["Your assistant, your key"], Localizer["Your app's AI assistant uses the provider and key you configure, and searches only what you let it."], FilmId: "focused"),
        new(BitIconName.Database, Localizer["The database you need"], Localizer["PostgreSQL by default, and SQL Server, MySQL or SQLite if a client needs another."])
    ];

    private DirectionTile[] YourServer =>
    [
        new(BitIconName.Server, Localizer["Any Linux server"], Localizer["From about €4 a month, at a host and in a country you choose."], FilmId: "deploy"),
        new(BitIconName.CompanyDirectory, Localizer["Or your own building"], Localizer["A machine on your own premises, for an internal tool."]),
        new(BitIconName.SecurityGroup, Localizer["Your company's sign-in"], Localizer["Enterprise single sign-on with Keycloak."], FilmId: "sso"),
        new(BitIconName.View, Localizer["Read-only production"], Localizer["Your AI can read live data and logs to find a bug, never change them."], FilmId: "dev-mcp")
    ];

    private DirectionTile[] Watched =>
    [
        new(BitIconName.Health, Localizer["Only the health report"], Localizer["The same one any monitor can read, and what any visitor sees, for the scans. That is all it reads."], FilmId: "health"),
        new(BitIconName.Blocked2, Localizer["Nothing of ours on your server"], Localizer["No dashboard or agent of ours runs there, and we hold no SSH key or database access."]),
        new(BitIconName.Ringer, Localizer["Alerts and a status page"], Localizer["Checks from three regions, alerts in Slack, Teams and Discord, a status page on your own domain."], FilmId: "status-page"),
        new(BitIconName.Download, Localizer["Yours to take elsewhere"], Localizer["Your health history and SLA reports export. If you stop using us, point another monitor at the same address."])
    ];

    private DirectionQuestion[] Questions =>
    [
        new(Localizer["Does my AI provider see my code?"], Localizer["It sees what your agent reads, on your plan's terms. A local model keeps it on your computer."]),
        new(Localizer["Can I build with a local model?"], Localizer["Yes, on macOS, Windows or Linux."]),
        new(Localizer["What happens if bit Fabric stops?"], Localizer["Your app keeps running unchanged. Only monitoring and status pages stop."]),
        new(Localizer["Do I need an account to start?"], Localizer["No. An account is asked for only when you connect a live app to monitoring."])
    ];
}
