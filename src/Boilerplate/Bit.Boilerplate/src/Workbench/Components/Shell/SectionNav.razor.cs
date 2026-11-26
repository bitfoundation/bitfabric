namespace Boilerplate.Workbench.Components.Shell;

/// <summary>
/// The Workbench's sections as a bar of links in the header, with the one the user is in marked and the count of what the
/// agents' tasks wait for on Progress. A phone gets <see cref="SectionMenu"/> instead.
/// </summary>
public partial class SectionNav
{
    private List<BitNavBarItem> items = [];

    /// <summary>The section the user is in; Home marks none, as the logo stands for it.</summary>
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>How many questions and steps the agents' tasks wait for the user on.</summary>
    [Parameter] public int Waiting { get; set; }

    [Parameter] public string? Class { get; set; }

    private string? SelectedKey => Section is WorkbenchSection.Home ? null : Section.ToString();

    // Room between an item's icon and its text for the count on Progress's icon.
    private static readonly BitNavBarClassStyles styles = new() { ItemText = "margin-inline-start: 0.375rem" };

    protected override void OnParametersSet()
    {
        // Built once and changed in place, as the bar keeps its selected item by reference and a new list would clear it.
        if (items.Count == 0)
        {
            items = [.. WorkbenchSections.Listed.Select(section => new BitNavBarItem
            {
                Key = section.ToString(),
                Text = Localizer[WorkbenchSections.TextKeyOf(section)],
                IconName = WorkbenchSections.IconOf(section),
                Url = WorkbenchSections.UrlOf(section)
            })];
        }

        var progress = items.Single(item => item.Key == nameof(WorkbenchSection.Progress));

        progress.Badge = Waiting > 0 ? Waiting.ToString(CultureInfo.CurrentCulture) : null;
        progress.BadgeAriaLabel = Waiting > 0 ? Localizer[nameof(WorkbenchStrings.ProgressNeedsYouCount), Waiting] : null;
    }
}
