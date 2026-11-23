using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

/// <summary>
/// The comparison of the home page and of <c>/contents</c>: a hosted builder, an empty project and bit Fabric, row by row, then
/// the promise it leads to. Used as it is by the app and by its design in the Workbench, so it needs nothing of either's base
/// class.
/// </summary>
public partial class AppHomeComparison
{
    [Inject] private IStringLocalizer<AppStrings> Localizer { get; set; } = default!;

    /// <summary>The id of the band title that names the table.</summary>
    [Parameter] public string LabelledBy { get; set; } = "ah-compare-title";

    /// <summary>The columns, in the order of each row's cells.</summary>
    private string[] Columns =>
    [
        Localizer[nameof(AppStrings.AppHomeCompareHostedBuilder)],
        Localizer[nameof(AppStrings.AppHomeCompareEmptyProject)],
        "bit Fabric"
    ];

    /// <summary>
    /// The two edges bit Fabric sits between: an empty project keeps the work private but
    /// spends tokens, days and security on what every app needs, and a hosted builder starts full featured but has the
    /// prompts, the code and the users. Beyond its basic sign-in, a hosted builder's AI writes the security as the visitor
    /// prompts, so it fares there as an empty project does. bit Fabric is full featured and
    /// never has any of it, so it has nothing to promise. Every claim stays within the say/don't-say table of
    /// bitfabric-internal/specs/product.md, and no competitor is named.
    /// </summary>
    private AppHomeComparisonRow[] Rows =>
    [
        new(Localizer[nameof(AppStrings.AppHomeCompareOnDayOne)],
        [
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareHostedOnDayOne)]),
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareEmptyOnDayOne)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricOnDayOne)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareSecurity)],
        [
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareHostedSecurity)]),
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareEmptySecurity)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricSecurity)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareYourCode)],
        [
            (AppHomeMark.Partly, Localizer[nameof(AppStrings.AppHomeCompareHostedCode)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareEmptyCode)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricCode)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareYourPrompts)],
        [
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareHostedPrompts)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareOnlyToYourAI)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareOnlyToYourAI)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareYourUsers)],
        [
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareHostedUsers)]),
            (AppHomeMark.Partly, Localizer[nameof(AppStrings.AppHomeCompareEmptyUsers)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricUsers)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareTraining)],
        [
            (AppHomeMark.Partly, Localizer[nameof(AppStrings.AppHomeCompareHostedTraining)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareEmptyTraining)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricTraining)])
        ]),
        new(Localizer[nameof(AppStrings.AppHomeCompareWhatYouPay)],
        [
            (AppHomeMark.Bad, Localizer[nameof(AppStrings.AppHomeCompareHostedPay)]),
            (AppHomeMark.Partly, Localizer[nameof(AppStrings.AppHomeCompareEmptyPay)]),
            (AppHomeMark.Good, Localizer[nameof(AppStrings.AppHomeCompareFabricPay)])
        ])
    ];

    private static string IconOf(AppHomeMark mark) => mark switch
    {
        AppHomeMark.Good => BitIconName.CheckMark,
        AppHomeMark.Partly => BitIconName.Warning,
        _ => BitIconName.Cancel
    };

    private static BitColor ColorOf(AppHomeMark mark) => mark switch
    {
        AppHomeMark.Good => BitColor.Success,
        AppHomeMark.Partly => BitColor.SevereWarning,
        _ => BitColor.Error
    };
}
