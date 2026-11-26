namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// The header of the Workbench's sections: the logo, which leads home, the sections with the one the user is in marked
/// (<see cref="SectionNav"/>, or <see cref="SectionMenu"/> on a phone), the page's own buttons, the agents' progress with what
/// waits for the user, the Workbench's language, its sound and its own theme. A page's own view has a toolbar of its own,
/// with <see cref="SectionMenu"/> in its breadcrumb.
/// </summary>
public partial class ShellHeader
{
    [AutoInject] private BitThemeManager bitThemeManager = default!;
    [AutoInject] private WorkbenchReviewSession reviewSession = default!;
    [AutoInject] private ILogger<ShellHeader> logger = default!;

    /// <summary>The section the page belongs to.</summary>
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>The page's own buttons, before the Workbench's.</summary>
    [Parameter] public RenderFragment? Actions { get; set; }

    protected override void OnInitialized()
    {
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
