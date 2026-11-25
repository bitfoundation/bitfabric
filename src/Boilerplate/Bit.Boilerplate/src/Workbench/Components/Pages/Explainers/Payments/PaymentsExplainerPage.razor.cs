using System.Globalization;

namespace Boilerplate.Workbench.Components.Pages.Explainers.Payments;

/// <summary>
/// An example explainer (the explainer skill, .github/agents/explainer.agent.md): how an app takes payments, on the web
/// and inside Google Play and the App Store, as a dated snapshot of the providers' and the stores' published fees and
/// rules (PaymentsFacts), with a calculator of what each one keeps from a sale. It is written in English and has no
/// data to load.
/// </summary>
public partial class PaymentsExplainerPage
{
    private const string OneOffKind = "one-off";
    private const string SubscriptionKind = "subscription";

    // Its figures are in euros and written the English way, as the explainer is.
    private static readonly CultureInfo english = CultureInfo.GetCultureInfo("en");

    // Per component, as the choice group keeps its own state on its items.
    private readonly BitChoiceGroupItem<string>[] saleKinds =
    [
        new() { Value = OneOffKind, Text = "One-off" },
        new() { Value = SubscriptionKind, Text = "Subscription" }
    ];

    [AutoInject] private PaymentsExplainerStore store = default!;

    private decimal Price => (decimal)Math.Clamp(store.Price, 1, 100);

    /// <summary>Each provider's fee on the sale the reader set, in the order of the facts.</summary>
    private IReadOnlyList<(PaymentsFee Fee, decimal Amount)> Rows => [.. PaymentsFacts.Fees.Select(fee => (fee, fee.FeeOf(Price, store.IsSubscription)))];

    /// <summary>Who keeps the least and who the most, in a sentence.</summary>
    private string Summary
    {
        get
        {
            var rows = Rows;
            var least = rows.Min(r => r.Amount);
            var most = rows.Max(r => r.Amount);
            var cheapest = rows.Where(r => r.Amount == least).Select(r => r.Fee.InSentence ?? r.Fee.Name).ToArray();
            var dearest = rows.Where(r => r.Amount == most).Select(r => r.Fee.InSentence ?? r.Fee.Name).ToArray();
            var sale = store.IsSubscription ? "subscription payment" : "sale";

            return $"On a {Money(Price)} {sale}, {string.Join(" and ", cheapest)} {(cheapest.Length > 1 ? "keep" : "keeps")} the least, {Money(least)}, and {string.Join(" and ", dearest)} the most, {Money(most)}.";
        }
    }

    protected override void OnInitialized()
    {
        Subscribe(store);
    }

    /// <summary>A bar's length, to scale with the largest fee on the sale.</summary>
    private string Width(decimal amount)
    {
        var most = Rows.Max(r => r.Amount);

        return string.Create(CultureInfo.InvariantCulture, $"{(most == 0 ? 0 : amount / most * 100):0.##}%");
    }

    private static string Money(decimal amount) => string.Create(english, $"€{amount:0.00}");

    private string Share(decimal amount) => string.Create(english, $"{amount / Price * 100:0.#}%");
}
