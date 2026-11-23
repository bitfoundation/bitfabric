using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

/// <summary>
/// A short film of the whole way, played on a drawing of the visitor's computer (the user's review, 2026-11-07): the line
/// copied on bitfabric.co, the terminal opened, the line pasted, Windows' permission given, then the Workbench, the landing
/// page and the app opening by themselves, VS Code with a first prompt typed, and what that prompt builds: on their own computer, unseen by us and theirs to keep (the user's review, the same day). The persona has never opened a terminal,
/// so they see it done before they do it. Everything in it is HTML in the page's theme, so its words are translated and it
/// follows light, dark and the computer picked; it stays schematic on purpose, like the home page's picture of the product.
/// It moves on by itself and can be paused or stepped through; the steps under it say the same in words.
/// </summary>
public partial class NewProjectWalkthrough
{
    private const int TickMilliseconds = 100;

    private sealed record Scene(string Key, int Milliseconds);

    private static readonly Scene[] windowsScenes =
    [
        new("copy", 3200), new("start", 2600), new("search", 3400), new("paste-click", 3200), new("enter", 3000),
        new("allow", 3200), new("workbench", 3000), new("landing", 3000), new("app", 3200), new("prompt", 5600), new("yours", 7000)
    ];

    private static readonly Scene[] macScenes =
    [
        new("copy", 3200), new("spotlight", 2400), new("search", 3000), new("paste-keys", 3000), new("enter", 3000),
        new("workbench", 3000), new("landing", 3000), new("app", 3200), new("prompt", 5600), new("yours", 7000)
    ];

    private static readonly Scene[] linuxScenes =
    [
        new("copy", 3200), new("open", 2800), new("paste-keys", 3000), new("enter", 3000),
        new("workbench", 3000), new("landing", 3000), new("app", 3200), new("prompt", 5600), new("yours", 7000)
    ];

    [Parameter] public NewProjectComputer Computer { get; set; }

    /// <summary>The line as it appears copied and pasted in the drawing.</summary>
    [Parameter] public string Line { get; set; } = string.Empty;

    /// <summary>The product's name, which the app in the drawing carries.</summary>
    [Parameter] public string Name { get; set; } = string.Empty;

    private int index;
    private int elapsed;
    private bool isPaused;
    private NewProjectComputer shownFor;

    private Scene[] Scenes => Computer switch
    {
        NewProjectComputer.Windows => windowsScenes,
        NewProjectComputer.MacOs => macScenes,
        _ => linuxScenes
    };

    private Scene Current => Scenes[Math.Min(index, Scenes.Length - 1)];

    private string Os => Computer switch
    {
        NewProjectComputer.Windows => "windows",
        NewProjectComputer.MacOs => "mac",
        _ => "linux"
    };

    private string Initial => string.IsNullOrEmpty(Name) ? "M" : Name[..1].ToUpperInvariant();

    protected override Task OnParamsSetAsync()
    {
        // Another computer is another film, from its start
        if (Computer != shownFor)
        {
            shownFor = Computer;
            index = 0;
            elapsed = 0;
        }

        return base.OnParamsSetAsync();
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        _ = Play(CurrentCancellationToken);
    }

    /// <summary>Moves on to the next scene when the current one has played, until the component goes away.</summary>
    private async Task Play(CancellationToken cancellationToken)
    {
        try
        {
            while (cancellationToken.IsCancellationRequested is false)
            {
                await Task.Delay(TickMilliseconds, cancellationToken);

                if (isPaused)
                    continue;

                elapsed += TickMilliseconds;

                if (elapsed < Current.Milliseconds)
                    continue;

                elapsed = 0;
                index = (index + 1) % Scenes.Length;
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void GoTo(int scene)
    {
        index = (scene + Scenes.Length) % Scenes.Length;
        elapsed = 0;
    }

    private void TogglePause() => isPaused = isPaused is false;

    /// <summary>What the scene shows, in words, under the drawing.</summary>
    private string Caption(string key) => key switch
    {
        "copy" => Localizer[nameof(AppStrings.NewProjectFilmCopy)],
        "start" => Localizer[nameof(AppStrings.NewProjectFilmStart)],
        "search" when Computer is NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectFilmSearchMac)],
        "search" => Localizer[nameof(AppStrings.NewProjectFilmSearchWindows)],
        "spotlight" => Localizer[nameof(AppStrings.NewProjectFilmSpotlight)],
        "open" => Localizer[nameof(AppStrings.NewProjectFilmOpenLinux)],
        "paste-click" => Localizer[nameof(AppStrings.NewProjectFilmPasteClick)],
        "paste-keys" when Computer is NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectFilmPasteMac)],
        "paste-keys" => Localizer[nameof(AppStrings.NewProjectFilmPasteKeys)],
        "enter" when Computer is NewProjectComputer.MacOs => Localizer[nameof(AppStrings.NewProjectFilmEnterMac)],
        "enter" => Localizer[nameof(AppStrings.NewProjectFilmEnter)],
        "allow" => Localizer[nameof(AppStrings.NewProjectFilmAllow)],
        "workbench" => Localizer[nameof(AppStrings.NewProjectFilmWorkbenchOpens)],
        "landing" => Localizer[nameof(AppStrings.NewProjectFilmLandingOpens)],
        "app" => Localizer[nameof(AppStrings.NewProjectFilmAppOpens)],
        "prompt" => Localizer[nameof(AppStrings.NewProjectFilmPromptCaption)],
        _ => Localizer[nameof(AppStrings.NewProjectFilmYoursCaption)]
    };
}
