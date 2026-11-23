// [mirror] the home page's onboarding - keep in sync with:
// - src/Client/Bit.Fabric.Client.Core/Components/Pages/Home/HomePage.razor.Onboarding.cs
using Bit.Bonboarding;

namespace Bit.Fabric.Client.Films.Components;

/// <summary>
/// The home page's onboarding in the app, on a visitor's first visit only (bitfabric-internal/specs/home-page.md,
/// 2026-11-07): why bit Fabric is worth a look, that every item has a short film, with one of them playing, then the
/// wizard, where nothing asks them to sign in, and back to the home page. It lives in the app's layout, since the tour goes
/// from page to page, and is lazily loaded with Bit.Bonboarding, so it reaches the tour's service through
/// <see cref="BitBonboarding.GetBonboardingService"/>: nothing the app reads at startup names Bonboarding's types
/// (Bonboarding's guide, "Lazy loading on WebAssembly"). The browser keeps the tour's progress, so one that was finished,
/// skipped or closed never shows again.
/// </summary>
public partial class AppHomeOnboarding
{
    /// <summary>The tour's id, and the key of its progress in the browser's storage, after Bonboarding's prefix.</summary>
    public const string Id = "home-onboarding";

    /// <summary>Bumped whenever the steps change in a way that makes a saved place mean something else.</summary>
    public const int Version = 1;

    /// <summary>The key Bonboarding keeps the tour's progress under in the browser's local storage.</summary>
    public const string StorageKey = "bit-bonboarding:" + Id;

    private const string FilmStep = "films";
    private const string WhyAnchor = "home-why";
    private const string FilmDialog = ".ah-film";
    private const string HeroActions = ".ah-actions";

    [AutoInject] private IServiceProvider services = default!;
    [AutoInject] private NavigationManager navigationManager = default!;

    private IBonboardingTourSession? session;
    private bool isOnFilmStep;

    /// <summary>The home page's address, with the page's culture when it has one.</summary>
    [Parameter, EditorRequired] public string HomeUrl { get; set; } = default!;

    /// <summary>The wizard's address, where the third step points.</summary>
    [Parameter, EditorRequired] public string GetStartedUrl { get; set; } = default!;

    /// <summary>Whether the tour is on its films step, where the home page plays the website's film (See HomePage.ShowOnboardingFilm).</summary>
    [Parameter] public EventCallback<bool> OnFilmStepChanged { get; set; }

    /// <summary>
    /// Brings the home page's hero into view, for a step about it, once the step shows: the app's shell puts a page back
    /// where it was left, which can come after the tour has measured it, so back from the wizard the hero can be above the
    /// screen. The page does it, as only it holds its hero (See HomePage.BringHeroIntoView), and the spotlight follows.
    /// </summary>
    [Parameter] public EventCallback BringHeroIntoView { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender is false)
            return;

        var bonboarding = services.GetBonboardingService();

        session = bonboarding.FindSession(Id) ?? await bonboarding.StartOnceAsync(CreateOnboarding(), cancellationToken: CurrentCancellationToken);

        // Gone while the tour was starting (the page was left, or the app soft restarted): Dispose has run already, so a
        // handler added now would keep this instance on the session for as long as the tour lives
        if (session is null || CurrentCancellationToken.IsCancellationRequested)
            return;

        session.Changed += OnSessionChanged;
        await ReportFilmStep();
    }

    private void OnSessionChanged(object? sender, BonboardingTourSessionChangedEventArgs args) => _ = InvokeAsync(ReportFilmStep);

    private async Task ReportFilmStep()
    {
        var onFilmStep = session is { EndReason: null, CurrentStep.Id: FilmStep };

        if (onFilmStep == isOnFilmStep)
            return;

        isOnFilmStep = onFilmStep;
        await OnFilmStepChanged.InvokeAsync(onFilmStep);
    }

    /// <summary>Whether the page opened on one of its bands, by a link to it (bitfabric-internal/specs/home-page.md, 2026-11-09).</summary>
    private bool OpenedOnABand => new Uri(navigationManager.Uri).Fragment.Length > 1;

    private BonboardingTourDefinition CreateOnboarding() => new()
    {
        Id = Id,
        Version = Version,
        Persist = true,
        MissingTarget = BonboardingMissingTargetBehavior.Center,
        Labels = new()
        {
            Next = Localizer[nameof(AppStrings.TourNext)],
            Previous = Localizer[nameof(AppStrings.TourBack)],
            Skip = Localizer[nameof(AppStrings.TourSkip)],
            Done = Localizer[nameof(AppStrings.TourDone)],
            Close = Localizer[nameof(AppStrings.Close)],
            DialogLabel = Localizer[nameof(AppStrings.TourDialogLabel)],
            ProgressFormat = Localizer[nameof(AppStrings.TourProgress)],
            StepAnnouncementFormat = Localizer[nameof(AppStrings.TourStepAnnouncement)],
            WaitingAnnouncement = Localizer[nameof(AppStrings.TourWaiting)],
            ProgressLabel = Localizer[nameof(AppStrings.TourProgressLabel)]
        },
        Steps =
        {
            new BonboardingStepDefinition
            {
                Id = "why",
                Route = HomeUrl,
                // What bit Fabric adds, in the hero, so the tour starts where the page opens, on any screen. A link to a
                // band opens further down, so there the card shows in the middle of the screen and the page stays where it is
                Target = OpenedOnABand ? BonboardingStepTarget.None : BonboardingStepTarget.Anchor(WhyAnchor),
                Placement = BonboardingPlacement.Bottom,
                OnShown = OpenedOnABand ? null : ShowHero(BringHeroIntoView),
                Title = Localizer[nameof(AppStrings.AppHomeSeeWhatSInItForYou)],
                Description = Localizer[nameof(AppStrings.AppHomeThisPageShowsWhyBitFabricCanBeUseful)]
            },
            new BonboardingStepDefinition
            {
                Id = FilmStep,
                Route = HomeUrl,
                // The film the page opens for this step, which shows a moment after the step begins
                Target = BonboardingStepTarget.Css(FilmDialog),
                MissingTarget = BonboardingMissingTargetBehavior.Wait,
                Scroll = BonboardingScrollMode.None,
                // The dialog's own edges frame the film, so the card can come closer: on a phone the two share the screen
                SpotlightPadding = 0,
                Offset = 8,
                Title = Localizer[nameof(AppStrings.AppHomeEveryItemHasAShortVideoLikeThisOne)],
                Description = Localizer[nameof(AppStrings.AppHomeAFewScenesOfYourAppDoingItPressOnAny)]
            },
            new BonboardingStepDefinition
            {
                Id = "create",
                Route = GetStartedUrl,
                Target = BonboardingStepTarget.Css(".np-intro"),
                Title = Localizer[nameof(AppStrings.AppHomeThenBuildYourProjectEasily)],
                Description = Localizer[nameof(AppStrings.AppHomeAnswerAFewQuestionsAndPasteOneLine)]
            },
            new BonboardingStepDefinition
            {
                Id = "start",
                Route = HomeUrl,
                Target = BonboardingStepTarget.Css(HeroActions),
                // Under the buttons, clear of the hero's title, and with the whole hero in view, so the page is back at its top
                Placement = BonboardingPlacement.Bottom,
                OnShown = ShowHero(BringHeroIntoView),
                Title = Localizer[nameof(AppStrings.AppHomeReadyWhenYouAre)],
                Description = Localizer[nameof(AppStrings.AppHomeGetStartedIsAlwaysAtHandAllTheWay)]
            }
        }
    };

    /// <summary>
    /// A step's <c>OnShown</c> that brings the hero into view. Static, so it holds the home page's callback and not this
    /// component: the tour's session outlives the component, as a soft restart (a language, time zone or account change)
    /// builds the layout and this host again and the new host carries on with the same session (See FindSession above),
    /// and a step holding this instance would keep it in memory, disposed, for as long as the tour lives.
    /// </summary>
    private static Func<BonboardingStepContext, ValueTask> ShowHero(EventCallback bringHeroIntoView)
        => async _ => await bringHeroIntoView.InvokeAsync();

    public override void Dispose()
    {
        if (session is not null)
        {
            session.Changed -= OnSessionChanged;
        }

        base.Dispose();
    }
}
