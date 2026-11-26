using System.ComponentModel;

namespace Bit.Fabric.Shared;

// The [Description] attribute defines a short, human/AI friendly summary for a page.
// Only pages that carry a [Description] are exposed by PageUrls.GetPages() (see PageUrls.Pages.cs), which the chatbot's
// GetAppPages tool reads. bitfabric.co's /llms.txt is written by hand instead (Server.Web/Seo/llms.txt,
// bitfabric-internal/specs/home-page.md 2026-11-19), as these describe the template's pages and the ones that need an
// account too.
// Do not describe a page here unless the description is safe for an anonymous reader.
// [NoIndex] keeps a page out of search engines (See NoIndexAttribute and PageUrls.IsNoIndex), and [NotInSitemap] keeps an
// indexable page out of sitemap.xml (See NotInSitemapAttribute).
public static partial class PageUrls
{
    [Description("Home page.")]
    public const string Home = "/";

    [Description("Everything a project made with bit Fabric starts with: the apps for every platform, a complete identity solution, the AI assistant, languages, the Workbench, the admin panel, deployment and how bit Fabric watches over the live app, each with a short film.")]
    public const string Features = "/features";

    /// <summary>Everything about bit Fabric on one page, the home page as it was until 2026-11-18. In no menu (bitfabric-internal/specs/home-page.md).</summary>
    [Description("Everything about bit Fabric on one page: what it adds, how it compares, every feature a project starts with, each with a short film, and what bitfabric.co costs to run.")]
    public const string Contents = "/contents";

    /// <summary>What bit Fabric costs today and what the visitor pays others (bitfabric-internal/specs/home-page.md, 2026-11-19); <c>/pricing.md</c> is the same as Markdown.</summary>
    [Description("What bit Fabric costs: nothing while it is in beta, no account needed to start and no AI credits; what stays true whatever it ever charges; and what you pay others for your AI, your server and your domain, written out.")]
    public const string Pricing = "/pricing";

    /// <summary>Who makes bit Fabric and what it sees and never sees of a project (bitfabric-internal/specs/home-page.md, 2026-11-19).</summary>
    [Description("Who makes bit Fabric, what it sees and never sees of your project, where everything of yours lives, what happens if bit Fabric stops, how to check it yourself and how to report a security problem.")]
    public const string Trust = "/trust";

    /// <summary>
    /// Why a real app costs less with bit Fabric (bitfabric-internal/specs/home-page.md, "The cost page", 2026-11-21): judge
    /// the cost of the project, not the price of a token, in three steps (tokens per task, tokens per project, so the project
    /// costs less);
    /// <c>/cost.md</c> is the same as Markdown.
    /// </summary>
    [Description("Why a real app costs less with bit Fabric: judge the cost of your project, not the price of a token. The tokens a task takes count, as Anthropic's own figures on its models show, and the tokens a whole project takes count more still, where bit Fabric has already built and tested much of it and cuts the rounds of rework.")]
    public const string Cost = "/cost";

    /// <summary>
    /// bit CLI, the tool every project is made and kept with: what each command does for the visitor, how their AI agent runs
    /// them, how to install it and how to check the build they run. bitplatform.dev's own bit CLI page leads here.
    /// <c>/cli.md</c> is the same as Markdown.
    /// </summary>
    [Description("bit CLI, the tool every bit Fabric project is made and kept with: what each command does for you, how your AI agent runs them for you, every command in one line, how to install it, and how to check the build you run.")]
    public const string Cli = "/cli";

    /// <summary>
    /// bit Fabric side by side with hosted AI builders, an AI agent on an empty project and starter kits, then a page for each
    /// (bitfabric-internal/specs/home-page.md, "The comparison pages", 2026-11-19). Each page is a literal route of
    /// <c>ComparePage</c>, so each is in the sitemap and an unknown subject answers 404.
    /// </summary>
    [Description("How bit Fabric compares with hosted AI builders (Lovable, Bolt, Base44, Replit, v0), with your own AI agent on an empty project and with starter kits: side by side, each fact about another product with its source and the day it was read.")]
    public const string Compare = "/compare";

    [Description("bit Fabric and Lovable side by side: what each does well, where your app runs, what pauses when credits run out, platforms and cost, each fact about Lovable with its source.")]
    public const string CompareLovable = "/compare/lovable";

    [Description("bit Fabric and Bolt side by side: what each does well, hosting and Bolt Database, tokens and limits, platforms and cost, each fact about Bolt with its source.")]
    public const string CompareBolt = "/compare/bolt";

    [Description("bit Fabric and Base44 side by side: what each does well, what runs your app, credits and plans, platforms and leaving, each fact about Base44 with its source.")]
    public const string CompareBase44 = "/compare/base44";

    [Description("bit Fabric and Replit side by side: what each does well, hosting and databases, credits and limits, platforms and cost, each fact about Replit with its source.")]
    public const string CompareReplit = "/compare/replit";

    [Description("bit Fabric and v0 side by side: what each does well, GitHub sync and Vercel hosting, credits, platforms and cost, each fact about v0 with its source.")]
    public const string CompareV0 = "/compare/v0";

    [Description("Your own AI agent on an empty project, and the same agent on bit Fabric, side by side: what it starts from, security, platforms and what it costs.")]
    public const string CompareEmptyProject = "/compare/empty-project";

    [Description("SaaS starter kits (ShipFast, Makerkit, supastarter) and bit Fabric side by side: what you get, what it costs, the terms and updates, and the way to a live app.")]
    public const string CompareStarterKits = "/compare/starter-kits";

    /// <summary>
    /// The AI agents a project is ready for, then a page for each and for the models they can run
    /// (bitfabric-internal/specs/home-page.md, "The AI agent pages", 2026-11-21). Each page is a literal route of
    /// <c>ForPage</c>, so each is in the sitemap and an unknown agent answers 404.
    /// </summary>
    [Description("The AI agents every bit Fabric project is ready for on day one (GitHub Copilot, Claude Code, Codex, Cursor, Antigravity, Gemini CLI, Junie and Devin Desktop): what each reads in the project on its own, how to call a skill in it, and a page for each, with each fact about an agent from its own documentation.")]
    public const string For = "/for";

    [Description("GitHub Copilot on a bit Fabric project, in VS Code, Visual Studio or the terminal: what it reads on its own, how to call a skill, how to start, what it costs, and models of your choice with your own key.")]
    public const string ForGitHubCopilot = "/for/github-copilot";

    [Description("Claude Code on a bit Fabric project: CLAUDE.md, the skills as slash commands, the tools, how to start and what it costs.")]
    public const string ForClaudeCode = "/for/claude-code";

    [Description("OpenAI Codex on a bit Fabric project: AGENTS.md, the skills called with $, the project's tools, how to start and what it costs.")]
    public const string ForCodex = "/for/codex";

    [Description("Cursor on a bit Fabric project: AGENTS.md, the skills as slash commands, the tools, how to start and which plan has which models.")]
    public const string ForCursor = "/for/cursor";

    [Description("Google Antigravity on a bit Fabric project: AGENTS.md and GEMINI.md, the skills it picks by their description, how to start and its free plan.")]
    public const string ForAntigravity = "/for/antigravity";

    [Description("Gemini CLI on a bit Fabric project: GEMINI.md, a slash command for each skill, the tools, and which accounts it serves today.")]
    public const string ForGeminiCli = "/for/gemini-cli";

    [Description("JetBrains Junie on a bit Fabric project: AGENTS.md, the skills, the tools, how to start and what it costs.")]
    public const string ForJunie = "/for/junie";

    [Description("Devin Desktop, where windsurf.com now leads, on a bit Fabric project: AGENTS.md, the skills as slash commands, how to start and its plans.")]
    public const string ForWindsurf = "/for/windsurf";

    [Description("A model on your own computer, through Ollama or LM Studio, in GitHub Copilot, Claude Code or Codex, on a bit Fabric project.")]
    public const string ForLocalModels = "/for/local-models";

    [Description("DeepSeek, Qwen, Kimi, GLM and MiniMax with your own key in GitHub Copilot or Claude Code, on a bit Fabric project: where the key goes, how each is sold, and who sees your code.")]
    public const string ForChineseModels = "/for/chinese-models";

    [Description("Set up a new project: a few questions, then the one line that creates it on your own computer.")]
    public const string NewProject = "/new";

    [Description("Set up a project that already exists on this computer, a teammate's or one from another computer: its repository's address, then one line that clones it and gets it ready.")]
    public const string SetupProject = "/setup";

    [NoIndex]
    public const string NotFound = "/not-found";

    [Description("Legal terms, conditions and end-user license agreement.")]
    public const string Terms = "/terms";

    [Description("Privacy policy.")]
    public const string PrivacyPolicy = "/privacy-policy";

    [Description("User settings hub (profile, account, two-factor authentication and sessions). Requires sign-in.")]
    public const string Settings = "/settings";

    /// <summary>
    /// Who and what is behind bit Fabric (bitfabric-internal/specs/home-page.md, 2026-11-19), with what the app tells of
    /// itself at the end: each app's About page renders Client.Core's <c>AboutBitFabric</c>.
    /// </summary>
    [Description("What the name bit Fabric means (the warp is ready, you weave the weft), the principles it's built on, and who makes it: BitPlatform, in the Netherlands, the team behind bit platform. It ends with the app's version.")]
    public const string About = "/about";

    /// <summary>
    /// The open-source software the apps are made with, and its licenses, which the About page links. Only the app sends people
    /// there, and it names the technology underneath, which the site's own pages don't
    /// (bitfabric-internal/specs/product.md, Q09).
    /// </summary>
    [NoIndex]
    public const string OpenSourceLicenses = "/about/licenses";


    [Description("Manage the AI assistant's system prompts. Requires sign-in.")]
    public const string SystemPrompts = "/system-prompts";

    [Description("Manage the tenants you own. Requires sign-in.")]
    public const string ManageMyTenants = "/manage-my-tenants";

    [Description("Administer all tenants. Requires sign-in.")]
    public const string ManageAllTenants = "/manage-all-tenants";

    [Description("Manage user groups and roles. Requires sign-in.")]
    public const string Roles = "/user-groups";

    [Description("Manage users. Requires sign-in.")]
    public const string Users = "/users";

    [Description("Review and revoke the external applications authorized over OAuth. Requires sign-in.")]
    public const string OAuthClients = "/oauth-clients";

    [Description("Projects, Trials and Licenses over time, every team's together, as totals only. Requires sign-in.")]
    public const string Growth = "/growth";

    /// <summary>
    /// Signed out, it only says what signing in gives (bitfabric-internal/specs/monitoring.md), so it isn't in the sitemap
    /// (bitfabric-internal/specs/home-page.md, 2026-11-19).
    /// </summary>
    [Description("The customer's own apps that bit Fabric watches: how each one is doing, and adding another by its address. Requires sign-in.")]
    [NotInSitemap]
    public const string Projects = "/projects";

    /// <summary>
    /// One environment of a project, such as its Test deployment: <c>/projects/{projectId}/{monitoringId}</c>, where alerts,
    /// emails and the project switcher lead. <c>/projects/{projectId}</c> alone opens its Production environment, or its first.
    /// </summary>
    public static string ProjectEnvironment(Guid projectId, Guid monitoringId) => $"{Projects}/{projectId}/{monitoringId}";

    [Description("Add an app to watch: its address, then an approval in the app's own sign-in page. Requires sign-in.")]
    public const string AddProject = "/projects/add";

    /// <summary>Every license of each team the signed-in account is in (bitfabric-internal/specs/product.md, Seeing licenses).</summary>
    [Description("The licenses your teams hold for their projects: until when each lasts, and the prompt that puts one into its project. Requires sign-in.")]
    [NotInSitemap]
    public const string Licenses = "/licenses";

    /// <summary>
    /// The add page with an approval's answer, which web-interop-app.html opens when no add page waits for it, such as
    /// when the browser blocked the popup (See MonitoringClientController.Callback); no [Description] because only that
    /// redirect reaches it.
    /// </summary>
    public const string AddProjectCallback = "/projects/add/callback";

    /// <summary>
    /// A free license for the project the link bit CLI prints names: <c>?project=&amp;id=&amp;commit=&amp;code=</c>. No
    /// [Description] because only that link reaches it.
    /// </summary>
    [NoIndex]
    public const string License = "/license";

    /// <summary>
    /// Where <c>/oauth/authorize</c> sends the browser; no [Description] because only that redirect reaches it. An app
    /// link on both platforms: whoever installed the app is usually signed in there, and even a cold hybrid start beats
    /// booting Blazor WebAssembly in an Android browser (ConsentPage.LeaveFor gets back out). Deliberately not under
    /// /oauth/, which belongs to the server, so the service worker, the AASA and the Android filter need no carve-out.
    /// </summary>
    [NoIndex]
    public const string OAuthConsent = "/oauth-consent";

    /// <summary>
    /// The device's telemetry and what the server sees of the request, for troubleshooting. No [Description]: it is
    /// not part of the app's navigation.
    /// </summary>
    [NoIndex]
    public const string Diagnostic = "/diagnostic";

    public const string WebInteropApp = "/web-interop-app.html";
}
