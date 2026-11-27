namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// What the reader sets on the payments board: the calculator's price and kind of sale, the region the stores' terms are
/// shown for and the providers they pick to set up. It is saved, so a reload shows the board as it was left, and, named
/// board-{the board's id}, it is written to the board's file too, where the agent reads the picks (WorkbenchBoardFiles).
/// </summary>
[BlexStore(Name = "board-payments", Persist = true)]
public partial class PaymentsBoardStore
{
    [BlexState] private double _price = 10;
    [BlexState] private bool _isSubscription;
    // The region's key; null until the reader picks one, which shows the first.
    [BlexState] private string? _region;
    // The providers the reader picked to set up, by name, in the order they picked them.
    [BlexState] private string[] _providers = [];

    [BlexAction] private void OnSetPrice(double price) => Price = price;

    [BlexAction] private void OnSetSubscription(bool isSubscription) => IsSubscription = isSubscription;

    [BlexAction] private void OnPickRegion(string? region) => Region = region;

    [BlexAction] private void OnToggleProvider(string name) => Providers = Providers.Contains(name) ? [.. Providers.Where(provider => provider != name)] : [.. Providers, name];
}
