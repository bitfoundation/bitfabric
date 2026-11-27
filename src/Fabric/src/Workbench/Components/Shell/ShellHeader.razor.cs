namespace Bit.Fabric.Workbench.Components.Shell;

/// <summary>
/// The header of every section, the same everywhere: the toggle of the section's side panel, the logo, which leads home,
/// the section switcher (<see cref="SectionMenu"/>) with what waits for the user on Progress counted on it, the open item's
/// title, the section's own buttons, then the Workbench's language, sound and theme. A desktop folds the side panel away
/// with its toggle; a smaller screen opens it over the page.
/// </summary>
public partial class ShellHeader
{
    private bool isNarrow;

    [AutoInject] private BitThemeManager bitThemeManager = default!;
    [AutoInject] private WorkbenchReviewSession reviewSession = default!;
    [AutoInject] private ILogger<ShellHeader> logger = default!;

    /// <summary>The section the page belongs to.</summary>
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>The open item's title, after the section in the breadcrumb: a page, a board or a task.</summary>
    [Parameter] public string? Title { get; set; }

    /// <summary>What follows the title, such as a page's review status.</summary>
    [Parameter] public RenderFragment? TitleExtra { get; set; }

    /// <summary>The section's own buttons, before the Workbench's.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    /// <summary>What comes after the Workbench's buttons, such as the toggle of a page's tools column on the other side.</summary>
    [Parameter] public RenderFragment? End { get; set; }

    /// <summary>Whether the section has a side panel (<see cref="SectionSidebar"/>), which the header's first button toggles.</summary>
    [Parameter] public bool HasSidePanel { get; set; }

    [Parameter] public string? Class { get; set; }

    private bool IsSidePanelShown => isNarrow ? WorkbenchStore.IsSidePanelOpen : WorkbenchStore.IsNavigatorCollapsed is false;

    /// <summary>A pane on the start side, with an arrow that points the way it will move.</summary>
    private string SideToggleIcon => IsSidePanelShown
        ? (CurrentDir is BitDir.Rtl ? BitIconName.ClosePane : BitIconName.ClosePaneMirrored)
        : (CurrentDir is BitDir.Rtl ? BitIconName.OpenPane : BitIconName.OpenPaneMirrored);

    protected override void OnInitialized()
    {
        // A panel opened over the page below a desktop belongs to the page it was opened on, not to the section the
        // switcher or a link leads to.
        if (WorkbenchStore.IsSidePanelOpen)
        {
            WorkbenchStore.CloseSidePanel();
        }

        Subscribe(WorkbenchStore);
        reviewSession.Changed += OnReviewChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender is false)
            return;

        // What waits for the user is known once the review document is open, which opening it doesn't announce.
        try
        {
            await reviewSession.Open(Localizer[nameof(WorkbenchStrings.Reviewer)]);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "The review document could not be opened for the header.");
            return;
        }

        if (CurrentCancellationToken.IsCancellationRequested is false)
        {
            StateHasChanged();
        }
    }

    private void OnNarrowChanged(bool matched)
    {
        isNarrow = matched;

        // A panel opened over the page on a smaller screen doesn't stay open on a wider one, which shows it as a column.
        if (matched is false && WorkbenchStore.IsSidePanelOpen)
        {
            WorkbenchStore.CloseSidePanel();
        }
    }

    private void ToggleSidePanel()
    {
        if (isNarrow)
        {
            WorkbenchStore.ToggleSidePanel();
        }
        else
        {
            WorkbenchStore.ToggleNavigator();
        }
    }

    private void OnReviewChanged() => InvokeAsync(StateHasChanged);

    public override async ValueTask DisposeAsync()
    {
        reviewSession.Changed -= OnReviewChanged;

        await base.DisposeAsync();
    }

    /// <summary>Switches the Workbench's own theme; each view keeps the theme of its scenario.</summary>
    private async Task ToggleTheme()
    {
        await bitThemeManager.ToggleDarkLightAsync();
    }
}
