namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// What the reader sets on the payments board: the calculator's price and kind of sale, and the region the stores'
/// terms are shown for. It is saved, so a reload shows the board as it was left.
/// </summary>
[BlexStore(Name = "board-payments", Persist = true)]
public partial class PaymentsBoardStore
{
    [BlexState] private double _price = 10;
    [BlexState] private bool _isSubscription;
    // The region's key; null until the reader picks one, which shows the first.
    [BlexState] private string? _region;

    [BlexAction] private void OnSetPrice(double price) => Price = price;

    [BlexAction] private void OnSetSubscription(bool isSubscription) => IsSubscription = isSubscription;

    [BlexAction] private void OnPickRegion(string? region) => Region = region;
}
