using System.Text;
using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>A plan as its card shows it: its name, who it's for, its price and what it includes.</summary>
public sealed record AppPricingPlan(string Name, string Audience, string Price, string? Period, string? Note, string[] Includes, bool IsHighlighted = false);

/// <summary>
/// A row of "Where a month's money goes": the same work on a hosted builder and with bit Fabric. A null <see cref="BuilderNote"/>
/// means the builder's side has no such row (the license); a null <see cref="BuilderAmount"/> with a note means the work is inside
/// the builder's price with no amount of its own.
/// </summary>
public sealed record AppPricingCostLine(string Kind, string Label, string? BuilderAmount, string? BuilderNote, string FabricAmount, string FabricPaidTo);

/// <summary>
/// What <c>/pricing</c> says (bitfabric-internal/specs/home-page.md; bitfabric-internal/specs/product.md, Business model), written once:
/// <see cref="PricingPage"/> renders it, and <c>/pricing.md</c> writes the same as Markdown for AI agents (See
/// <see cref="Markdown"/>), so the two can't say different things. The plans are a draft:
/// free if you can't pay, €10 a month for up to three projects, €200 for every project of one company. They're on the page
/// on Test, which people are shown for their feedback (See <see cref="IsShown"/>); nobody pays during the
/// beta, bit CLI checks no license yet and there are no payments. Every line stays within the claims table
/// (bitfabric-internal/.agents/product-marketing.md): never "free" for the whole thing, no referral, nothing that suggests
/// building takes months or that the visitor needs to buy a computer.
/// </summary>
public static class AppPricing
{
    /// <summary>The page as Markdown, which Server.Web serves.</summary>
    public const string MarkdownUrl = "/pricing.md";

    /// <summary>
    /// Whether the site shows the plans. On Production, for now, it doesn't (bitfabric-internal/specs/home-page.md): /pricing says
    /// "Coming soon", /pricing.md doesn't answer and llms.txt names no plan. Test, where the draft is shown to people,
    /// and every developer's machine show them.
    /// </summary>
    public static bool IsShown => AppEnvironment.IsProduction() is false;

    // The draft plans, in euros excluding VAT (bitfabric-internal/specs/product.md, Business model). Yearly is ten months for twelve.
    public const int TeamProjects = 3;
    public const decimal TeamMonthly = 10;
    public const decimal TeamYearly = 100;
    public const decimal CompanyMonthly = 200;
    public const decimal CompanyYearly = 2_000;
    public const int TrialDays = 30;
    public const int RefundDays = 30;

    // A license for life: the first 500, about two years of the plan, once.
    public const int LifetimeSeats = 500;
    public const decimal LifetimeTeam = TeamMonthly * 24;
    public const decimal LifetimeCompany = CompanyMonthly * 24;

    // An example month for a team of two building a real app: the builder's amounts are an example, its share included (an
    // example of 20%, which no builder publishes), and its builds and tests are inside its price. The AI plan is Claude Max's
    // $100 a month with Claude Code (claude.com/pricing, read 2026-11-20); the server is Contabo's Cloud VPS 4, 4 cores and
    // 8 GB, €4.40 a month with VAT on a 24-month term (contabo.com/en/vps, read 2026-11-20), named plainly with no referral
    // link.
    public const decimal ExampleBuilderAi = 240;
    public const decimal ExampleBuilderHosting = 30;
    public const decimal ExampleOwnAi = 100;
    public const decimal ExampleOwnServer = 4.40m;

    /// <summary>An amount the way the page writes it in every language: €10, €2,000, €4.40.</summary>
    public static string Euros(decimal euros) => "€" + euros.ToString(euros == decimal.Truncate(euros) ? "N0" : "N2", CultureInfo.InvariantCulture);

    public static AppPricingPlan[] Plans(IStringLocalizer<AppStrings> l) =>
    [
        new(l[nameof(AppStrings.PricingPlanFreeName)], l[nameof(AppStrings.PricingPlanFreeAudience)], Euros(0), null, null,
            [l[nameof(AppStrings.PricingUpToProjects), TeamProjects], l[nameof(AppStrings.PricingPlanFreeEverything)], l[nameof(AppStrings.PricingPlanFreeRenewed)]]),
        new(l[nameof(AppStrings.PricingPlanTeamName)], l[nameof(AppStrings.PricingUpToProjects), TeamProjects], Euros(TeamMonthly), l[nameof(AppStrings.PricingPerMonth)], l[nameof(AppStrings.PricingOrAYear), Euros(TeamYearly)],
            [l[nameof(AppStrings.PricingPlanTeamUpgrades)], l[nameof(AppStrings.PricingPlanTeamSkills)], l[nameof(AppStrings.PricingPlanTeamWatched)]], IsHighlighted: true),
        new(l[nameof(AppStrings.PricingPlanCompanyName)], l[nameof(AppStrings.PricingPlanCompanyAudience)], Euros(CompanyMonthly), l[nameof(AppStrings.PricingPerMonth)], l[nameof(AppStrings.PricingOrAYear), Euros(CompanyYearly)],
            [l[nameof(AppStrings.PricingPlanCompanyNoLimit)], l[nameof(AppStrings.PricingPlanCompanyEverything)], l[nameof(AppStrings.PricingPlanCompanyOwn)]])
    ];

    /// <summary>Who pays what, as examples a visitor finds themselves in.</summary>
    public static AppHomeTile[] Examples(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Education, l[nameof(AppStrings.PricingExampleStudentTitle)], l[nameof(AppStrings.PricingExampleStudentText)], Euros(0)),
        new(BitIconName.People, l[nameof(AppStrings.PricingExampleFoundersTitle)], l[nameof(AppStrings.PricingExampleFoundersText)], l[nameof(AppStrings.PricingAMonth), Euros(TeamMonthly)]),
        new(BitIconName.CityNext, l[nameof(AppStrings.PricingExampleCompanyTitle)], l[nameof(AppStrings.PricingExampleCompanyText)], l[nameof(AppStrings.PricingAMonth), Euros(CompanyMonthly)]),
        new(BitIconName.Contact, l[nameof(AppStrings.PricingExampleFreelancerTitle)], l[nameof(AppStrings.PricingExampleFreelancerText)], l[nameof(AppStrings.PricingAMonth), Euros(TeamMonthly)])
    ];

    /// <summary>The example month, row by row: the same work on both sides, and bit Fabric's license as the one row more.</summary>
    public static AppPricingCostLine[] Saving(IStringLocalizer<AppStrings> l) =>
    [
        new("ai", l[nameof(AppStrings.PricingSavingAi)], Euros(ExampleBuilderAi), l[nameof(AppStrings.PricingSavingShareIncluded)], Euros(ExampleOwnAi), l[nameof(AppStrings.PricingSavingAiMakers)]),
        new("hosting", l[nameof(AppStrings.PricingSavingHosting)], Euros(ExampleBuilderHosting), l[nameof(AppStrings.PricingSavingShareIncluded)], Euros(ExampleOwnServer), l[nameof(AppStrings.PricingSavingHost)]),
        new("build", l[nameof(AppStrings.PricingSavingBuild)], null, l[nameof(AppStrings.PricingSavingOnBuilder)], Euros(0), l[nameof(AppStrings.PricingSavingOwnComputer)]),
        new("license", l[nameof(AppStrings.PricingSavingLicense)], null, null, Euros(TeamMonthly), "bit Fabric")
    ];

    public static decimal ExampleBuilderTotal => ExampleBuilderAi + ExampleBuilderHosting;

    public static decimal ExampleFabricTotal => ExampleOwnAi + ExampleOwnServer + TeamMonthly;

    public static decimal ExampleKept => ExampleBuilderTotal - ExampleFabricTotal;

    /// <summary>Why the same work costs less, said under the example month.</summary>
    public static string[] SavingWhy(IStringLocalizer<AppStrings> l) =>
    [
        l[nameof(AppStrings.PricingSavingWhyAi)],
        l[nameof(AppStrings.PricingSavingWhyServer), Euros(ExampleOwnServer)],
        l[nameof(AppStrings.PricingSavingWhyTests)],
        l[nameof(AppStrings.PricingSavingWhyBills)],
        l[nameof(AppStrings.PricingSavingWhyContext)]
    ];

    /// <summary>What a license gives: the right to keep developing.</summary>
    public static AppHomeTile[] License(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.Sync, l[nameof(AppStrings.PricingLicenseUpgradesTitle)], l[nameof(AppStrings.PricingLicenseUpgradesText)]),
        new(BitIconName.Robot, l[nameof(AppStrings.PricingLicenseSkillsTitle)], l[nameof(AppStrings.PricingLicenseSkillsText)]),
        new(BitIconName.Health, l[nameof(AppStrings.PricingLicenseWatchedTitle)], l[nameof(AppStrings.PricingLicenseWatchedText)]),
        new(BitIconName.Unlock, l[nameof(AppStrings.PricingLicenseOpenTitle)], l[nameof(AppStrings.PricingLicenseOpenText)])
    ];

    /// <summary>What stays true whatever the visitor pays.</summary>
    public static AppHomeTile[] Always(IStringLocalizer<AppStrings> localizer) =>
    [
        new(BitIconName.Code, localizer[nameof(AppStrings.PricingAlwaysYoursTitle)], localizer[nameof(AppStrings.PricingAlwaysYoursText)]),
        new(BitIconName.Lock, localizer[nameof(AppStrings.PricingAlwaysNeverSeesTitle)], localizer[nameof(AppStrings.PricingAlwaysNeverSeesText)]),
        new(BitIconName.Robot, localizer[nameof(AppStrings.PricingNoCreditsTitle)], localizer[nameof(AppStrings.PricingNoCreditsText)]),
        new(BitIconName.Server, localizer[nameof(AppStrings.PricingAlwaysKeepsWorkingTitle)], localizer[nameof(AppStrings.PricingAlwaysKeepsWorkingText)])
    ];

    /// <summary>
    /// The visitor's own AI plan, the one cost the home page's calculator doesn't show: its prices as Copilot's and Claude's
    /// sites listed them on 2026-11-19 (bitfabric-internal/.agents/product-marketing.md, Claims). The rest of what they pay others is the
    /// calculator's (See AppHomeCostCalculator.PaidOf).
    /// </summary>
    public static AppHomeTile AiPlan(IStringLocalizer<AppStrings> localizer) =>
        new(BitIconName.Robot, localizer[nameof(AppStrings.PricingAiPlanTitle)], localizer[nameof(AppStrings.PricingAiPlanText)], localizer[nameof(AppStrings.PricingAiPlanValue)]);

    /// <summary>The questions a visitor asks about the price, with answers that are in the page for search engines though closed.</summary>
    public static (string Question, string Answer)[] Questions(IStringLocalizer<AppStrings> l) =>
    [
        (l[nameof(AppStrings.PricingQuestionProject)], l[nameof(AppStrings.PricingAnswerProject)]),
        (l[nameof(AppStrings.PricingQuestionCheaper)], l[nameof(AppStrings.PricingAnswerCheaper), Euros(TeamMonthly), Euros(CompanyMonthly)]),
        (l[nameof(AppStrings.PricingQuestionCantPay)], l[nameof(AppStrings.PricingAnswerCantPay)]),
        (l[nameof(AppStrings.PricingQuestionFreeLicense)], l[nameof(AppStrings.PricingAnswerFreeLicense), TrialDays]),
        (l[nameof(AppStrings.PricingQuestionStop)], l[nameof(AppStrings.PricingAnswerStop)]),
        (l[nameof(AppStrings.PricingQuestionClients)], l[nameof(AppStrings.PricingAnswerClients), Euros(TeamMonthly), Euros(CompanyMonthly)]),
        (l[nameof(AppStrings.PricingQuestionForLife)], l[nameof(AppStrings.PricingAnswerForLife), LifetimeSeats, Euros(LifetimeTeam), Euros(LifetimeCompany)]),
        (l[nameof(AppStrings.PricingQuestionRefund)], l[nameof(AppStrings.PricingAnswerRefund), RefundDays]),
        (l[nameof(AppStrings.PricingQuestionInvoice)], l[nameof(AppStrings.PricingAnswerInvoice)]),
        (l[nameof(AppStrings.PricingQuestionBetaEnds)], l[nameof(AppStrings.PricingAnswerBetaEnds)]),
        (l[nameof(AppStrings.PricingQuestionFree)], l[nameof(AppStrings.PricingAnswerFree)]),
        (l[nameof(AppStrings.PricingQuestionAccount)], l[nameof(AppStrings.PricingAnswerAccount)]),
        (l[nameof(AppStrings.PricingQuestionEarn)], l[nameof(AppStrings.PricingAnswerEarn)])
    ];

    /// <summary>
    /// The page as Markdown, in the order the page shows it, in the localizer's language. <paramref name="pageUrl"/> is the
    /// page's own address and <paramref name="getStartedUrl"/> the wizard's, both whole, as an agent reads the file alone.
    /// </summary>
    public static string Markdown(IStringLocalizer<AppStrings> localizer, string pageUrl, string getStartedUrl)
    {
        var md = new StringBuilder();

        md.Append("# ").AppendLine(localizer[nameof(AppStrings.PricingTitle)]).AppendLine();
        md.Append("> ").AppendLine(localizer[nameof(AppStrings.PricingLead)]).AppendLine();
        md.Append('[').Append(localizer[nameof(AppStrings.PricingPageTitle)]).Append("](").Append(pageUrl).AppendLine(")").AppendLine();

        md.Append("## ").AppendLine(localizer[nameof(AppStrings.PricingPlansTitle)]).AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.PricingPlansNote)]).AppendLine();
        foreach (var plan in Plans(localizer))
        {
            md.Append("### ").Append(plan.Name).Append(": ").Append(plan.Price);
            if (plan.Period is not null)
            {
                md.Append(' ').Append(plan.Period);
            }
            md.AppendLine().AppendLine();
            md.Append(plan.Audience);
            if (plan.Note is not null)
            {
                md.Append(". ").Append(plan.Note);
            }
            md.AppendLine(".").AppendLine();
            foreach (var include in plan.Includes)
            {
                md.Append("- ").AppendLine(include);
            }
            md.AppendLine();
        }

        Section(md, localizer[nameof(AppStrings.PricingExamplesTitle)], localizer[nameof(AppStrings.PricingExamplesIntro)], Examples(localizer));

        md.Append("## ").AppendLine(localizer[nameof(AppStrings.PricingSavingTitle)]).AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.PricingSavingIntro)]).AppendLine();
        md.Append("| | ").Append(localizer[nameof(AppStrings.PricingSavingBuilder)]).Append(" | ").Append(localizer[nameof(AppStrings.PricingSavingFabric)]).AppendLine(" |");
        md.AppendLine("| --- | --- | --- |");
        foreach (var line in Saving(localizer))
        {
            var builder = line.BuilderNote is null ? "–" : $"{line.BuilderAmount ?? localizer[nameof(AppStrings.PricingSavingInItsPrice)].Value} ({line.BuilderNote})";
            md.Append("| ").Append(line.Label).Append(" | ").Append(builder).Append(" | ").Append(line.FabricAmount).Append(" (").Append(line.FabricPaidTo).AppendLine(") |");
        }
        md.Append("| **").Append(localizer[nameof(AppStrings.PricingSavingMonth)]).Append("** | **").Append(Euros(ExampleBuilderTotal)).Append("** | **").Append(Euros(ExampleFabricTotal)).AppendLine("** |").AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.PricingSavingKept), Euros(ExampleKept)]).AppendLine();
        foreach (var why in SavingWhy(localizer))
        {
            md.Append("- ").AppendLine(why);
        }
        md.AppendLine().AppendLine(localizer[nameof(AppStrings.PricingSavingHonest)]).AppendLine();

        Section(md, localizer[nameof(AppStrings.PricingLicenseTitle)], localizer[nameof(AppStrings.PricingLicenseIntro)], License(localizer));
        Section(md, localizer[nameof(AppStrings.PricingAlwaysTitle)], null, Always(localizer));
        Section(md, localizer[nameof(AppStrings.PricingOthersTitle)], localizer[nameof(AppStrings.PricingOthersIntro)], [AiPlan(localizer)]);

        // The rest of the bill is this site's own, as the home page's calculator shows it with what it starts with switched on
        var bill = AppHomeCostCalculator.PaidOf(localizer);
        var perMonth = localizer[nameof(AppStrings.AppHomeCostPerMonth)].Value;
        md.Append("### ").AppendLine(localizer[nameof(AppStrings.AppHomeCostTitle)]).AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.AppHomeCostIntro)]).AppendLine();
        md.Append("**€").Append(bill.Where(item => item.StartsOn).Sum(item => item.Euros)).Append(' ').Append(perMonth).AppendLine("**").AppendLine();
        foreach (var item in bill)
        {
            md.Append("- **").Append(item.Title).Append("**");
            if (item.IsOptional)
            {
                md.Append(" (").Append(localizer[nameof(AppStrings.AppHomeCostOptional)]).Append(')');
            }
            md.Append(": €").Append(item.Euros).Append(' ').Append(perMonth);
            if (item.StartsOn is false)
            {
                md.Append(", ").Append(localizer[nameof(AppStrings.PricingNotInTotal)]);
            }
            md.Append(". ");
            if (item.Spec is not null)
            {
                md.Append(item.Spec).Append(". ");
            }
            md.AppendLine(Literal(item.Text));
        }
        md.AppendLine().AppendLine(Literal(localizer[nameof(AppStrings.AppHomeCostFootnote)])).AppendLine();

        md.Append("#### ").AppendLine(localizer[nameof(AppStrings.AppHomeCostFreeTitle)]).AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.AppHomeCostFreeIntro)]).AppendLine();
        foreach (var (title, text) in AppHomeCostCalculator.FreeOf(localizer))
        {
            md.Append("- **").Append(title).Append("**");
            md.AppendLine(text is null ? string.Empty : $": {text}");
        }
        md.AppendLine().AppendLine(localizer[nameof(AppStrings.AppHomeCostMinimum)]).AppendLine();

        md.Append("## ").AppendLine(localizer[nameof(AppStrings.PricingQuestionsTitle)]).AppendLine();
        foreach (var (question, answer) in Questions(localizer))
        {
            md.Append("### ").AppendLine(question).AppendLine();
            md.AppendLine(answer).AppendLine();
        }

        md.Append("## ").AppendLine(localizer[nameof(AppStrings.AppHomeFinalTitle)]).AppendLine();
        md.AppendLine(localizer[nameof(AppStrings.AppHomeFinalText)]).AppendLine();
        md.Append('[').Append(localizer[nameof(AppStrings.AppHomeGetStarted)]).Append("](").Append(getStartedUrl).AppendLine(")");

        return md.ToString().ReplaceLineEndings("\n");
    }

    /// <summary>A text whose asterisks are its own, such as a footnote's mark, rather than Markdown's emphasis.</summary>
    private static string Literal(string text) => text.Replace("*", @"\*", StringComparison.Ordinal);

    private static void Section(StringBuilder md, string title, string? intro, AppHomeTile[] tiles)
    {
        md.Append("## ").AppendLine(title).AppendLine();
        if (intro is not null)
        {
            md.AppendLine(intro).AppendLine();
        }
        foreach (var tile in tiles)
        {
            md.Append("- **").Append(tile.Title).Append("**");
            if (tile.Value is not null)
            {
                md.Append(": ").Append(tile.Value);
            }
            md.Append(". ").AppendLine(tile.Text);
        }
        md.AppendLine();
    }
}
