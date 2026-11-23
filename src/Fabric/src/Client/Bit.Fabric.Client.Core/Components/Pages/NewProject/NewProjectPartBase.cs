using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject;

/// <summary>
/// Base of the new project page's parts: the state, the answers with their defaults filled in, and the help panel.
/// Each part listens to the state itself, so a change re-renders every part that shows it.
/// </summary>
public partial class NewProjectPartBase : AppComponentBase
{
    /// <summary>The page's state, which the page hands its parts.</summary>
    [CascadingParameter] public NewProjectState? PageState { get; set; }

    protected NewProjectState Store => PageState ?? throw new InvalidOperationException("A part of the new project page is shown outside the page.");

    /// <summary>Whether the page reads right to left.</summary>
    protected static bool IsRtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    /// <summary>The answers as they stand, with the computer guessed from the browser until the visitor picks one.</summary>
    protected NewProjectAnswers Answers => Store.Answers;

    /// <summary>The computer the visitor's browser runs on, or null on a phone or tablet (See NewProjectState.DetectedComputer).</summary>
    protected NewProjectComputer? DetectedComputer => Store.DetectedComputer;

    /// <summary>What the computer choice says under its label: the guess, or that there was nothing to guess from.</summary>
    protected string? ComputerHint => Store.Computer is not null
        ? null
        : DetectedComputer is null
            ? Localizer[nameof(AppStrings.NewProjectComputerPick)]
            : Localizer[nameof(AppStrings.NewProjectComputerGuessed)];

    protected override async Task OnInitAsync()
    {
        Store.Changed += OnStoreChanged;

        await base.OnInitAsync();
    }

    private void OnStoreChanged() => _ = InvokeAsync(StateHasChanged);

    protected void OpenHelp(string topic) => Store.OpenHelp(topic);

    /// <summary>The computer's name as the visitor knows it.</summary>
    protected string ComputerName(NewProjectComputer computer) => computer switch
    {
        NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectMac)],
        NewProjectComputer.Linux => Localizer[nameof(AppStrings.NewProjectLinux)],
        _ => Localizer[nameof(AppStrings.NewProjectWindows)]
    };

    /// <summary>A device's name as the visitor knows it.</summary>
    protected string DeviceName(NewProjectDevice device) => device switch
    {
        NewProjectDevice.Android => Localizer[nameof(AppStrings.NewProjectAndroid)],
        NewProjectDevice.Ios => Localizer[nameof(AppStrings.NewProjectIphoneAndIpad)],
        NewProjectDevice.Windows => Localizer[nameof(AppStrings.NewProjectWindowsApp)],
        _ => Localizer[nameof(AppStrings.NewProjectMacApp)]
    };

    /// <summary>Names one after another, with the comma the language writes: «،» in Persian and Arabic.</summary>
    protected static string ListOf(IEnumerable<string> names) => string.Join(IsRtl ? "، " : ", ", names);

    /// <summary>A number of GB with at most one decimal, in the current culture.</summary>
    protected static string Gb(double value) => value.ToString(value % 1 == 0 ? "0" : "0.#", CultureInfo.CurrentCulture);

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        Store.Changed -= OnStoreChanged;

        await base.DisposeAsync(disposing);
    }
}
