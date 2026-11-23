// [mirror] the home page's onboarding - keep in sync with:
// - src/Client/Bit.Fabric.Client.Films/Components/AppHomeOnboarding.razor.cs
using System.Text.Json;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// The home page's onboarding, on a visitor's first visit only. The tour itself is the films'
/// library's <c>AppHomeOnboarding</c>, lazily loaded with Bit.Bonboarding, and the layout hosts it, since it goes to the wizard
/// and back. The page loads the two only when the browser has no record of the tour being finished, skipped or closed: a
/// returning visitor downloads neither.
/// </summary>
public partial class HomePage
{
    private const string BonboardingAssembly = "Bit.Bonboarding.wasm";
    private const string OnboardingTypeName = "Bit.Fabric.Client.Films.Components.AppHomeOnboarding, Bit.Fabric.Client.Films";

    [AutoInject] private HomeOnboardingService homeOnboarding = default!;
    [AutoInject] private LocalStorage localStorage = default!;

    /// <summary>The film the onboarding plays: the fast website search engines love.</summary>
    private AppHomeFeature OnboardingFilm => DayOne.Single(f => f.Id is "website");

    /// <summary>The hero, which the tour's first and last steps are about.</summary>
    private ElementReference hero;

    private Task BringHeroIntoView() => hero.ScrollIntoView(new ScrollIntoViewOptions { Block = ScrollLogicalPosition.Nearest }).AsTask();

    private async Task StartOnboarding()
    {
        homeOnboarding.FilmStepChanged += OnFilmStepChanged;
        homeOnboarding.ShowHero = BringHeroIntoView;

        // Coming back from the wizard, the tour going on is the same one, already loaded
        if (homeOnboarding.HostType is null)
        {
            if (IsOnboardingClosed(await localStorage.GetItem(Bit.Fabric.Client.Films.Components.AppHomeOnboarding.StorageKey)))
                return;

            // An AOT build has every assembly in it already
            if (AppPlatform.IsAot is false)
            {
                await lazyAssemblyLoader.LoadAssembliesAsync([FilmsAssembly, BonboardingAssembly]);
            }

            var prefix = string.IsNullOrWhiteSpace(culture) ? null : $"/{culture}";

            homeOnboarding.Show(Type.GetType(OnboardingTypeName, throwOnError: true)!, new Dictionary<string, object?>
            {
                ["HomeUrl"] = $"{prefix}{PageUrls.Home}",
                ["GetStartedUrl"] = $"{prefix}{PageUrls.NewProject}",
                ["OnFilmStepChanged"] = EventCallback.Factory.Create<bool>(homeOnboarding, homeOnboarding.SetFilmStep),
                ["BringHeroIntoView"] = EventCallback.Factory.Create(homeOnboarding, homeOnboarding.BringHeroIntoView)
            });
        }

        // Loaded by now, for this page too when it's back from the wizard: Back there returns to the films step
        filmType ??= Type.GetType(FilmTypeName, throwOnError: true);

        ShowOnboardingFilm();
    }

    /// <summary>
    /// Whether the browser's record of the tour, the one Bonboarding keeps in its local storage, says it was finished, skipped
    /// or closed. Read without Bonboarding, which isn't loaded yet; anything it can't read counts as not closed, and
    /// Bonboarding's own StartOnceAsync decides then.
    /// </summary>
    private static bool IsOnboardingClosed(string? record)
    {
        if (string.IsNullOrWhiteSpace(record))
            return false;

        try
        {
            using var json = JsonDocument.Parse(record);

            if (json.RootElement.ValueKind is not JsonValueKind.Object)
                return false;

            var properties = json.RootElement.EnumerateObject().ToDictionary(p => p.Name, p => p.Value, StringComparer.OrdinalIgnoreCase);

            bool IsTrue(string name) => properties.TryGetValue(name, out var value) && value.ValueKind is JsonValueKind.True;

            var isThisVersion = properties.TryGetValue("version", out var version) && version.ValueKind is JsonValueKind.Number
                && version.GetInt32() == Bit.Fabric.Client.Films.Components.AppHomeOnboarding.Version;

            return isThisVersion && (IsTrue("completed") || IsTrue("dismissed"));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private void OnFilmStepChanged() => _ = InvokeAsync(ShowOnboardingFilm);

    /// <summary>
    /// Told that every item has a video, a visitor pictures a long one and never presses ⓘ, so the
    /// films step plays one: a few scenes of their app, with a line under each. The film is open while the tour is on that
    /// step, however it got there, Back from the wizard included, and closes once the tour moves on or ends.
    /// </summary>
    private void ShowOnboardingFilm()
    {
        var isOnFilmStep = homeOnboarding.IsOnFilmStep && filmType is not null;

        if (isOnFilmStep && playing is null)
        {
            playing = OnboardingFilm;
            isOnboardingFilm = true;
        }
        else if (isOnFilmStep is false && isOnboardingFilm)
        {
            playing = null;
            isOnboardingFilm = false;
        }
        else
        {
            return;
        }

        StateHasChanged();
    }

    private void StopWatchingOnboarding()
    {
        homeOnboarding.FilmStepChanged -= OnFilmStepChanged;

        // Unless the next home page has already taken it over
        if (homeOnboarding.ShowHero == (Func<Task>)BringHeroIntoView)
        {
            homeOnboarding.ShowHero = null;
        }
    }
}
