namespace Bit.Fabric.Workbench.Components.Pages;

/// <summary>
/// The Workbench's landing page: who it is for, in one big card (mostly the AI agent, then you) and small cards for the rest, the
/// agents' long tasks, the explainers they build, how to use it, the catalog of pages with their review status, and the flows.
/// </summary>
public partial class HomePage
{
    // The labels floating around the hero's devices.
    private static readonly (string IconName, string TextKey)[] chips =
    [
        (BitIconName.ClearNight, nameof(WorkbenchStrings.HomeChipDark)),
        (BitIconName.Globe, nameof(WorkbenchStrings.HomeChipRtl)),
        (BitIconName.Sync, nameof(WorkbenchStrings.HomeChipLoading)),
        (BitIconName.Camera, nameof(WorkbenchStrings.HomeChipStore)),
        (BitIconName.ChatBot, nameof(WorkbenchStrings.HomeChipLive))
    ];

    // The big card's two parts for you, after the agent's: feedback on the page, and a task's progress.
    private static readonly (string IconName, string TitleKey, string TextKey)[] yourParts =
    [
        (BitIconName.Comment, nameof(WorkbenchStrings.HomeBigFeedbackTitle), nameof(WorkbenchStrings.HomeBigFeedbackText)),
        (BitIconName.TaskList, nameof(WorkbenchStrings.HomeBigProgressTitle), nameof(WorkbenchStrings.HomeBigProgressText))
    ];

    // Everything else the Workbench does, one short line each under the big card.
    private static readonly (string IconName, string TitleKey, string TextKey)[] smallCards =
    [
        (BitIconName.Code, nameof(WorkbenchStrings.HomeCardRealTitle), nameof(WorkbenchStrings.HomeCardRealText)),
        (BitIconName.Split, nameof(WorkbenchStrings.HomeCardStatesTitle), nameof(WorkbenchStrings.HomeCardStatesText)),
        (BitIconName.Devices2, nameof(WorkbenchStrings.HomeCardDevicesTitle), nameof(WorkbenchStrings.HomeCardDevicesText)),
        (BitIconName.Camera, nameof(WorkbenchStrings.HomeCardStoresTitle), nameof(WorkbenchStrings.HomeCardStoresText)),
        (BitIconName.Lightbulb, nameof(WorkbenchStrings.HomeCardHeadStartTitle), nameof(WorkbenchStrings.HomeCardHeadStartText)),
        (BitIconName.Lock, nameof(WorkbenchStrings.HomeCardSafeTitle), nameof(WorkbenchStrings.HomeCardSafeText)),
        (BitIconName.Tiles, nameof(WorkbenchStrings.HomeCardManyAppsTitle), nameof(WorkbenchStrings.HomeCardManyAppsText)),
        (BitIconName.Robot, nameof(WorkbenchStrings.HomeCardAgentsTitle), nameof(WorkbenchStrings.HomeCardAgentsText)),
        (BitIconName.Repair, nameof(WorkbenchStrings.HomeCardCustomizeTitle), nameof(WorkbenchStrings.HomeCardCustomizeText))
    ];

    // What an agent can explain as a page here instead of in the chat.
    private static readonly (string IconName, string TextKey)[] explainerPoints =
    [
        (BitIconName.Flow, nameof(WorkbenchStrings.HomeExplainersPointDiagram)),
        (BitIconName.Split, nameof(WorkbenchStrings.HomeExplainersPointCompare)),
        (BitIconName.ReportDocument, nameof(WorkbenchStrings.HomeExplainersPointReport)),
        (BitIconName.Comment, nameof(WorkbenchStrings.HomeExplainersPointComment))
    ];

    private static readonly (string TitleKey, string TextKey)[] steps =
    [
        (nameof(WorkbenchStrings.HomeHowStep1Title), nameof(WorkbenchStrings.HomeHowStep1Text)),
        (nameof(WorkbenchStrings.HomeHowStepAnswerTitle), nameof(WorkbenchStrings.HomeHowStepAnswerText)),
        (nameof(WorkbenchStrings.HomeHowStep2Title), nameof(WorkbenchStrings.HomeHowStep2Text)),
        (nameof(WorkbenchStrings.HomeHowStep3Title), nameof(WorkbenchStrings.HomeHowStep3Text))
    ];

    private bool isPromptCopied;
    private bool isOpenFailed;
    // Hands the cards over to their reveal, which by then has played for those in sight, so they don't blink.
    private bool areRevealsLive;

    [AutoInject] private Clipboard clipboard = default!;
    [AutoInject] private WorkbenchReviewSession reviewSession = default!;
    [AutoInject] private ILogger<HomePage> logger = default!;

    /// <summary>The page the hero opens and shows on the laptop: the assistant, or the catalog's first page when a project removed it.</summary>
    private static WorkbenchPageInfo? HeroPage => WorkbenchPageCatalog.Find("assistant") ?? WorkbenchPageCatalog.Pages.FirstOrDefault();

    /// <summary>The page the hero shows on the phone: the settings, or another page of the catalog.</summary>
    private static WorkbenchPageInfo? PhonePage => WorkbenchPageCatalog.Find("settings") ?? WorkbenchPageCatalog.Pages.FirstOrDefault(p => p.Key != HeroPage?.Key) ?? HeroPage;

    /// <summary>The pages an agent built to explain something (the explainer skill), in the catalog's order.</summary>
    private static IReadOnlyList<WorkbenchPageInfo> Explainers => [.. WorkbenchPageCatalog.Pages.Where(p => p.CategoryKey == nameof(WorkbenchStrings.CategoryExplainers))];

    /// <summary>An explainer opens on a laptop, as it is read more than it is tried on a phone.</summary>
    private static string ExplainerUrl(WorkbenchPageInfo page) => $"{WorkbenchUrls.Workbench(page.Key)}?device=laptop-hidpi";

    /// <summary>The catalog's pages by category, in the catalog's order, as the Pages pane groups them.</summary>
    private static IEnumerable<IGrouping<string, WorkbenchPageInfo>> PageGroups => WorkbenchPageCatalog.Pages.GroupBy(p => p.CategoryKey);

    /// <summary>How many of the catalog's columns a group takes: one per card, up to the three a wide screen has.</summary>
    private static string SpanClass(int cardCount) => $"span-{Math.Min(cardCount, 3)}";

    /// <summary>The page's comments that are not done yet.</summary>
    private int OpenCount(string pageKey) => reviewSession.ThreadsOf(pageKey).Count(t => t.Status is not WorkbenchReviewStatus.Done);

    protected override void OnInitialized()
    {
        reviewSession.Changed += OnReviewChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("workbench.loadThumbnails");

            // The review document joins once interactive, for the statuses, comment counts and flows on the cards.
            try
            {
                await reviewSession.Open(Localizer[nameof(WorkbenchStrings.Reviewer)]);
            }
            catch (Exception exception)
            {
                isOpenFailed = true;
                logger.LogError(exception, "The review document could not be opened for the home page.");
            }

            StateHasChanged();

            await Task.Delay(5000);

            areRevealsLive = true;
            StateHasChanged();
        }
    }

    private void OnReviewChanged() => InvokeAsync(StateHasChanged);

    private void Reload() => NavigationManager.Refresh(forceReload: true);

    public override async ValueTask DisposeAsync()
    {
        reviewSession.Changed -= OnReviewChanged;

        await base.DisposeAsync();
    }

    /// <summary>The hero's entrance animation, staggered by the element's place.</summary>
    private static BmotionTransition Enter(int index) => Bm.Spring(stiffness: 120, damping: 20).WithDelay(index * 0.07);

    /// <summary>Copies the sample agent prompt and shows a check mark on the button for a moment.</summary>
    private async Task CopyPrompt()
    {
        // A browser can refuse the clipboard to a copy that arrives after a server round trip; Safari always does.
        try
        {
            await clipboard.WriteText(Localizer[nameof(WorkbenchStrings.HomeCtaPrompt)].Value);
        }
        catch (JSException)
        {
            SnackBarService.Error(Localizer[nameof(WorkbenchStrings.CopyFailed)]);
            return;
        }

        isPromptCopied = true;
        StateHasChanged();

        await Task.Delay(1500);

        isPromptCopied = false;
        StateHasChanged();
    }
}
