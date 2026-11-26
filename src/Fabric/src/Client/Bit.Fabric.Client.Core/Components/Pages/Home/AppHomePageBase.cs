using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// What bitfabric.co's public pages share: the home page, <c>/features</c> and <c>/contents</c> open on the
/// same title, play a feature's film in a dialog, carry the films' text for search engines while prerendering, and keep
/// "Get started" at hand on the screen's edge once the page's own button has scrolled away. The films are a library of their
/// own, Bit.Fabric.Client.Films, which the browser downloads the first time one opens: nothing here names one of its types
/// before then.
/// </summary>
public abstract partial class AppHomePageBase : AppPageBase
{
    /// <summary>
    /// The hero's title, a sentence a line: it is two short sentences in every language, and a width that puts one
    /// language's on two lines breaks another's in the middle of one, as 18ch left the Persian «صاحب» on a line alone.
    /// </summary>
    protected string[] HeroTitleSentences => [.. SentenceEnd().Split(Localizer[nameof(AppStrings.AppHomeBuildWithAIOwnEverything)]).Where(sentence => sentence.Length > 0)];

    /// <summary>The space after a sentence's full stop, in the scripts the app's languages use.</summary>
    [GeneratedRegex(@"(?<=[.!?。！？।])\s*")]
    private static partial Regex SentenceEnd();

    protected const string FilmsAssembly = "Bit.Fabric.Client.Films.wasm";
    protected const string FilmTypeName = "Bit.Fabric.Client.Films.Components.AppHomeFilm, Bit.Fabric.Client.Films";

    [AutoInject] protected LazyAssemblyLoader lazyAssemblyLoader = default!;

    /// <summary>Where "Get started" leads: the wizard, which needs no account, in the page's language.</summary>
    protected static string GetStartedUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject);

    /// <summary>The page every project's features are on, in the page's language.</summary>
    protected static string FeaturesUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Features);

    /// <summary>What bit Fabric costs and what the visitor pays others, in the page's language.</summary>
    protected static string PricingUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing);

    /// <summary>The AI agents a project is ready for, in the page's language.</summary>
    protected static string ForUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.For);

    /// <summary>Who makes bit Fabric and what it can see, in the page's language.</summary>
    protected static string TrustUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Trust);

    /// <summary>bit Fabric next to the other ways, one by one, in the page's language.</summary>
    protected static string CompareUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Compare);

    /// <summary>Why a real app costs less, in the page's language.</summary>
    protected static string CostUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cost);

    /// <summary>bit CLI, the tool the one line installs, in the page's language.</summary>
    protected static string CliUrl => CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cli);

    /// <summary>The feature whose film is playing in the dialog, if any.</summary>
    protected AppHomeFeature? playing;

    /// <summary>The film player's component, once the films' library is loaded.</summary>
    protected Type? filmType;

    /// <summary>Whether the film in the dialog is the one the home page's onboarding opened, rather than one the visitor did.</summary>
    protected bool isOnboardingFilm;

    /// <summary>
    /// The status notice's card on the home page and /features: a few lines rather than a film, since the notice shows only
    /// while something is wrong (bitfabric-internal/specs/monitoring.md, "The app shows it while something is wrong").
    /// </summary>
    protected AppHomeFeature StatusNoticeFeature => new("status-notice", BitIconName.Health,
        Localizer[nameof(AppStrings.AppHomeDayOneStatusNotice)], Localizer[nameof(AppStrings.AppHomeDayOneStatusNoticeText)],
        Note: new(Localizer[nameof(AppStrings.AppHomeStatusNoticeLead)],
        [
            (Localizer[nameof(AppStrings.AppHomeStatusNoticeWhenTerm)], Localizer[nameof(AppStrings.AppHomeStatusNoticeWhen)]),
            (Localizer[nameof(AppStrings.AppHomeStatusNoticeShowsTerm)], Localizer[nameof(AppStrings.AppHomeStatusNoticeShows)]),
            (Localizer[nameof(AppStrings.AppHomeStatusNoticeWhereTerm)], Localizer[nameof(AppStrings.AppHomeStatusNoticeWhere)])
        ],
        Localizer[nameof(AppStrings.AppHomeStatusNoticeAfter)]));

    protected async Task Explain(AppHomeFeature feature)
    {
        if (feature.Note is null && filmType is null)
        {
            // An AOT build has every assembly in it already
            if (AppPlatform.IsAot is false)
            {
                await lazyAssemblyLoader.LoadAssembliesAsync([FilmsAssembly]);
            }

            filmType = Type.GetType(FilmTypeName, throwOnError: true);
        }

        playing = feature;
    }

    protected void CloseFilm()
    {
        playing = null;
        isOnboardingFilm = false;
    }

    /// <summary>
    /// What a feature's info button would show, as text for the prerendered page, so search engines read every film:
    /// a note's lines, or the film's captions (See AppHomeFilms.CaptionsOf). The interactive page
    /// renders none of it: a film is built only once it's opened.
    /// </summary>
    protected string TextOf(AppHomeFeature feature)
    {
        if (feature.Note is { } note)
            return string.Join(" ", note.Facts.Select(f => $"{f.Term}: {f.Detail}.").Prepend(note.Lead).Append(note.After));

        return new Bit.Fabric.Client.Films.Components.AppHomeFilms(Localizer).CaptionsOf(feature.Id);
    }

    // "Get started" stays at hand down the page as a tab on the screen's edge: it shows once the
    // page's own button has scrolled away, and hides again at the closing call to action, which has its own.
    protected ElementReference heroActions;
    protected ElementReference finalBand;
    private ButilSubscription? heroWatch;
    private ButilSubscription? finalWatch;
    private bool isHeroInView = true;
    private bool isFinalInView;

    protected bool IsCtaShown => isHeroInView is false && isFinalInView is false;

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        heroWatch = await heroActions.ObserveIntersection(JSRuntime, entries => Track(entries, inView => isHeroInView = inView));
        finalWatch = await finalBand.ObserveIntersection(JSRuntime, entries => Track(entries, inView => isFinalInView = inView));
    }

    private void Track(IntersectionObserverEntry[] entries, Action<bool> setInView)
    {
        if (entries.Length is 0)
            return;

        setInView(entries[^1].IsIntersecting);
        _ = InvokeAsync(StateHasChanged);
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        if (heroWatch is not null)
            await heroWatch.DisposeAsync();

        if (finalWatch is not null)
            await finalWatch.DisposeAsync();

        await base.DisposeAsync(disposing);
    }
}
