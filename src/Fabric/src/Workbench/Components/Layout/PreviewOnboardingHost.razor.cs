namespace Bit.Fabric.Workbench.Components.Layout;

/// <summary>
/// Holds the app's home page onboarding in a view, as the app's layout does (AppOnboardingHost). The tour's steps name the
/// app's addresses for the home page and the wizard, and Bonboarding goes to a step's address and waits until the page is
/// there; here those pages are the view's previews, so the tour is handed those instead, in the view's settings.
/// </summary>
public partial class PreviewOnboardingHost
{
    [AutoInject] private HomeOnboardingService homeOnboarding = default!;

    private Type? hostType;
    private Dictionary<string, object?>? parameters;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        homeOnboarding.HostChanged += OnHostChanged;
        Show();
    }

    private void OnHostChanged() => _ = InvokeAsync(() =>
    {
        Show();
        StateHasChanged();
    });

    private void Show()
    {
        if (homeOnboarding.HostType is not { } type || hostType == type)
            return;

        parameters = new(homeOnboarding.HostParameters ?? new Dictionary<string, object?>())
        {
            [nameof(AppHomeOnboarding.HomeUrl)] = SitePageHost.PreviewOf(SitePageHost.HomeKey, Scenario, PaneId),
            [nameof(AppHomeOnboarding.GetStartedUrl)] = SitePageHost.PreviewOf(SitePageHost.NewProjectKey, Scenario, PaneId)
        };
        hostType = type;
    }

    public override async ValueTask DisposeAsync()
    {
        homeOnboarding.HostChanged -= OnHostChanged;

        await base.DisposeAsync();
    }
}
