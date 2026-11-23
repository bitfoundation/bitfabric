using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// What the stand-ins of bit Fabric's monitoring controllers share besides <see cref="WorkbenchStandIn"/>: the sample world
/// they read and change, and who the view is signed in as.
/// </summary>
public abstract class MonitoringStandIn(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : WorkbenchStandIn(scenarioState, "monitoring")
{
    protected MonitoringSampleWorld World => world;

    /// <summary>Who the view is signed in as, the way the server names the author of an update.</summary>
    protected async Task<string> CurrentUserName()
    {
        var user = (await authenticationStateProvider.GetAuthenticationStateAsync()).User;
        return user.IsAuthenticated() ? user.GetDisplayName() : "?";
    }
}
