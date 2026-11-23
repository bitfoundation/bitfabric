using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>What a part of a month's bill is, which sets its color in every design.</summary>
public enum PricingSavingKind
{
    /// <summary>The AI that does the building.</summary>
    Ai,

    /// <summary>The server the app runs on, with its database.</summary>
    Hosting,

    /// <summary>Building the app, its previews and its automated tests.</summary>
    Build,

    /// <summary>What a hosted builder adds on top of what it buys for you.</summary>
    Share,

    /// <summary>bit Fabric's license.</summary>
    License,

    /// <summary>What the customer no longer pays.</summary>
    Kept
}

/// <summary>A part of a month's bill: what it is, what it costs, and who is paid for it.</summary>
public sealed record PricingSavingPart(string Label, decimal Euros, PricingSavingKind Kind, string PaidTo);

/// <summary>One way to build, as one bar: its parts and their total.</summary>
public sealed record PricingSavingBar(string Name, PricingSavingPart[] Parts)
{
    /// <summary>What is paid; what is kept isn't.</summary>
    public decimal Total => Parts.Where(part => part.Kind is not PricingSavingKind.Kept).Sum(part => part.Euros);
}

/// <summary>
/// A row of the row-by-row and table designs: the same piece of work on both sides. <see cref="BuilderNote"/> null means the
/// builder's side has no such row (the license); <see cref="BuilderEuros"/> null with a note means it's inside the builder's
/// price with no amount of its own (builds and tests).
/// </summary>
public sealed record PricingCostLine(string Label, PricingSavingKind Kind, decimal? BuilderEuros, string? BuilderNote, decimal FabricEuros, string FabricPaidTo);

/// <summary>
/// "Where a month's money goes": a hosted builder buys the AI and the hosting for you and adds its own share on each; with bit
/// Fabric each bill comes straight from whoever does the work, the tests run on the computer the customer already has, and the
/// license is the one line that's ours. Four designs of the same month, picked with the page's "Saving" switch, one of them
/// with the same rows on both sides and bit Fabric's card a row longer for its license.
/// </summary>
public static class PricingSavings
{
    public const string Switch = "saving";

    public const string Rows = "rows";

    public const string Bills = "bills";

    public const string Table = "table";

    public const string Bars = "bars";

    // An example month for a team of two building a real app. Example amounts, not measurements: the builder's share is an
    // example of 20% on the AI and on the hosting, which no hosted builder publishes, so the public site needs real numbers,
    // or the word "example" on the band, before it uses them. A builder's builds and tests are inside its price, so they get
    // no amount of their own. The AI plan is Claude Max's $100 a month, which comes with Claude Code (claude.com/pricing, read
    // 2026-11-20). The server is Contabo's Cloud VPS 4, 4 cores and 8 GB, at €4.40 a month with VAT on a 24-month term
    // (contabo.com/en/vps, read 2026-11-20). The tests run on the computer the customer already has: never say or imply they
    // need to buy one.
    public const decimal BuilderAi = 200;
    public const decimal BuilderAiShare = 40;
    public const decimal BuilderHosting = 25;
    public const decimal BuilderHostingShare = 5;

    public const decimal OwnAiPlan = 100;
    public const decimal OwnServer = 4.40m;

    public static PricingSavingBar Builder(IStringLocalizer l) => new(l["On a hosted builder"],
    [
        new(l["The AI"], BuilderAi, PricingSavingKind.Ai, l["The builder"]),
        new(l["The builder's share on the AI"], BuilderAiShare, PricingSavingKind.Share, l["The builder"]),
        new(l["Hosting and the database"], BuilderHosting, PricingSavingKind.Hosting, l["The builder"]),
        new(l["The builder's share on hosting"], BuilderHostingShare, PricingSavingKind.Share, l["The builder"])
    ]);

    public static PricingSavingBar Fabric(IStringLocalizer l)
    {
        var builder = Builder(l).Total;
        var paid = OwnAiPlan + OwnServer + ProjectPricing.TeamMonthly;

        return new(l["With bit Fabric"],
        [
            new(l["Your AI plan"], OwnAiPlan, PricingSavingKind.Ai, l["The AI's maker: Anthropic, OpenAI or Google"]),
            new(l["Your server"], OwnServer, PricingSavingKind.Hosting, l["Your host, Contabo for example"]),
            new(l["bit Fabric's license"], ProjectPricing.TeamMonthly, PricingSavingKind.License, "bit Fabric"),
            new(l["What you keep"], builder - paid, PricingSavingKind.Kept, string.Empty)
        ]);
    }

    /// <summary>The same month row by row: the same work on both sides, and bit Fabric's license as the one row more.</summary>
    public static PricingCostLine[] Lines(IStringLocalizer l) =>
    [
        new(l["The AI"], PricingSavingKind.Ai, BuilderAi + BuilderAiShare, l["The builder, its share included"], OwnAiPlan, l["Anthropic, OpenAI or Google"]),
        new(l["Hosting and the database"], PricingSavingKind.Hosting, BuilderHosting + BuilderHostingShare, l["The builder, its share included"], OwnServer, l["Your host, Contabo for example"]),
        new(l["Builds, previews and automated tests"], PricingSavingKind.Build, null, l["On the builder's servers"], 0, l["The computer you already have, and GitHub's free plan"]),
        new(l["bit Fabric's license"], PricingSavingKind.License, null, null, ProjectPricing.TeamMonthly, "bit Fabric")
    ];

    /// <summary>The bills with nothing to pay, shown in the bills design: what runs on the computer you have, and GitHub.</summary>
    public static (string Label, string PaidTo)[] FreeLines(IStringLocalizer l) =>
    [
        (l["Your tests and previews"], l["The computer you already have"]),
        (l["Your code and its deployment"], l["GitHub, on its free plan"])
    ];

    public static string Title(IStringLocalizer l) => l["Where a month's money goes"];

    public static string Lead(IStringLocalizer l) => l["An example month for a team of two building a real app."];

    /// <summary>Why the same work costs less, said under every design.</summary>
    public static string[] Why(IStringLocalizer l) =>
    [
        l["Your AI plan comes straight from its maker, and a plan goes much further than the same money spent on credits."],
        l["Your server comes straight from a host, from {0} a month, with nobody's share on top.", Euros(OwnServer)],
        l["Your tests run on the computer you already have, so they cost nothing extra."],
        l["Every bill comes from whoever does the work, so you see what each part costs."]
    ];

    /// <summary>When a hosted builder is the cheaper pick, said plainly.</summary>
    public static string Honest(IStringLocalizer l) => l["Making a landing page or a small, simple app? A hosted builder costs less for that, and it's the better pick."];
}
