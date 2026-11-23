using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// The groups a project's parts are shown in. Each part's title, icon and what it proves come from the app's own report,
/// in its own words (bitfabric-internal/specs/monitoring.md, Each part's category, name, icon and description come from
/// the report), so bit Fabric keeps no catalog of the template's checks: it only names the six categories the template
/// defines, in the viewer's language, and shows a category the app made up as it is.
/// </summary>
public static class ProjectChecks
{
    /// <summary>
    /// The template's categories in the order a project's page shows them, then "Other". <c>Title</c> and <c>About</c>
    /// are <see cref="AppStrings"/> keys; "Other" has no <c>About</c>.
    /// </summary>
    public static readonly (string Name, string Title, string About)[] Groups =
    [
        ("Platform", nameof(AppStrings.MonitoringGroupPlatform), nameof(AppStrings.MonitoringGroupPlatformAbout)),
        ("Messaging", nameof(AppStrings.MonitoringGroupMessaging), nameof(AppStrings.MonitoringGroupMessagingAbout)),
        ("Identity", nameof(AppStrings.MonitoringGroupIdentity), nameof(AppStrings.MonitoringGroupIdentityAbout)),
        ("AI", nameof(AppStrings.MonitoringGroupAI), nameof(AppStrings.MonitoringGroupAIAbout)),
        ("Integrations", nameof(AppStrings.MonitoringGroupIntegrations), nameof(AppStrings.MonitoringGroupIntegrationsAbout)),
        ("Websites", nameof(AppStrings.MonitoringGroupWebsites), nameof(AppStrings.MonitoringGroupWebsitesAbout)),
        ("Other", nameof(AppStrings.MonitoringGroupOther), "")
    ];

    /// <summary>The group a part shows in: its category, or "Other" for a part without one.</summary>
    public static string GroupOf(ProjectCheckDto check)
        => string.IsNullOrWhiteSpace(check.Category) ? "Other" : check.Category.Trim();

    /// <summary>The <see cref="AppStrings"/> key a group shows with; a category the app made up shows as it is.</summary>
    public static string TitleOf(string group)
        => Groups.FirstOrDefault(known => known.Name == group).Title ?? group;

    public static string AboutOf(string group)
        => Groups.FirstOrDefault(known => known.Name == group).About ?? "";

    /// <summary>The template's icon of a category (See HealthCheckCategories.IconOf), the same one the app's own page draws.</summary>
    public static string IconOf(string group) => HealthCheckCategories.IconOf(group);

    /// <summary>Where a group goes on the page: the template's in order, then the app's own, then "Other".</summary>
    public static int OrderOf(string group)
        => Array.FindIndex(Groups, known => known.Name == group) is var index and >= 0 ? index * 2 : (Groups.Length - 1) * 2 - 1;

    /// <summary>Approving bit Fabric in the app again, with whoever signs in there: the add page, the app's address looked for at once.</summary>
    public static string ReconnectHref(ProjectDto project)
        => $"{PageUrls.AddProject}?address={Uri.EscapeDataString(Uri.TryCreate(project.Address, UriKind.Absolute, out var address) ? address.Authority : project.Address ?? "")}";

    /// <summary>What the app calls a part; its report key when the app says nothing.</summary>
    public static string TitleOf(ProjectCheckDto check)
        => string.IsNullOrWhiteSpace(check.Title) ? check.Key ?? "?" : check.Title;
}
