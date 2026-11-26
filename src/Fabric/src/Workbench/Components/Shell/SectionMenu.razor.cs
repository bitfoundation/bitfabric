namespace Bit.Fabric.Workbench.Components.Shell;

/// <summary>
/// The Workbench's sections as one button that names the section the user is in and opens all of them, Home included,
/// with the count of what the agents' tasks wait for on Progress: the header's on a phone, and the breadcrumb's in a page's
/// own view, whose toolbar has no room for <see cref="SectionNav"/>. Its name adds that it holds the sections, as a page's
/// view already has a button named Pages, the one that folds its list of pages.
/// </summary>
public partial class SectionMenu
{
    private List<BitMenuButtonItem> items = [];

    private static readonly BitMenuButtonClassStyles styles = new() { Callout = "--bit-MenuButton-callout-min-width: 12rem" };

    /// <summary>The section the user is in, which the button names and the menu checks.</summary>
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>How many questions and steps the agents' tasks wait for the user on.</summary>
    [Parameter] public int Waiting { get; set; }

    [Parameter] public BitSize? Size { get; set; }

    [Parameter] public string? Class { get; set; }

    protected override void OnParametersSet()
    {
        items = [.. new[] { WorkbenchSection.Home }.Concat(WorkbenchSections.Listed).Select(section => new BitMenuButtonItem
        {
            Key = section.ToString(),
            Text = Localizer[WorkbenchSections.TextKeyOf(section)],
            IconName = WorkbenchSections.IconOf(section),
            Href = section is WorkbenchSection.Home ? WorkbenchUrls.HomeTop : WorkbenchSections.UrlOf(section),
            RadioGroup = "section",
            IsChecked = section == Section,
            SecondaryText = section is WorkbenchSection.Progress && Waiting > 0 ? Waiting.ToString(CultureInfo.CurrentCulture) : null
        })];
    }
}
