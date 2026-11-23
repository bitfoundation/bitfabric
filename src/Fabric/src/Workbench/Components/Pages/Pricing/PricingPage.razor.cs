using Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing;

/// <summary>
/// bitfabric.co/pricing in four business strategies, one design option each (bitfabric-internal/.agents/gtm/README.md).
/// Every strategy's prices are in its own class under Data, and the parts all of them share are in <see cref="PricingShared"/>.
/// Billing is the page's switch, so the in-page Monthly and Yearly buttons change the view's switch, and a link or a
/// screenshot shows the one picked. The founding members' seats left are the one thing the page loads, as the server
/// would count them, so the data states reach that counter.
/// </summary>
public partial class PricingPage
{
    public const string PageKey = "pricing";

    public const string BillingSwitch = "billing";

    public const string Monthly = "monthly";

    public const string Yearly = "yearly";

    private const string ContactUrl = "mailto:info@bitplatform.dev";

    private int? seatsLeft;
    private bool isLoadingSeats = true;

    /// <summary>The design option of the view: one of the strategies' keys, the first when none is picked.</summary>
    private string VariantKey => WorkbenchPageCatalog.Find(PageKey)?.ResolveVariant(Scenario)?.Key ?? ProjectPricing.Key;

    /// <summary>Built on each render, so an edit to a price shows with hot reload.</summary>
    private PricingStrategy Strategy => VariantKey switch
    {
        CompanyPricing.Key => CompanyPricing.Build(Localizer),
        AgencyPricing.Key => AgencyPricing.Build(Localizer),
        MemberPricing.Key => MemberPricing.Build(Localizer),
        _ => ProjectPricing.Build(Localizer)
    };

    private bool IsYearly => Scenario.GetSwitch(BillingSwitch) == Yearly;

    /// <summary>The design of "Where a month's money goes", picked with the page's Saving switch: row by row when none is picked.</summary>
    private string? SavingView => Scenario.GetSwitch(PricingSavings.Switch);

    /// <summary>The hero's title, a sentence a line.</summary>
    private string[] TitleSentences
    {
        get
        {
            var parts = Strategy.Title.Split(". ", StringSplitOptions.RemoveEmptyEntries);

            return [.. parts.Select((part, index) => index < parts.Length - 1 ? part + "." : part)];
        }
    }

    /// <summary>The months a yearly payment saves, when every plan with a yearly price saves the same.</summary>
    private int? MonthsOff
    {
        get
        {
            var months = Strategy.Plans.Where(plan => plan.YearlyPrice is not null)
                                       .Select(plan => PricingFormat.MonthsOff(plan.Price, plan.YearlyPrice))
                                       .Distinct()
                                       .ToArray();

            return months is [{ } only] ? only : null;
        }
    }

    // The hero's title is the page's heading: an h2 under the app's header, which names the page in the h1, and the h1 without it.
    private int TitleLevel => Scenario.Chrome ? 2 : 1;

    private string BandHeading => $"h{TitleLevel + 1}";

    private int CardLevel => TitleLevel + 2;

    private string CardHeading => $"h{CardLevel}";

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        if (IsPrerendering)
            return;

        if (VariantKey == MemberPricing.Key)
        {
            await LoadSeats();
        }
    }

    /// <summary>The founding seats still free; null when the count couldn't be read, and the card then says nothing of it.</summary>
    private async Task LoadSeats()
    {
        try
        {
            seatsLeft = await ScenarioState.Load("founding-seats", () => MemberPricing.SampleSeatsLeft, () => 0, CurrentCancellationToken);
        }
        catch (WorkbenchSimulatedException)
        {
            seatsLeft = null;
        }
        catch (OperationCanceledException)
        {
            return;
        }

        isLoadingSeats = false;
    }

    private PricingTile[] CloudTiles(PricingTile included) => [included, PricingShared.Sms(Localizer)];

    /// <summary>Monthly or yearly, picked on the page: the view's switch follows, monthly being its default.</summary>
    private async Task SetBilling(string? key)
    {
        var yearly = key == Yearly;

        if (yearly == IsYearly)
            return;

        await ReportScenario(Scenario.WithSwitch(BillingSwitch, yearly ? Yearly : null));
    }
}
