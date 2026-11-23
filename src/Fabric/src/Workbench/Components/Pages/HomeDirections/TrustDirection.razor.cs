using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Trust first (2026-11-19): who makes it, what it sees and never sees (bitfabric-internal/specs/product.md, Trust and
/// privacy), where everything lives, what happens if it stops, how to check it, what it costs, the paperwork, the customers
/// and a careful buyer's questions.
/// The company's registration and address aren't known yet, so they are marked placeholders, never made up.
/// </summary>
public partial class TrustDirection
{
    private string[] TitleLines => [Localizer["Your app, your server, your AI."], Localizer["We never hold any of it."]];

    private (string, string)[] HeroChips =>
    [
        (BitIconName.MapPin, Localizer["A company in the Netherlands"]),
        (BitIconName.Lock, Localizer["Nothing of yours is with us"]),
        (BitIconName.Code, Localizer["Built on open source"])
    ];

    private DirectionFact[] Company =>
    [
        new(Localizer["Name"], "BitPlatform"),
        new(Localizer["Chamber of Commerce (KvK)"], "89263723"),
        new(Localizer["VAT number"], "NL004711376B75"),
        new(Localizer["Address"], Localizer["Sixmastraat 15, 8932 PA Leeuwarden, the Netherlands"]),
        new(Localizer["Contact"], "info@bitplatform.dev")
    ];

    private DirectionTile[] Team =>
    [
        new(BitIconName.People, Localizer["The team behind bit platform"],
            Localizer["{0} GitHub stars, {1} contributors and {2} downloads.", AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors, AppHomeNumbers.Downloads]),
        new(BitIconName.Code, Localizer["Built in public"],
            Localizer["bit platform's work happens on GitHub, where anyone can read it: {0} improvements a month.", AppHomeNumbers.ImprovementsAMonth]),
        new(BitIconName.Devices3, Localizer["Built with itself"],
            Localizer["bitfabric.co is a bit Fabric app, on the web, Android, iOS, Windows and macOS."])
    ];

    private string[] SeesColumns => [Localizer["What bit Fabric sees"]];

    /// <summary>bitfabric-internal/specs/product.md, Trust and privacy: what bit Fabric never sees, then what it sees, and only with permission.</summary>
    private DirectionRow[] Sees =>
    [
        Never(Localizer["Your code and its history"]),
        Never(Localizer["Your prompts and your conversations with your AI"]),
        Never(Localizer["Your database, your files and your users' data"]),
        Never(Localizer["Your keys and secrets"]),
        new(Localizer["Your app's logs and errors"], [new(DirectionMark.Good, Localizer["Never: they stay on your server, or go to your own error tracker"])]),
        Only(Localizer["Your email"], Localizer["Only if you create an account"]),
        Only(Localizer["Your project's name and options"], Localizer["Only what you pick on the Get started page, which says so"]),
        Only(Localizer["Your app's health"], Localizer["Only if you connect monitoring: each part's status and response time, never the data behind it or its error texts"]),
        Only(Localizer["What any visitor sees"], Localizer["Only if you ask for scans: headers, public pages and load times"]),
        Only(Localizer["The installed tool's own errors and timings"], Localizer["With no project content in them; on by default, and one line switches it off"])
    ];

    private DirectionRow Never(string topic) => new(topic, [new(DirectionMark.Good, Localizer["Never"])]);

    private static DirectionRow Only(string topic, string text) => new(topic, [new(DirectionMark.Info, text)]);

    private DirectionTile[] Where =>
    [
        new(BitIconName.Code, Localizer["Your repository"], AppLocalizer[nameof(AppStrings.AppHomeOwnCodeText)]),
        new(BitIconName.Server, Localizer["Your server"], Localizer["Any Linux server at a host you choose, Hetzner or Contabo in Europe for example, or one on your own premises."]),
        new(BitIconName.Database, Localizer["Your users and their data"], AppLocalizer[nameof(AppStrings.AppHomeOwnUsersText)]),
        new(BitIconName.Permissions, Localizer["Your keys"], AppLocalizer[nameof(AppStrings.AppHomeOwnKeysText)]),
        new(BitIconName.Robot, Localizer["Your AI"], Localizer["The agent and plan you already use, or a model on your own computer that keeps everything there."])
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

    private DirectionTile[] Check =>
    [
        new(BitIconName.Code, Localizer["Built on open source"],
            Localizer["bit platform is open source on GitHub, with {0} stars and {1} contributors.", AppHomeNumbers.GitHubStars, AppHomeNumbers.Contributors]),
        new(BitIconName.ReportDocument, Localizer["A script short enough to read"],
            Localizer["The line you paste runs a short installer from bitfabric.co. Read it first: it needs no administrator rights."]),
        new(BitIconName.Certificate, Localizer["Signed and attested"],
            Localizer["The tool it installs is built by GitHub Actions, signed and attested, so you can check that what you run is what was published."]),
        new(BitIconName.SpeedHigh, Localizer["Scores anyone can test"],
            Localizer["The landing page every project gets scores 100 for accessibility, SEO and performance. Run PageSpeed on it yourself."])
    ];

    private DirectionTile[] Paperwork =>
    [
        new(BitIconName.ReportDocument, Localizer["A data processing agreement"], Localizer["On request at info@bitplatform.dev, for the little bit Fabric does see: your account, and your app's health if you connect it."]),
        new(BitIconName.Hide3, Localizer["No tracking, no cookie banner"], Localizer["bitfabric.co has nothing to ask your consent for, so it doesn't ask."]),
        new(BitIconName.ShieldSolid, Localizer["GDPR tools in your app"], Localizer["Your users choose what they allow, download their data and delete their account by themselves."])
    ];

    private (string, string)[] Services =>
    [
        (BitIconName.Server, Localizer["A Linux server"]),
        (BitIconName.Globe, "Cloudflare"),
        (BitIconName.Code, "GitHub"),
        (BitIconName.Mail, Localizer["An email and SMS provider"]),
        (BitIconName.HardDrive, Localizer["S3 storage, for files and backups"]),
        (BitIconName.Robot, Localizer["OpenAI, for the site's assistant"])
    ];

    /// <summary>On the app's home page carousel; showing their logos waits for each one's consent (bitfabric-internal/.agents/product-marketing.md, Proof Points).</summary>
    private static string[] Customers => ["Rigid Robotics", "Antargyan", "SCA", "CrystaCode.ai"];

    private DirectionQuestion[] Questions =>
    [
        new(Localizer["Where is my data?"],
            Localizer["In your own database, on your own server, at the host you choose. bit Fabric never has it: monitoring reads only your app's health report and what any visitor sees."]),
        new(Localizer["Can it run on our own premises?"],
            Localizer["Yes. Your app runs on any Linux server, including one in your own building, with your company's single sign-on such as Keycloak. Serve it with Caddy, and no third party sits in front of it."]),
        new(Localizer["What if bit Fabric is acquired, or stops?"],
            Localizer["Your app keeps running unchanged: the code is in your repository and the database on your server. Only monitoring, alerts and status pages stop, and any other monitor can read the same health report. Versions already published under MIT stay MIT."]),
        new(Localizer["Which AI sees my code?"],
            AppLocalizer[nameof(AppStrings.AppHomeAnswerAISeesCode)])
    ];
}
