namespace Bit.Fabric.Client.Films.Components;

/// <summary>
/// A feature's film, in the home page's dialog: its scenes play by themselves once on a drawn device, with a caption under
/// each, and can be paused, replayed or stepped through. At its end it stops on Replay, and on Learn more when the feature
/// has a page that shows it live (<see cref="MoreUrl"/>). Two-step sign-in's was the first one, and the model for
/// every feature (bitfabric-internal/specs/home-page.md, 2026-11-07).
/// </summary>
public partial class AppHomeFilm
{
    private const int TickMilliseconds = 100;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter, EditorRequired] public AppHomeFilmScene[] Scenes { get; set; } = default!;

    [Parameter] public EventCallback OnClose { get; set; }

    /// <summary>
    /// A sign-in provider's logo by its name (google, apple, github, twitter, facebook), drawn by whoever plays the film: they
    /// are the app's own sign-in icons in Client.Core, which this library can't reference, and one copy of them is enough.
    /// </summary>
    [Parameter] public RenderFragment<string>? Logo { get; set; }

    /// <summary>Where the feature can be seen live, offered when the film ends.</summary>
    [Parameter] public string? MoreUrl { get; set; }

    private int index;
    private int elapsed;
    private bool isPaused;
    private bool isEnded;

    private AppHomeFilmScene Current => Scenes[Math.Min(index, Scenes.Length - 1)];

    /// <summary>A phone or a tablet is taller than a screen, so a film with one keeps its height from scene to scene.</summary>
    private bool IsTall => Scenes.Any(s => s.Device is AppHomeDevice.Phone or AppHomeDevice.Tablet);

    /// <summary>A phone in a film that has a tablet too is drawn smaller, so the two are told apart at a glance.</summary>
    private bool HasTablet => Scenes.Any(s => s.Device is AppHomeDevice.Tablet) && Scenes.Any(s => s.Device is AppHomeDevice.Phone);

    private AppHomeFilmPart[] Flow => [.. Current.Parts.Where(p => p is not AppHomeFilmOverlay)];

    private AppHomeFilmPart[] Overlays => [.. Current.Parts.Where(p => p is AppHomeFilmOverlay)];

    /// <summary>Long enough to read the caption and to see the scene's last event, and never shorter than 2.8 seconds.</summary>
    private static int LengthOf(AppHomeFilmScene scene)
    {
        if (scene.Milliseconds > 0)
            return scene.Milliseconds;

        var lastEvent = scene.Parts.Select(p => p.Ends).DefaultIfEmpty(0).Max();

        return Math.Max(2800, Math.Max(1600 + scene.Caption.Length * 38, (int)(lastEvent * 1000) + 1800));
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            _ = Play(CurrentCancellationToken);
        }
    }

    private async Task Play(CancellationToken cancellationToken)
    {
        try
        {
            while (cancellationToken.IsCancellationRequested is false)
            {
                await Task.Delay(TickMilliseconds, cancellationToken);

                if (isPaused || isEnded)
                    continue;

                elapsed += TickMilliseconds;

                if (elapsed < LengthOf(Current))
                    continue;

                elapsed = 0;
                if (index == Scenes.Length - 1)
                {
                    isEnded = true;
                }
                else
                {
                    index++;
                }
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (OperationCanceledException) { }
    }

    private void GoTo(int scene)
    {
        index = (scene + Scenes.Length) % Scenes.Length;
        elapsed = 0;
        isEnded = false;
    }

    private void Replay()
    {
        GoTo(0);
        isPaused = false;
    }

    private void TogglePause() => isPaused = isPaused is false;
}
