namespace Boilerplate.Workbench.Infrastructure.Models;

/// <summary>
/// The parts of the Workbench its header leads to from every one of them (<see cref="WorkbenchSections"/>), marking the one
/// the user is in.
/// </summary>
public enum WorkbenchSection
{
    Home,
    Pages,
    Flows,
    Boards,
    Progress
}

/// <summary>Where each section starts, its icon and its name.</summary>
public static class WorkbenchSections
{
    /// <summary>The sections the header lists, Home left out, as the logo leads there.</summary>
    public static WorkbenchSection[] Listed { get; } = [WorkbenchSection.Pages, WorkbenchSection.Flows, WorkbenchSection.Boards, WorkbenchSection.Progress];

    /// <summary>Pages start on the home page's list of them, which a page's own view leads back to.</summary>
    public static string UrlOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => WorkbenchUrls.HomePages,
        WorkbenchSection.Flows => WorkbenchUrls.Flows,
        WorkbenchSection.Boards => WorkbenchUrls.Boards,
        WorkbenchSection.Progress => WorkbenchUrls.Progress,
        _ => WorkbenchUrls.Home
    };

    public static string IconOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => BitIconName.Design,
        WorkbenchSection.Flows => BitIconName.Flow,
        WorkbenchSection.Boards => BitIconName.ViewDashboard,
        WorkbenchSection.Progress => BitIconName.TaskList,
        _ => BitIconName.Home
    };

    public static string TextKeyOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => nameof(WorkbenchStrings.Pages),
        WorkbenchSection.Flows => nameof(WorkbenchStrings.Flows),
        WorkbenchSection.Boards => nameof(WorkbenchStrings.Boards),
        WorkbenchSection.Progress => nameof(WorkbenchStrings.Progress),
        _ => nameof(WorkbenchStrings.Home)
    };
}
