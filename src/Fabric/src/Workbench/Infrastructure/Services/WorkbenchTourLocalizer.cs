using Bit.Bonboarding;

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// The words of the guides' buttons and announcements, and their steps' texts, which are resource keys. The buttons' words are
/// AppStrings', which the app's own onboarding reads too.
/// </summary>
public sealed class WorkbenchTourLocalizer(IStringLocalizer<WorkbenchStrings> localizer, IStringLocalizer<AppStrings> appLocalizer) : IBonboardingLocalizer
{
    public BonboardingLabels GetLabels(BonboardingTourDefinition? tour) => new()
    {
        Next = appLocalizer[nameof(AppStrings.TourNext)],
        Previous = appLocalizer[nameof(AppStrings.TourBack)],
        Skip = appLocalizer[nameof(AppStrings.TourSkip)],
        Done = appLocalizer[nameof(AppStrings.TourDone)],
        Close = appLocalizer[nameof(AppStrings.Close)],
        DialogLabel = appLocalizer[nameof(AppStrings.TourDialogLabel)],
        ProgressFormat = appLocalizer[nameof(AppStrings.TourProgress)],
        StepAnnouncementFormat = appLocalizer[nameof(AppStrings.TourStepAnnouncement)],
        WaitingAnnouncement = appLocalizer[nameof(AppStrings.TourWaiting)],
        ProgressLabel = appLocalizer[nameof(AppStrings.TourProgressLabel)],
        HintDone = localizer[nameof(WorkbenchStrings.GotIt)]
    };

    public string? Localize(string? text) => text is null ? null : localizer[text];
}
