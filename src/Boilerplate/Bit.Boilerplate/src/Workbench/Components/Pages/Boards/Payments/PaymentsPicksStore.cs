namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// The providers the reader picks on the payments board to set up, by name, in the order they picked them. Named
/// board-{the board's id}, it is written to the board's file each time it saves, where the agent reads the picks, and its
/// Submit bar keeps them there as final (the boards skill). It holds the picks alone, as everything in it counts as one.
/// </summary>
[BlexStore(Name = "board-payments", Persist = true)]
public partial class PaymentsPicksStore
{
    [BlexState] private string[] _providers = [];

    [BlexAction] private void OnToggleProvider(string name) => Providers = Providers.Contains(name) ? [.. Providers.Where(provider => provider != name)] : [.. Providers, name];
}
