namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>What an amount buys: nothing said, a month, a year, one payment, or for good.</summary>
public enum PricingPeriod
{
    None,
    Month,
    Year,
    Once,
    Forever
}

/// <summary>An amount in euros, excluding VAT, for a period; "from" when it is where a contract starts.</summary>
public sealed record PricingAmount(decimal Euros, PricingPeriod Period, bool IsFrom = false);

/// <summary>Where a plan's button leads: the wizard, an email to the team, or nowhere yet (payments aren't built).</summary>
public enum PricingAction
{
    Start,
    Contact,
    Choose
}

/// <summary>A plan as its card shows it. <see cref="YearlyPrice"/> is shown instead of <see cref="Price"/> when yearly billing is picked.</summary>
public sealed record PricingPlan(string Key, string Name, string Audience, PricingAmount Price)
{
    public PricingAmount? YearlyPrice { get; init; }

    /// <summary>A short line under the price, such as "By contract and invoice".</summary>
    public string? PriceNote { get; init; }

    public string[] Includes { get; init; } = [];

    /// <summary>The tag on a highlighted plan, such as "Recommended"; null for none.</summary>
    public string? Highlight { get; init; }

    public PricingAction Action { get; init; } = PricingAction.Choose;

    public string ActionText { get; init; } = string.Empty;

    /// <summary>True for the founding membership, whose card counts the seats left.</summary>
    public bool CountsSeats { get; init; }
}

/// <summary>A tile of a band: an icon, a title, an optional amount and one line.</summary>
public sealed record PricingTile(string Icon, string Title, string Text, string? Value = null);

public sealed record PricingQuestion(string Question, string Answer);

/// <summary>A row of the small comparison table under the plans.</summary>
public sealed record PricingRow(string Label, string[] Values);

/// <summary>The small second offer under the plans: its title, one line, and a table.</summary>
public sealed record PricingAlternative(string Title, string Text, string[] Headers, PricingRow[] Rows);

/// <summary>Everything one strategy's page says, built from its prices (See the *Pricing classes next to this one).</summary>
public sealed record PricingStrategy(string Key, string Title, string Lead)
{
    /// <summary>The line above the plans: who and what is priced, and that prices exclude VAT.</summary>
    public string Basis { get; init; } = string.Empty;

    public PricingPlan[] Plans { get; init; } = [];

    /// <summary>Who pays what, as examples a visitor finds themselves in.</summary>
    public PricingTile[] Samples { get; init; } = [];

    /// <summary>A band of its own under the plans, such as what paying changes; null for none.</summary>
    public (string Title, string Text, PricingTile[] Tiles)? Extra { get; init; }

    public PricingAlternative? Alternative { get; init; }

    /// <summary>A compact line right under the plan cards, in the style of the cloud row, such as the freelancers' price.</summary>
    public PricingTile? UnderPlans { get; init; }

    /// <summary>A highlighted line under the plans, such as the pilot: a title, a line and its button's text.</summary>
    public (string Title, string Text, string Action)? Offer { get; init; }

    /// <summary>The "Once your app is live" row's first cell, or null where the row doesn't show.</summary>
    public PricingTile? CloudIncluded { get; init; }

    public PricingQuestion[] Questions { get; init; } = [];

    public bool HasYearly => Plans.Any(plan => plan.YearlyPrice is not null);
}
