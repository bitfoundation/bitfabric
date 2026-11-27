using System.Text;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>A page <c>/cost</c> cites: the name a reader knows it by, its address, and the day it was published when it says.</summary>
public sealed record AppCostSource(string Id, string Name, string Url, string? PublishedOn = null);

/// <summary>A step of how bit Fabric's AI builds a part: its icon, its title and one line.</summary>
public sealed record AppCostStep(string Icon, string Title, string Text);

/// <summary>One bar of the models' comparison: the model, the setting it ran at, the figure as written, and its length against the longest.</summary>
public sealed record AppCostBar(string Model, string? Setting, string Figure, double Share, bool IsStronger);

/// <summary>A question <c>/cost</c> answers, with the sources its answer rests on, if any.</summary>
public sealed record AppCostQuestion(string Question, string Answer, string[] Sources);

/// <summary>
/// What <c>/cost</c> says (bitfabric-internal/specs/home-page.md, "The cost page"), written once: <see cref="CostPage"/> renders it, and
/// <c>/cost.md</c> writes the same as Markdown for AI agents (See <see cref="Markdown"/>), so the two can't say different
/// things. The case, as one argument in three steps: a token's price means little
/// on its own, what counts is the tokens a task takes, which Anthropic's Opus and Sonnet illustrate in its own figures; more
/// important still are the tokens a whole project takes, where bit Fabric has already built and tested much of it and its
/// way of working cuts the rounds of rework, while the instructions and tools that keep it on course are read from the AI
/// provider's prompt cache; so a project costs less with bit Fabric. Opus and Sonnet are an example of the
/// principle, never the subject. Every figure about another company's product is a constant below with the source it comes from, read on
/// 27 November 2026; none is bit Fabric's own measurement.
/// </summary>
public static class AppCostCase
{
    /// <summary>The page as Markdown, which Server.Web serves.</summary>
    public const string MarkdownUrl = "/cost.md";

    /// <summary>The day every source was read (CostReadDate in each language), and the page's JSON-LD dateModified.</summary>
    public static readonly DateOnly ReadOn = new(2026, 11, 27);

    // The models, as Anthropic names them (ANT-2, ANT-3)
    public const string Sonnet = "Sonnet 5.5";
    public const string Opus = "Opus 5.5";

    // Price per million tokens, input and output, in US dollars (ANT-3)
    public const decimal SonnetInput = 2;
    public const decimal SonnetOutput = 10;
    public const decimal OpusInput = 4;
    public const decimal OpusOutput = 20;

    // A million input tokens read from the prompt cache, a twentieth of the input price (ANT-3)
    public const decimal SonnetCacheRead = 0.10m;

    // Anthropic's own test on a SWE-bench Pro subset: Sonnet 5.5 at its default effort against Opus 5.5 at low effort, the
    // share of tasks each solved and the cost per solved task, priced as a customer is billed; "about 7% more per solved
    // task" is Anthropic's own wording of the gap (ANT-2)
    public const decimal SonnetPerTask = 0.11m;
    public const decimal OpusPerTask = 0.12m;
    public const string SonnetSolved = "87.6%";
    public const string OpusSolved = "87.4%";
    public const string MorePerTask = "7%";

    // Opus 4.5 at medium effort against Sonnet 4.5's best SWE-bench Verified score (ANT-1)
    public const string FewerOutputTokens = "76%";

    /// <summary>
    /// Every page the page cites, in the order the page first cites them, so a source's number is its place here plus one.
    /// Names and addresses aren't translated: they are what a reader finds at the address.
    /// </summary>
    public static AppCostSource[] Sources(IStringLocalizer<AppStrings> l) =>
    [
        new("ANT-2", "Anthropic documentation: Optimizing for cost and intelligence", "https://platform.claude.com/docs/en/about-claude/models/optimizing-for-cost-and-intelligence"),
        new("ANT-3", "Anthropic documentation: Pricing", "https://platform.claude.com/docs/en/about-claude/pricing"),
        new("ANT-1", "Anthropic: Introducing Claude Opus 4.5", "https://www.anthropic.com/news/claude-opus-4-5", l[nameof(AppStrings.CostOpus45Date)]),
        new("AG-1", "Claude pricing", "https://claude.com/pricing")
    ];

    /// <summary>A source's number on the page.</summary>
    public static int NumberOf(IStringLocalizer<AppStrings> l, string id) => Array.FindIndex(Sources(l), source => source.Id == id) + 1;

    /// <summary>An amount in US dollars the way the page writes it in every language: $2, $0.12.</summary>
    public static string Dollars(decimal dollars) => "$" + dollars.ToString(dollars == decimal.Truncate(dollars) ? "N0" : "N2", CultureInfo.InvariantCulture);

    /// <summary>What every project starts with, built and tested before the first prompt: the home page's own day-one texts where it has them.</summary>
    public static AppHomeTile[] Built(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Fingerprint, l[nameof(AppStrings.AppHomeDayOneIdentity)], l[nameof(AppStrings.AppHomeDayOneIdentityText)]),
        new(BitIconName.Org, l[nameof(AppStrings.AppHomeManyCompaniesOneApp)], l[nameof(AppStrings.AppHomeDayOneTenantsText)]),
        new(BitIconName.BarChartVerticalFill, l[nameof(AppStrings.NewProjectSummaryLiveDashboard)], l[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamInThe)]),
        new(BitIconName.LocaleLanguage, l[nameof(AppStrings.AppHomeDayOneLanguages)], l[nameof(AppStrings.AppHomeDayOneLanguagesText)]),
        new(BitIconName.CloudDownload, l[nameof(AppStrings.AppHomeWorksOffline)], l[nameof(AppStrings.AppHomeItKeepsWorkingWithoutAConnectionAnd)]),
        new(BitIconName.ShieldSolid, l[nameof(AppStrings.AppHomeGDPRFriendly)], l[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)]),
        new(BitIconName.Devices3, l[nameof(AppStrings.CostBuiltPlatforms)], l[nameof(AppStrings.CostBuiltPlatformsText)]),
        new(BitIconName.CheckList, l[nameof(AppStrings.CostBuiltTests)], l[nameof(AppStrings.CostBuiltTestsText)])
    ];

    /// <summary>Building by trial: the rounds a prompt that wasn't settled first goes through.</summary>
    public static string[] Trial(IStringLocalizer<AppStrings> l) =>
    [
        l[nameof(AppStrings.CostTrialPrompt)],
        l[nameof(AppStrings.CostTrialBuilt)],
        l[nameof(AppStrings.CostTrialNotIt)],
        l[nameof(AppStrings.CostTrialAgain)],
        l[nameof(AppStrings.CostTrialBroke)]
    ];

    /// <summary>How bit Fabric's AI builds a part (the ai-dlc skill and the Workbench), each step once.</summary>
    public static AppCostStep[] Way(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Comment, l[nameof(AppStrings.CostWayPlan)], l[nameof(AppStrings.CostWayPlanText)]),
        new(BitIconName.Devices2, l[nameof(AppStrings.CostWayShow)], l[nameof(AppStrings.CostWayShowText)]),
        new(BitIconName.Code, l[nameof(AppStrings.CostWayBuild)], l[nameof(AppStrings.CostWayBuildText)]),
        new(BitIconName.CheckListCheck, l[nameof(AppStrings.CostWayCheck)], l[nameof(AppStrings.CostWayCheckText)]),
        new(BitIconName.CompletedSolid, l[nameof(AppStrings.CostWayDone)], l[nameof(AppStrings.CostWayDoneText)])
    ];

    /// <summary>The price of a million tokens, input and output, on each model (ANT-3): the stronger one's twice the other's.</summary>
    public static AppCostBar[] TokenPrices() =>
    [
        new(Sonnet, null, $"{Dollars(SonnetInput)} / {Dollars(SonnetOutput)}", (double)(SonnetOutput / OpusOutput), IsStronger: false),
        new(Opus, null, $"{Dollars(OpusInput)} / {Dollars(OpusOutput)}", 1, IsStronger: true)
    ];

    /// <summary>What a solved task cost each model in Anthropic's test (ANT-2), at the settings it ran them at.</summary>
    public static AppCostBar[] TaskCosts(IStringLocalizer<AppStrings> l) =>
    [
        new(Sonnet, l[nameof(AppStrings.CostModelsDefaultEffort)], Dollars(SonnetPerTask), (double)(SonnetPerTask / OpusPerTask), IsStronger: false),
        new(Opus, l[nameof(AppStrings.CostModelsLowEffort)], Dollars(OpusPerTask), 1, IsStronger: true)
    ];

    /// <summary>The project's instructions on <see cref="Sonnet"/>, without the prompt cache and read from it (ANT-3).</summary>
    public static AppCostBar[] CachePrices(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.CostCachedWithout)], null, Dollars(SonnetInput), 1, IsStronger: false),
        new(l[nameof(AppStrings.CostCachedFrom)], null, Dollars(SonnetCacheRead), (double)(SonnetCacheRead / SonnetInput), IsStronger: true)
    ];

    /// <summary>The panel's title, the model's name kept on one line.</summary>
    public static string CachePanelTitle(IStringLocalizer<AppStrings> l) => l[nameof(AppStrings.CostCachedPanelTitle), Sonnet.Replace(' ', ' ')];

    public static string TaskLine(IStringLocalizer<AppStrings> l) => l[nameof(AppStrings.CostModelsTaskLine), MorePerTask, SonnetSolved, OpusSolved];

    public static string Earlier(IStringLocalizer<AppStrings> l) => l[nameof(AppStrings.CostModelsEarlier), FewerOutputTokens];

    /// <summary>
    /// The questions: the empty project's and the hosted builders' cost questions with the comparison pages' own answers, so
    /// the site says one thing, then what the visitor pays for and which model to build with.
    /// </summary>
    public static AppCostQuestion[] Questions(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.CompareQuestionTokens)], l[nameof(AppStrings.CompareAnswerTokens)], ["AG-1"]),
        new(l[nameof(AppStrings.CompareQuestionCheaper)], l[nameof(AppStrings.CompareAnswerCheaper)], []),
        new(l[nameof(AppStrings.CostQuestionPay)], l[nameof(AppStrings.CostAnswerPay)], []),
        new(l[nameof(AppStrings.CostQuestionModel)], l[nameof(AppStrings.CostAnswerModel)], ["ANT-2"])
    ];

    /// <summary>
    /// The page as Markdown for AI agents, in the order the page shows it, with every figure's source as a number that leads
    /// to the list at the end. <paramref name="pageUrl"/> and <paramref name="getStartedUrl"/> are whole addresses on the host serving it.
    /// </summary>
    public static string Markdown(IStringLocalizer<AppStrings> l, string pageUrl, string pricingUrl, string getStartedUrl)
    {
        string Ref(params string[] ids) => string.Concat(ids.Select(id => $" [{NumberOf(l, id)}]"));

        var md = new StringBuilder();

        md.Append("# ").AppendLine(l[nameof(AppStrings.CostTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostLead)]).AppendLine();
        md.Append("The page: ").AppendLine(pageUrl).AppendLine();

        // 1. Tokens per task, which Anthropic's own models illustrate
        md.Append("## 1. ").AppendLine(l[nameof(AppStrings.CostModelsTitle)]).AppendLine();
        md.Append(l[nameof(AppStrings.CostModelsIntro)]).AppendLine(Ref("ANT-2")).AppendLine();
        md.Append("**").Append(l[nameof(AppStrings.CostModelsTokenTitle)]).Append("**").AppendLine(Ref("ANT-3")).AppendLine();
        foreach (var bar in TokenPrices())
        {
            md.Append("- ").Append(bar.Model).Append(": ").AppendLine(bar.Figure);
        }
        md.AppendLine().AppendLine(l[nameof(AppStrings.CostModelsTokenLine)]).AppendLine();
        md.Append("**").Append(l[nameof(AppStrings.CostModelsTaskTitle)]).Append("**").AppendLine(Ref("ANT-2")).AppendLine();
        foreach (var bar in TaskCosts(l))
        {
            md.Append("- ").Append(bar.Model).Append(' ').Append(bar.Setting).Append(": ").AppendLine(bar.Figure);
        }
        md.AppendLine().AppendLine(TaskLine(l)).AppendLine();
        md.Append(Earlier(l)).AppendLine(Ref("ANT-1")).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostModelsWhy)]).AppendLine();

        // 2. Tokens per project: what is already built, fewer rounds of rework, and the instructions read from the cache
        md.Append("## 2. ").AppendLine(l[nameof(AppStrings.CostProjectTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostProjectIntro)]).AppendLine();
        md.Append("### ").AppendLine(l[nameof(AppStrings.CostBuiltTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostBuiltIntro)]).AppendLine();
        foreach (var tile in Built(l))
        {
            md.Append("- **").Append(tile.Title).Append("**: ").AppendLine(tile.Text);
        }
        md.AppendLine();
        md.Append("### ").AppendLine(l[nameof(AppStrings.CostRoundsTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostRoundsIntro)]).AppendLine();
        md.Append("**").Append(l[nameof(AppStrings.CostTrialTitle)]).Append("**: ").Append(string.Join(" → ", Trial(l))).Append(" → ").AppendLine(l[nameof(AppStrings.CostTrialRepeat)]).AppendLine();
        md.Append("**").Append(l[nameof(AppStrings.CostWayTitle)]).AppendLine("**:").AppendLine();
        var number = 1;
        foreach (var step in Way(l))
        {
            md.Append(number++).Append(". **").Append(step.Title).Append("**: ").AppendLine(step.Text);
        }
        md.AppendLine().AppendLine(l[nameof(AppStrings.CostRoundsLine)]).AppendLine();
        md.Append("### ").AppendLine(l[nameof(AppStrings.CostCachedTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostCachedIntro)]).AppendLine();
        md.Append("**").Append(CachePanelTitle(l)).Append("**").AppendLine(Ref("ANT-3")).AppendLine();
        foreach (var bar in CachePrices(l))
        {
            md.Append("- ").Append(bar.Model).Append(": ").AppendLine(bar.Figure);
        }
        md.AppendLine().AppendLine(l[nameof(AppStrings.CostCachedPanelLine)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostCachedLine)]).AppendLine();

        // 3. So the project costs less, and the bill is on the pricing page
        md.Append("## 3. ").AppendLine(l[nameof(AppStrings.CostSwitchTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostSwitchIntro)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostSwitchLine)]).AppendLine();
        md.Append('[').Append(l[nameof(AppStrings.CostSwitchLink)]).Append("](").Append(AppHomeNumbers.WorkbenchDemoUrl).AppendLine("/progress)").AppendLine();

        md.Append("## 4. ").AppendLine(l[nameof(AppStrings.CostLessTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostLessText)]).AppendLine();
        md.Append('[').Append(l[nameof(AppStrings.CostLessLink)]).Append("](").Append(pricingUrl).AppendLine(")").AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.CompareQuestionsTitle)]).AppendLine();
        foreach (var question in Questions(l))
        {
            md.Append("### ").AppendLine(question.Question).AppendLine();
            md.Append(question.Answer).AppendLine(Ref(question.Sources)).AppendLine();
        }

        md.Append("## ").AppendLine(l[nameof(AppStrings.CompareSourcesTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.CostSourcesIntro), l[nameof(AppStrings.CostReadDate)].Value]).AppendLine();
        number = 1;
        foreach (var source in Sources(l))
        {
            md.Append(number++).Append(". [").Append(source.Name).Append("](").Append(source.Url).Append(')');
            if (source.PublishedOn is not null)
            {
                md.Append(". ").Append(l[nameof(AppStrings.CostPublishedOn), source.PublishedOn]);
            }
            md.AppendLine();
        }
        md.AppendLine();

        md.Append("## ").AppendLine(l[nameof(AppStrings.AppHomeFinalTitle)]).AppendLine();
        md.AppendLine(l[nameof(AppStrings.AppHomeFinalText)]).AppendLine();
        md.Append('[').Append(l[nameof(AppStrings.AppHomeGetStarted)]).Append("](").Append(getStartedUrl).AppendLine(")");

        return md.ToString().ReplaceLineEndings("\n");
    }
}
