using Bit.Fabric.Shared.Features.Growth;

namespace Bit.Fabric.Client.Core.Components.Pages.Management.Growth;

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
        new(Localizer[nameof(AppStrings.GrowthStateLicensed)], Report.TrialsLicensed, "licensed"),
        new(Localizer[nameof(AppStrings.GrowthStateRunning)], Report.TrialsRunning, "running"),
        new(Localizer[nameof(AppStrings.GrowthStateEnding)], Report.TrialsEndingThisWeek, "ending"),
        new(Localizer[nameof(AppStrings.GrowthStateEnded)], Report.TrialsEndedUnlicensed, "ended")
    ];

    private double Share(Part part) => Report.Trials is 0 ? 0 : part.Count / (double)Report.Trials;

    private string AccessibleName => string.Join(", ", Parts.Select(part => $"{part.Name}: {GrowthFormat.Count(part.Count)} ({GrowthFormat.Share(Share(part))})"));
}
