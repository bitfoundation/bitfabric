using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring;

/// <summary>
/// Hosts the app's projects page: the team's projects as cards, some in every state or an agency's fourteen (the Projects
/// switch, which the projects stand-in reads), a user without a team yet who creates one (the Team switch), and, with the
/// view signed out, what signing in gives. Empty is a team with no project yet.
/// </summary>
public partial class ProjectsHost
{
    public const string AmountSwitch = Data.WorkbenchProjectsController.AmountSwitch;

    public const string TeamSwitch = "team";

    public const string NoTeam = "none";

    private Task<AuthenticationState>? noTeamState;
    private int pageKey;

    /// <summary>The view's user without a team, while the Team switch says so and the page hasn't created one.</summary>
    private Task<AuthenticationState>? NoTeamState => Scenario.GetSwitch(TeamSwitch) is NoTeam && World.IsNewTeam is false ? noTeamState : null;

    protected override async Task OnBeforeAppPage()
    {
        var user = (await AuthenticationStateTask).User;

        noTeamState = Task.FromResult(new AuthenticationState(new ClaimsPrincipal(user.Identities.Select(identity => new ClaimsIdentity(
            identity.Claims.Where(claim => claim.Type != AppClaimTypes.TENANT_ID), identity.AuthenticationType, identity.NameClaimType, identity.RoleClaimType)))));

        World.TeamCreated += OnTeamCreated;
    }

    /// <summary>The page created the team: the new team's page replaces it, with no project yet.</summary>
    private void OnTeamCreated()
    {
        pageKey++;
        _ = InvokeAsync(StateHasChanged);
    }

    public override async ValueTask DisposeAsync()
    {
        World.TeamCreated -= OnTeamCreated;

        await base.DisposeAsync();
    }
}
