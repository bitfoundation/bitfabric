namespace Bit.Fabric.Workbench.Infrastructure.Models;

/// <summary>
/// The parts of the Workbench, which the header's switcher leads to from every one of them (<see cref="WorkbenchSections"/>),
/// naming the one the user is in.
/// </summary>
public enum WorkbenchSection
{
    Home,
    Pages,
    Boards,
    Progress
}

/// <summary>Where each section starts, its icon and its name.</summary>
public static class WorkbenchSections
{
    /// <summary>Every section, in the order the header's switcher lists them.</summary>
    public static WorkbenchSection[] All { get; } = [WorkbenchSection.Home, WorkbenchSection.Pages, WorkbenchSection.Boards, WorkbenchSection.Progress];

    /// <summary>Pages start on the first page, the theme and its components.</summary>
    public static string UrlOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => WorkbenchUrls.Pages,
        WorkbenchSection.Boards => WorkbenchUrls.Boards,
        WorkbenchSection.Progress => WorkbenchUrls.Progress,
        _ => WorkbenchUrls.Home
    };

    public static string IconOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => BitIconName.Design,
        WorkbenchSection.Boards => BitIconName.ViewDashboard,
        WorkbenchSection.Progress => BitIconName.TimelineProgress,
        _ => BitIconName.Home
    };

    public static string TextKeyOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => nameof(WorkbenchStrings.Pages),
        WorkbenchSection.Boards => nameof(WorkbenchStrings.Boards),
        WorkbenchSection.Progress => nameof(WorkbenchStrings.Progress),
        _ => nameof(WorkbenchStrings.Home)
    };

    /// <summary>A line on what the section is for, under its name in the header's switcher.</summary>
    public static string HintKeyOf(WorkbenchSection section) => section switch
    {
        WorkbenchSection.Pages => nameof(WorkbenchStrings.SectionPagesHint),
        WorkbenchSection.Boards => nameof(WorkbenchStrings.SectionBoardsHint),
        WorkbenchSection.Progress => nameof(WorkbenchStrings.SectionProgressHint),
        _ => nameof(WorkbenchStrings.SectionHomeHint)
    };
}
