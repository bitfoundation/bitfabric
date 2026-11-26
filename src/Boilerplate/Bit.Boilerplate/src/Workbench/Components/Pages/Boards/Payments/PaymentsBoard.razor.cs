using System.Globalization;

namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// An example board (the boards skill, .github/agents/boards.agent.md): how an app takes payments, on the web and inside
/// Google Play and the App Store, as a dated snapshot of the providers' and the stores' published fees and rules
/// (PaymentsFacts), with a calculator of what each one keeps from a sale. It is written in English and has no data to load.
/// </summary>
public partial class PaymentsBoard
{
    private const string OneOffKind = "one-off";
    private const string SubscriptionKind = "subscription";

    // The routes' bars are drawn on a scale of 0 to 30%, the most a store takes.
    private const decimal ScaleTop = 30;

    // Its figures are in euros and written the English way, as the board is.
    private static readonly CultureInfo english = CultureInfo.GetCultureInfo("en");

    // Per component, as the choice groups keep their own state on their items.
    private readonly BitChoiceGroupItem<string>[] saleKinds =
    [
        new() { Value = OneOffKind, Text = "One-off" },
        new() { Value = SubscriptionKind, Text = "Subscription" }
    ];

    private readonly BitChoiceGroupItem<string>[] regionItems = [.. PaymentsFacts.Regions.Select(region => new BitChoiceGroupItem<string> { Value = region.Key, Text = region.Name })];

    [AutoInject] private PaymentsBoardStore store = default!;

    private decimal Price => (decimal)Math.Clamp(store.Price, 1, 100);

    /// <summary>The region the stores' terms are shown for: the one picked, or the first.</summary>
    private PaymentsRegion Region => PaymentsFacts.Regions.FirstOrDefault(region => region.Key == store.Region) ?? PaymentsFacts.Regions[0];

    /// <summary>Each provider's fee on the sale the reader set, in the order of the facts.</summary>
    private IReadOnlyList<(PaymentsFee Fee, decimal Amount)> Rows => [.. PaymentsFacts.Fees.Select(fee => (fee, fee.FeeOf(Price, store.IsSubscription)))];

    /// <summary>What a €10 sale leaves you on the web, with the cheapest card fee, and in the stores, beside the title.</summary>
    private static BoardNumber[] Kept
    {
        get
        {
            var web = PaymentsFacts.Fees.Where(fee => fee.IsStore is false).MinBy(fee => fee.FeeOf(10, false))!;
            var store = PaymentsFacts.Fees.Where(fee => fee.IsStore).MaxBy(fee => fee.FeeOf(10, false))!;

            return
            [
                new(Money(10 - web.FeeOf(10, false)), $"of a €10 sale stays with you on the web, with {web.Name}"),
                new(Money(10 - store.FeeOf(10, false)), "in the stores, at their small-business rate")
            ];
        }
    }

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

    /// <summary>Where a route's share starts and ends on the 0 to 30% scale; a route that takes nothing gets a dot at 0.</summary>
    private static string RangeStyle(PaymentsRoute route) =>
        string.Create(CultureInfo.InvariantCulture, $"inset-inline-start:{route.Low / ScaleTop * 100:0.##}%;width:{Math.Max(route.High - route.Low, 0.6m) / ScaleTop * 100:0.##}%");

    private static (string StoreName, PaymentsTerm Term)[] Terms(PaymentsWay way) => [("Google Play", way.GooglePlay), ("App Store", way.AppStore)];

    private static string AllowedIcon(PaymentsAllowed allowed) => allowed switch
    {
        PaymentsAllowed.Yes => BitIconName.CheckMark,
        PaymentsAllowed.Limited => BitIconName.Info,
        _ => BitIconName.Cancel
    };

    private static string Money(decimal amount) => string.Create(english, $"€{amount:0.00}");

    private string Share(decimal amount) => string.Create(english, $"{amount / Price * 100:0.#}%");
}
