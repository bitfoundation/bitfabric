using Bit.Bonboarding;

namespace Bit.Fabric.Workbench.Infrastructure.Services.StandIns;

/// <summary>
/// Where the tours keep their progress here. A view's tours keep it in memory, per view, and never in the browser, which every
/// view of a Workbench shares. The app's home page shows its onboarding on a visitor's first visit only, and the app tells one
/// from the tour's record in the browser; a view of it is the visit its Visit switch names instead, so two views side by side
/// can be a first visit and a returning visitor at once, and a reload starts the first visit over, as a design view keeps
/// nothing. The Workbench's own guides, whose ids start with <see cref="WorkbenchGuidePrefix"/>, are the Workbench's rather
/// than a view's, so the browser keeps theirs, as Bonboarding does by default: a guide shown once, like the Progress page's,
/// stays put away after a reload. The Workbench's tour keeps no progress at all (Persist = false).
/// </summary>
public sealed class WorkbenchOnboardingStore(WorkbenchScenarioState scenarioState, BonboardingBrowserStore browser) : IBonboardingStore, IBonboardingStoreEvents
{
    public const string WorkbenchGuidePrefix = "workbench-";

    private readonly BonboardingInMemoryStore records = new();

    // What another tab of the Workbench did with one of its guides, so a guide put away there stops here too.
    public event EventHandler<BonboardingStoreChangedEventArgs>? Changed
    {
        add => browser.Changed += value;
        remove => browser.Changed -= value;
    }

    public async ValueTask<BonboardingRecord?> GetAsync(string tourId, CancellationToken cancellationToken = default)
    {
        if (IsWorkbenchGuide(tourId))
            return await browser.GetAsync(tourId, cancellationToken);

        if (await records.GetAsync(tourId, cancellationToken) is { } record)
            return record;

        // A returning visitor's browser holds the finished tour, so the home page doesn't show it again.
        if (tourId is AppHomeOnboarding.Id && scenarioState.Scenario.GetSwitch(HomeHost.VisitSwitch) is not HomeHost.FirstVisit)
            return new() { TourId = tourId, Version = AppHomeOnboarding.Version, Completed = true, CompletedAt = DateTimeOffset.UnixEpoch, UpdatedAt = DateTimeOffset.UnixEpoch };

        return null;
    }

    public ValueTask SetAsync(BonboardingRecord record, CancellationToken cancellationToken = default) => IsWorkbenchGuide(record.TourId) ? browser.SetAsync(record, cancellationToken) : records.SetAsync(record, cancellationToken);

    public ValueTask RemoveAsync(string tourId, CancellationToken cancellationToken = default) => IsWorkbenchGuide(tourId) ? browser.RemoveAsync(tourId, cancellationToken) : records.RemoveAsync(tourId, cancellationToken);

    // A view's "show the tours again" forgets that view's tours only, never the Workbench's own guides.
    public ValueTask ClearAsync(CancellationToken cancellationToken = default) => records.ClearAsync(cancellationToken);

    private static bool IsWorkbenchGuide(string tourId) => tourId.StartsWith(WorkbenchGuidePrefix, StringComparison.Ordinal);
}
