namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry;

/// <summary>
/// What each person changes on a project's telemetry dashboard: the environment and the period, the groups they folded,
/// the area they filtered to, the cards' order, sizes (1 small, 2 wide, 4 full width) and the ones they hid, the cards and
/// alerts they deleted or paused, whether the tip about their AI is open, and whether they are rearranging. Saved, so it
/// stays as they left it.
/// </summary>
[BlexStore(Name = "project-telemetry", Persist = true, Version = 1)]
public partial class ProjectTelemetryStore
{
    [BlexState] private string _environment = "Production";
    [BlexState] private string _range = "24h";
    [BlexState] private string _area = "";
    [BlexState] private bool _isTipClosed;
    [BlexState] private List<string> _closedGroups = [];
    [BlexState] private List<string> _order = [];
    [BlexState] private List<string> _hidden = [];
    [BlexState] private List<string> _deleted = [];
    [BlexState] private List<string> _paused = [];
    [BlexState] private Dictionary<string, int> _sizes = [];
    [BlexState(Persist = false)] private bool _isEditing;

    [BlexAction] private void OnSetEnvironment(string environment) => Environment = environment;

    [BlexAction] private void OnSetRange(string range) => Range = range;

    [BlexAction] private void OnSetArea(string area) => Area = area;

    [BlexAction] private void OnToggleTip() => IsTipClosed = IsTipClosed is false;

    [BlexAction]
    private void OnToggleGroup(string key) => ClosedGroups = ClosedGroups.Contains(key) ? [.. ClosedGroups.Where(k => k != key)] : [.. ClosedGroups, key];

    [BlexAction] private void OnSetOrder(List<string> order) => Order = order;

    [BlexAction] private void OnHide(string key) => Hidden = [.. Hidden.Where(k => k != key), key];

    [BlexAction] private void OnShow(string key) => Hidden = [.. Hidden.Where(k => k != key)];

    [BlexAction] private void OnDelete(string key) => Deleted = [.. Deleted.Where(k => k != key), key];

    [BlexAction] private void OnTogglePaused(string key) => Paused = Paused.Contains(key) ? [.. Paused.Where(k => k != key)] : [.. Paused, key];

    [BlexAction] private void OnSetSize(string key, int size) => Sizes = new(Sizes) { [key] = size };

    [BlexAction] private void OnToggleEditing() => IsEditing = IsEditing is false;

    /// <summary>Puts the layout back as it came: order, sizes, hidden cards and folded groups. What was deleted stays deleted.</summary>
    [BlexAction]
    private void OnReset()
    {
        Order = [];
        Hidden = [];
        Sizes = [];
        ClosedGroups = [];
    }
}
