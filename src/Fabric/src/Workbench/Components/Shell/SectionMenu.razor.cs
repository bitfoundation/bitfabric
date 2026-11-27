namespace Bit.Fabric.Workbench.Components.Shell;

/// <summary>
/// The Workbench's sections as one button in the header's breadcrumb, in every section: it names the section the user is in
/// and opens all four, each with a line on what it is for, with what the agents' tasks wait for on Progress counted on the
/// button and on Progress's item. Its name adds that it holds the sections, as a section's side panel has a toggle named
/// after the section too. Its look is in workbench.scss, as its menu opens outside the component.
/// </summary>
public partial class SectionMenu
{
    private List<BitMenuButtonItem> items = [];

    private static readonly BitMenuButtonClassStyles classes = new()
    {
        Root = "section-switcher",
        Callout = "section-switcher-callout",
        ItemButton = "section-switcher-item",
        ItemDescription = "section-switcher-hint",
        ItemSecondaryText = "section-switcher-count"
    };

    /// <summary>The section the user is in, which the button names and the menu checks.</summary>
    [Parameter] public WorkbenchSection Section { get; set; }

    /// <summary>How many questions and steps the agents' tasks wait for the user on.</summary>
    [Parameter] public int Waiting { get; set; }

    [Parameter] public BitSize? Size { get; set; }

    [Parameter] public string? Class { get; set; }

    private string AriaLabel => Waiting > 0
        ? $"{Localizer[WorkbenchSections.TextKeyOf(Section)]} ({Localizer[nameof(WorkbenchStrings.WorkbenchSections)]}, {Localizer[nameof(WorkbenchStrings.ProgressNeedsYouCount), Waiting]})"
        : $"{Localizer[WorkbenchSections.TextKeyOf(Section)]} ({Localizer[nameof(WorkbenchStrings.WorkbenchSections)]})";

    protected override void OnParametersSet()
    {
        items = [.. WorkbenchSections.All.Select(section => new BitMenuButtonItem
        {
            Key = section.ToString(),
            Text = Localizer[WorkbenchSections.TextKeyOf(section)],
            Description = Localizer[WorkbenchSections.HintKeyOf(section)],
            IconName = WorkbenchSections.IconOf(section),
            Href = section is WorkbenchSection.Home ? WorkbenchUrls.HomeTop : WorkbenchSections.UrlOf(section),
            RadioGroup = "section",
            IsChecked = section == Section,
            SecondaryText = section is WorkbenchSection.Progress && Waiting > 0 ? Waiting.ToString(CultureInfo.CurrentCulture) : null
        })];
    }
}
