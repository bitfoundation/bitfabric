namespace Bit.Fabric.Client.Core.Infrastructure.Services;

/// <summary>
/// Between the home page and its onboarding, which goes from page to page and so lives in the layout
/// (<c>AppOnboardingHost</c>): the home page hands it the tour's component once it has loaded it, and hears from it when the
/// tour is on the step that plays a film. The tour's component is lazily loaded with Bit.Bonboarding, so it is a
/// <see cref="Type"/> here, and nothing in Client.Core names Bonboarding's types.
/// </summary>
public partial class HomeOnboardingService
{
    /// <summary>The tour's component, once the home page has loaded it.</summary>
    public Type? HostType { get; private set; }

    public IDictionary<string, object?>? HostParameters { get; private set; }

    /// <summary>Whether the tour is on its films step, where the home page plays a film.</summary>
    public bool IsOnFilmStep { get; private set; }

    public event Action? HostChanged;

    public event Action? FilmStepChanged;

    /// <summary>Puts the tour's component in the layout. Only the first call does anything: the tour then carries on there.</summary>
    public void Show(Type hostType, IDictionary<string, object?> parameters)
    {
        if (HostType is not null)
            return;

        HostType = hostType;
        HostParameters = parameters;
        HostChanged?.Invoke();
    }

    public void SetFilmStep(bool isOnFilmStep)
    {
        IsOnFilmStep = isOnFilmStep;
        FilmStepChanged?.Invoke();
    }

    /// <summary>The home page showing its hero, set by the page that's open: only it holds the hero's element.</summary>
    public Func<Task>? ShowHero { get; set; }

    /// <summary>Asks the open home page to bring its hero into view, for a step of the tour about it.</summary>
    public Task BringHeroIntoView() => ShowHero?.Invoke() ?? Task.CompletedTask;
}
