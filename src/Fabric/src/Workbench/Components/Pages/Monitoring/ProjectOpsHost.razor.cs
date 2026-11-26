namespace Bit.Fabric.Workbench.Components.Pages.Monitoring;

/// <summary>
/// Hosts the app's project page: the project the Project switch picks, each in a state of its own, on the tab the Tab
/// switch picks. A tab opened on the page becomes the view's Tab switch, so a reload and a screenshot keep it.
/// </summary>
public partial class ProjectOpsHost
{
    public const string ProjectSwitch = "project";

    public const string TabSwitch = "tab";

    /// <summary>The one part History and Performance show, as the map opens them on it (See ProjectTabLink).</summary>
    public const string CheckSwitch = "check";

    public const string DefaultProject = "yourapp";

    public const string PressSwitch = "press";

    /// <summary>
    /// The states of a tab a press of one of its buttons brings: the tab it's on and the button, found by a selector of the
    /// app's own markup.
    /// </summary>
    public static readonly Dictionary<string, (string Tab, string Selector)> Presses = new()
    {
        ["test-alert"] = ("alerts", ".alerts .actions button:last-of-type"),
        ["credits"] = ("sla", "section[aria-labelledby='credits-title'] button"),
        ["disconnect"] = ("settings", "section[aria-labelledby='connection-title'] .actions button"),
        ["remove"] = ("settings", "section[aria-labelledby='remove-title'] .actions button")
    };

    private bool isPressed;

    /// <summary>The project page's tabs, as its address names them (See ProjectPage), Overview first.</summary>
    public static readonly string[] Tabs = ["overview", "map", "history", "incidents", "performance", "scans", "sla", "locations", "status", "alerts", "settings"];

    /// <summary>The picked project, or Your app; a project the view added on the add page counts too, while the view lasts.</summary>
    private Guid MonitoringId => (World.Find(Scenario.GetSwitch(ProjectSwitch) ?? DefaultProject) ?? World.Find(DefaultProject))?.Id ?? Guid.Empty;

    private (string Tab, string Selector)? PressedButton => Scenario.GetSwitch(PressSwitch) is { } press && Presses.TryGetValue(press, out var pressed) ? pressed : null;

    /// <summary>The tab the Tab switch picks, or the one the Press switch's button is on.</summary>
    private string? Tab => PressedButton?.Tab ?? (Scenario.GetSwitch(TabSwitch) is { } tab && tab != Tabs[0] && Tabs.Contains(tab) ? tab : null);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (IsReady is false || isPressed || PressedButton is not { } press)
            return;

        isPressed = true;
        await Press(press.Selector);
    }

    protected override IReadOnlyDictionary<string, object?> QueryOfSwitches() => new Dictionary<string, object?>
    {
        ["tab"] = Tab,
        ["check"] = Tab is "history" or "performance" ? Scenario.GetSwitch(CheckSwitch) : null
    };

    /// <summary>A tab the page opened becomes the Tab switch, and leaves behind a press made on another tab.</summary>
    protected override WorkbenchScenario SwitchesOfQuery(WorkbenchScenario scenario, IReadOnlyDictionary<string, string> query)
    {
        var tab = query.GetValueOrDefault("tab") is { } opened && Tabs.Contains(opened) && opened != Tabs[0] ? opened : null;
        var updated = scenario.WithSwitch(TabSwitch, tab)
                              .WithSwitch(CheckSwitch, tab is "history" or "performance" ? query.GetValueOrDefault("check") : null);

        return PressedButton is { } press && press.Tab != tab ? updated.WithSwitch(PressSwitch, null) : updated;
    }
}
