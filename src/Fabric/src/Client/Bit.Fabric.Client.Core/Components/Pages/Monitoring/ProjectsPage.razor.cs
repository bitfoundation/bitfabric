using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Bit.Fabric.Shared.Features.Projects;
using Bit.Fabric.Shared.Features.Projects.Dtos;
using Bit.Fabric.Shared.Features.Tenants;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// The current team's projects (bitfabric-internal/specs/monitoring.md, The projects page): a card each, those needing
/// someone first, drawn from its Production environment with its other environments on it; and a Licenses tab with each
/// project's license (A card per project, its environments on it, and a Licenses tab).
/// </summary>
public partial class ProjectsPage
{
    private bool isLoading = true;
    private bool hasTeam;
    private bool isCreatingTeam;
    private string? teamName;
    private List<ProjectDto> projects = [];

    [AutoInject] private IProjectsController projectsController = default!;
    [AutoInject] private ITenantController tenantController = default!;

    /// <summary>The open tab, in the address so it can be shared: the cards, or <c>licenses</c>.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "tab")] public string? Tab { get; set; }

    private const string ProjectsTab = "projects";
    private const string LicensesTab = "licenses";

    private string SelectedTab => Tab is LicensesTab ? LicensesTab : ProjectsTab;

    private string SignInUrl => $"{CultureInfoManager.GetCurrentCultureUrl(PageUrls.SignIn)}?return-url={Uri.EscapeDataString(PageUrls.Projects)}";

    private int NeedsAttention => projects.Count(NeedsYou);

    /// <summary>More cards than fit a screen, as an agency watching its customers' apps has.</summary>
    private const int SearchFrom = 6;

    private bool onlyAttention;
    private string? search;

    /// <summary>A project needs someone when one of its environments does.</summary>
    private static bool NeedsYou(ProjectDto project) => project.Monitorings.Any(environment => environment.Health is ProjectHealth.Down or ProjectHealth.Degraded
                                                                                               || environment.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded);

    /// <summary>The environment a project's card draws: the one needing someone, else its Production, else its first.</summary>
    private static MonitoringDto? CardOf(ProjectDto project)
        => project.Monitorings.FirstOrDefault(environment => environment.Health is ProjectHealth.Down)
           ?? project.Monitorings.FirstOrDefault(environment => environment.Health is ProjectHealth.Degraded || environment.ConnectionStatus is ProjectConnectionStatus.ReconnectNeeded)
           ?? project.Monitorings.FirstOrDefault();

    /// <summary>The cards the filter and the search leave, by the project's name or an environment's name or address.</summary>
    private IEnumerable<ProjectDto> Visible() => projects.Where(project =>
        (onlyAttention is false || NeedsYou(project))
        && (string.IsNullOrWhiteSpace(search)
            || project.Name?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) is true
            || project.Monitorings.Any(environment => environment.Environment?.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase) is true
                                                      || environment.Address?.Contains(search.Trim(), StringComparison.OrdinalIgnoreCase) is true)));

    private void ShowAll()
    {
        onlyAttention = false;
        search = null;
    }

    private void OpenTab(string? key)
        => NavigationManager.NavigateTo(NavigationManager.GetUriWithQueryParameter("tab", key is LicensesTab ? LicensesTab : null), replace: true);

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        var user = (await AuthenticationStateTask).User;

        if (user.IsAuthenticated() is false)
        {
            isLoading = false;
            return;
        }

        hasTeam = user.GetTenantId() is not null;

        if (hasTeam is false)
        {
            teamName = Localizer[nameof(AppStrings.MonitoringUsersTeam), user.GetDisplayName()];
            isLoading = false;
            return;
        }

        try
        {
            // The cards' days run from the viewer's midnights (bitfabric-internal/specs/monitoring.md, Times follow the
            // viewer's time zone).
            projects = await projectsController.GetProjects(TimeZoneService.CurrentTimeZone.Id, CurrentCancellationToken);
        }
        finally
        {
            isLoading = false;
        }
    }

    private async Task CreateTeam()
    {
        if (isCreatingTeam || string.IsNullOrWhiteSpace(teamName))
            return;

        isCreatingTeam = true;

        try
        {
            var team = await tenantController.Create(new() { Name = TeamSlug(teamName), Title = teamName.Trim() }, CurrentCancellationToken);

            if (await AuthManager.SwitchTenant(team.Id, CurrentCancellationToken))
            {
                // A soft restart, so everything tenant-scoped is rebuilt in the new team, this page included.
                PubSubService.Publish(ClientAppMessages.SOFT_RESTART);
            }
        }
        finally
        {
            isCreatingTeam = false;
        }
    }

    /// <summary>
    /// The team's sub domain name (TenantDto.NAME_REGEX_PATTERN): its title in lowercase letters and digits, with a random
    /// tail, since the name is unique across every team and nobody chose it.
    /// </summary>
    private static string TeamSlug(string title)
    {
        var letters = new string([.. title.ToLowerInvariant().Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')).Take(40)]);
        return $"{(letters.Length is 0 ? "team" : letters)}-{Guid.NewGuid().ToString("N")[..8]}";
    }
}
