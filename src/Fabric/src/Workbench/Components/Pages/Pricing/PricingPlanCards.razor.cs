using Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing;

/// <summary>
/// The plans of a strategy as cards: the name, who it is for, the price for the billing picked, a button and what it
/// includes. A plan with a yearly price shows it on yearly billing, with the other one under it; the founding membership
/// counts its seats left.
/// </summary>
public partial class PricingPlanCards
{
    [Parameter] public PricingPlan[] Plans { get; set; } = [];

    [Parameter] public bool IsYearly { get; set; }

    [Parameter] public string HeadingElement { get; set; } = "h3";

    /// <summary>Where a plan sold by contract leads.</summary>
    [Parameter] public string? ContactUrl { get; set; }

    /// <summary>The founding seats left; null when the count couldn't be read.</summary>
    [Parameter] public int? SeatsLeft { get; set; }

    [Parameter] public bool IsLoadingSeats { get; set; }

    private PricingAmount AmountOf(PricingPlan plan) => IsYearly && plan.YearlyPrice is { } yearly ? yearly : plan.Price;

    private string? PeriodOf(PricingAmount amount) => amount.Period switch
    {
        PricingPeriod.Month => Localizer["a month"].Value,
        PricingPeriod.Year => Localizer["a year"].Value,
        PricingPeriod.Once => Localizer["once"].Value,
        PricingPeriod.Forever => Localizer["forever"].Value,
        _ => null
    };

    /// <summary>The lines under the price: the other way to pay for it, then the plan's own note.</summary>
    private string[] NotesOf(PricingPlan plan)
    {
        List<string> notes = [];

        if (plan.YearlyPrice is { } yearly)
        {
            if (IsYearly)
            {
                notes.Add(Localizer["Or {0} a month, paid monthly", PricingFormat.Euros(plan.Price.Euros)]);
            }
            else if (PricingFormat.MonthsOff(plan.Price, yearly) is { } months)
            {
                notes.Add(Localizer["Or {0} a year, {1} months off", PricingFormat.Euros(yearly.Euros), months]);
            }
            else
            {
                notes.Add(Localizer["Or {0} a year", PricingFormat.Euros(yearly.Euros)]);
            }
        }

        if (plan.PriceNote is { } note)
        {
            notes.Add(note);
        }

        return [.. notes];
    }

    private string? HrefOf(PricingPlan plan) => plan.Action switch
    {
        PricingAction.Start => PageUrls.NewProject,
        PricingAction.Contact => ContactUrl,
        _ => null
    };

    /// <summary>False only for the founding membership once every seat is taken.</summary>
    private bool IsOpen(PricingPlan plan) => plan.CountsSeats is false || IsLoadingSeats || SeatsLeft is not 0;
}
