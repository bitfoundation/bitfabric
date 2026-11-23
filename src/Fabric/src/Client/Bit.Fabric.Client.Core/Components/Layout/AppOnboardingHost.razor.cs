namespace Bit.Fabric.Client.Core.Components.Layout;

public partial class AppOnboardingHost
{
    [AutoInject] private HomeOnboardingService homeOnboarding = default!;

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        homeOnboarding.HostChanged += OnHostChanged;
    }

    private void OnHostChanged() => _ = InvokeAsync(StateHasChanged);

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        homeOnboarding.HostChanged -= OnHostChanged;

        await base.DisposeAsync(disposing);
    }
}
