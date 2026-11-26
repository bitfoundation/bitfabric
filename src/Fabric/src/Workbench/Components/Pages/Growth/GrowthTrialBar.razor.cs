using Bit.Fabric.Workbench.Components.Pages.Growth.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Growth;

/// <summary>
/// Every Trial bit Fabric ever gave, by where it is now: licensed, still running, ending within a week, or ended without a
/// License. A part too narrow for its share prints none; the legend gives every count.
/// </summary>
public partial class GrowthTrialBar
{
    [Parameter, EditorRequired] public GrowthReport Report { get; set; } = default!;

    private sealed record Part(string Name, int Count, string Tone);

    private Part[] Parts =>
    [
        new(Localizer[nameof(WorkbenchStrings.GrowthStateLicensed)], Report.TrialsLicensed, "licensed"),
        new(Localizer[nameof(WorkbenchStrings.GrowthStateRunning)], Report.TrialsRunning, "running"),
        new(Localizer[nameof(WorkbenchStrings.GrowthStateEnding)], Report.TrialsEndingThisWeek, "ending"),
        new(Localizer[nameof(WorkbenchStrings.GrowthStateEnded)], Report.TrialsEndedUnlicensed, "ended")
    ];

    private double Share(Part part) => Report.Trials is 0 ? 0 : part.Count / (double)Report.Trials;

    private string AccessibleName => string.Join(", ", Parts.Select(part => $"{part.Name}: {GrowthFormat.Count(part.Count)} ({GrowthFormat.Share(Share(part))})"));
}
