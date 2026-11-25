namespace Boilerplate.Workbench.Components.Pages.Explainers.Payments;

/// <summary>
/// What the reader sets on the payments explainer: the calculator's price and kind of sale, and the region the stores'
/// rules are shown for. It is persisted per view, so a reload or a screenshot shows the page as it was left.
/// </summary>
[BlexStore(Name = "explainer-payments", Persist = true)]
public partial class PaymentsExplainerStore
{
    [BlexState] private double _price = 10;
    [BlexState] private bool _isSubscription;
    // The region's key; null until the reader picks one, which shows the first.
    [BlexState] private string? _region;

    [BlexAction] private void OnSetPrice(double price) => Price = price;

    [BlexAction] private void OnSetSubscription(bool isSubscription) => IsSubscription = isSubscription;

    [BlexAction] private void OnPickRegion(string? region) => Region = region;
}
