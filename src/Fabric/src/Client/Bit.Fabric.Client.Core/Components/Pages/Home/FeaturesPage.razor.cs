using System.Text.Json.Nodes;

namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/features: everything every project comes with, band by band, each feature with its
/// film, then the numbers that earn trust, the projects built on bit platform and the way to start. It was the home page's
/// until the home page became seven bands that lead a visitor to start; it keeps every band's address and every film's text
/// for search engines. <see cref="ContentsPage"/> is the same page with what else the old home page had.
/// </summary>
public partial class FeaturesPage
{
    /// <summary>
    /// Whether this is <c>/contents</c>, the home page as it was until 2026-11-18: the old hero, What we add, the comparison
    /// and the calculator join the page, in their old places.
    /// </summary>
    protected virtual bool IsOldHomePage => false;

    /// <summary>
    /// The page as JSON-LD: a WebPage about bit Fabric (the home page's SoftwareApplication, by its
    /// @id), the way back to the home page, and its bands in their order, each by its address on the page.
    /// </summary>
    private string BuildFeaturesJsonLd()
    {
        var siteUrl = new Uri(NavigationManager.BaseUri).GetLeftPart(UriPartial.Authority);
        var homeUrl = new Uri(new Uri(siteUrl), CultureInfoManager.GetCurrentCultureUrl(PageUrls.Home)).ToString();
        var title = Localizer[nameof(AppStrings.FeaturesTitle)].Value;

        var webPage = new JsonObject
        {
            ["@type"] = "WebPage",
            ["@id"] = $"{CanonicalUrl}#webpage",
            ["url"] = CanonicalUrl,
            ["name"] = Localizer[nameof(AppStrings.FeaturesPageTitle)].Value,
            ["description"] = Localizer[nameof(AppStrings.FeaturesPageDescription)].Value,
            ["isPartOf"] = new JsonObject { ["@id"] = $"{siteUrl}/#website" },
            ["about"] = new JsonObject { ["@id"] = $"{siteUrl}/#software" },
            ["mainEntity"] = new JsonObject { ["@id"] = $"{CanonicalUrl}#list" },
            ["breadcrumb"] = new JsonObject { ["@id"] = $"{CanonicalUrl}#breadcrumb" }
        };

        if (string.IsNullOrWhiteSpace(CultureInfo.CurrentUICulture.Name) is false)
        {
            webPage["inLanguage"] = CultureInfo.CurrentUICulture.Name;
        }

        var bands = new JsonObject
        {
            ["@type"] = "ItemList",
            ["@id"] = $"{CanonicalUrl}#list",
            ["name"] = title,
            ["itemListElement"] = new JsonArray([.. Sections.Select((section, index) => (JsonNode?)new JsonObject
            {
                ["@type"] = "ListItem",
                ["position"] = index + 1,
                ["name"] = section.Title,
                ["url"] = $"{CanonicalUrl}#{section.Id}"
            })])
        };

        var breadcrumb = new JsonObject
        {
            ["@type"] = "BreadcrumbList",
            ["@id"] = $"{CanonicalUrl}#breadcrumb",
            ["itemListElement"] = new JsonArray(
                new JsonObject { ["@type"] = "ListItem", ["position"] = 1, ["name"] = Localizer[nameof(AppStrings.Home)].Value, ["item"] = homeUrl },
                new JsonObject { ["@type"] = "ListItem", ["position"] = 2, ["name"] = title, ["item"] = CanonicalUrl })
        };

        return new JsonObject
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = new JsonArray(webPage, bands, breadcrumb)
        }.ToJsonString();
    }

    /// <summary>The old home page's six values, the first three of them in its hero.</summary>
    private (string Icon, string Title, string Text)[] Values =>
    [
        (BitIconName.Lock, Localizer[nameof(AppStrings.AppHomePrivacyFirst)], Localizer[nameof(AppStrings.AppHomeBitFabricNeverSeesYourCodeDataOr)]),
        (BitIconName.Savings, Localizer[nameof(AppStrings.AppHomeCostOptimized)], Localizer[nameof(AppStrings.AppHomeCostOptimizedText)]),
        (BitIconName.ThisPC, Localizer[nameof(AppStrings.AppHomeYouOwnItAll)], Localizer[nameof(AppStrings.AppHomeTheCodeTheServerAndYourUsersDataAre)]),
        (BitIconName.Permissions, Localizer[nameof(AppStrings.AppHomeZeroLockIn)], Localizer[nameof(AppStrings.AppHomeStandardOpenCodeThatRunsAnywhereKeep)]),
        (BitIconName.Rocket, Localizer[nameof(AppStrings.AppHomeFullFeaturedFromDayOne)], Localizer[nameof(AppStrings.AppHomeYouNeverStartFromAnEmptyProjectWhat)]),
        (BitIconName.People, Localizer[nameof(AppStrings.AppHomeAProTeamOfAIAgents)], Localizer[nameof(AppStrings.AppHomeAgentsThatWorkLikeASeasonedProduct)])
    ];

    private const string PictureTypeName = "Bit.Fabric.Client.Diagrams.Components.AppHomePictureView, Bit.Fabric.Client.Diagrams";

    /// <summary>What the three pictures on the page need: every visit downloads these, after the page is interactive.</summary>
    private static readonly string[] PictureAssemblies = ["Bit.Fabric.Client.Diagrams.wasm", "Bit.Blow.wasm", "Bit.Bmotion.wasm"];

    [AutoInject] private ClientCoreSettings clientCoreSettings = default!;

    [CascadingParameter] public BitDir? CurrentDir { get; set; }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        await LoadPictures();
    }

    private bool arePicturesWanted;
    private Type? pictureType;
    private Dictionary<string, object?>? goingLiveParameters;
    private Dictionary<string, object?>? protectionParameters;
    private Dictionary<string, object?>? aiDlcParameters;

    private const string GoingLivePicture = "going-live";
    private const string ProtectionPicture = "protection";
    private const string AiDlcPicture = "ai-dlc";

    /// <summary>
    /// Brings in the pictures. A download that fails leaves the plain list in their place and says nothing: the visitor
    /// has lost only an animation. It names none of their types, which the browser would resolve before they are loaded.
    /// </summary>
    private async Task LoadPictures()
    {
        arePicturesWanted = true;
        StateHasChanged();

        try
        {
            // An AOT build has every assembly in it already
            if (AppPlatform.IsAot is false)
            {
                await lazyAssemblyLoader.LoadAssembliesAsync(PictureAssemblies);
            }

            pictureType = Type.GetType(PictureTypeName, throwOnError: true);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            ExceptionHandler.Handle(exp, ExceptionDisplayKind.None);
        }
    }

    /// <summary>A picture's parameters. Called only once its library is loaded, as it names its types.</summary>
    private Dictionary<string, object?> PictureParameters(string id)
    {
        var pictures = new Bit.Fabric.Client.Diagrams.Components.AppHomePictures(Localizer);

        return new()
        {
            ["Picture"] = id switch
            {
                GoingLivePicture => pictures.GoingLive(),
                ProtectionPicture => pictures.Protection(),
                _ => pictures.AiDlc()
            },
            ["Dir"] = CurrentDir
        };
    }

    /// <summary>What the AI-DLC picture says, as a list.</summary>
    private (string Title, string? Text)[] AiDlcSteps =>
    [
        (Localizer[nameof(AppStrings.AppHomeAiDlcAsk)], Localizer[nameof(AppStrings.AppHomeAiDlcAskText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcQuestions)], Localizer[nameof(AppStrings.AppHomeAiDlcQuestionsText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcPlan)], Localizer[nameof(AppStrings.AppHomeAiDlcPlanText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcDesign)], Localizer[nameof(AppStrings.AppHomeAiDlcDesignText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcBuild)], Localizer[nameof(AppStrings.AppHomeAiDlcBuildText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcTests)], Localizer[nameof(AppStrings.AppHomeAiDlcTestsText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcReview)], Localizer[nameof(AppStrings.AppHomeAiDlcReviewText)]),
        (Localizer[nameof(AppStrings.AppHomeAiDlcReady)], Localizer[nameof(AppStrings.AppHomeAiDlcReadyText)])
    ];

    /// <summary>What the going live picture says, as a list. It names no type of the pictures' library, so it's here before them.</summary>
    private (string Title, string? Text)[] GoingLiveSteps =>
    [
        (Localizer[nameof(AppStrings.AppHomeGoingLiveServed)], Localizer[nameof(AppStrings.AppHomeGoingLiveServedText)]),
        (Localizer[nameof(AppStrings.AppHomeGoingLiveOld)], null),
        (Localizer[nameof(AppStrings.AppHomeGoingLiveData)], null),
        (Localizer[nameof(AppStrings.AppHomeGoingLiveNew)], null),
        (Localizer[nameof(AppStrings.AppHomeGoingLiveBoth)], null),
        (Localizer[nameof(AppStrings.AppHomeGoingLiveGone)], null)
    ];

    /// <summary>What the protection picture says, as a list.</summary>
    private (string Title, string? Text)[] ProtectionSteps =>
    [
        (Localizer[nameof(AppStrings.AppHomeProtectionVisitors)], null),
        (Localizer[nameof(AppStrings.AppHomeProtectionFront)], Localizer[nameof(AppStrings.AppHomeProtectionFrontText)]),
        (Localizer[nameof(AppStrings.AppHomeProtectionServer)], $"{Localizer[nameof(AppStrings.AppHomeProtectionWebsite)]}, {Localizer[nameof(AppStrings.AppHomeProtectionData)]}, {Localizer[nameof(AppStrings.AppHomeProtectionDashboards)]}"),
        (Localizer[nameof(AppStrings.AppHomeProtectionTeam)], null),
        (Localizer[nameof(AppStrings.AppHomeProtectionStaff)], Localizer[nameof(AppStrings.AppHomeProtectionStaffText)]),
        (Localizer[nameof(AppStrings.AppHomeProtectionAnyone)], Localizer[nameof(AppStrings.AppHomeProtectionAnyoneText)])
    ];

    /// <summary>A band's cards as one grid, or as two with its showcase between them, right after the card it names.</summary>
    private static IEnumerable<(AppHomeFeature[] Features, bool ShowcaseAfter)> GridsOf(AppHomeSection section)
    {
        var split = section.Showcase is { } showcase ? Array.FindIndex(section.Features, f => f.Id == showcase.After) + 1 : 0;

        if (split is 0)
        {
            yield return (section.Features, false);
            yield break;
        }

        yield return (section.Features[..split], true);

        if (split < section.Features.Length)
            yield return (section.Features[split..], false);
    }

    private AppHomeSection[] Sections =>
    [
        new("platforms", Localizer[nameof(AppStrings.AppHomeEveryPlatformFromOneProject)], Localizer[nameof(AppStrings.AppHomeOneCodebaseBecomesYourAppsYour)],
        [
            new("apps", BitIconName.CellPhone, Localizer[nameof(AppStrings.AppHomeAndroidIOSWindowsAndMacOS)], Localizer[nameof(AppStrings.AppHomeRealAppsWithUnlimitedAccessToNative)], Explained: false),
            new("website", BitIconName.Globe, Localizer[nameof(AppStrings.AppHomeAFastWebsiteSearchEnginesLove)], Localizer[nameof(AppStrings.AppHomePagesThatOpenFastAndAreReadyFor)]),
            new("admin-panel", BitIconName.BarChartVerticalFill, Localizer[nameof(AppStrings.AppHomeAnAdminPanel)], Localizer[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamInThe)]),
            new("tenants", BitIconName.Org, Localizer[nameof(AppStrings.AppHomeManyCompaniesOneApp)], Localizer[nameof(AppStrings.AppHomeMultiTenantAndSaaSReady)]),
            new("gdpr", BitIconName.ShieldSolid, Localizer[nameof(AppStrings.AppHomeGDPRFriendly)], Localizer[nameof(AppStrings.AppHomeUsersChooseWhatTheyAllowDownload)]),
            new("offline", BitIconName.CloudDownload, Localizer[nameof(AppStrings.AppHomeWorksOffline)], Localizer[nameof(AppStrings.AppHomeItKeepsWorkingWithoutAConnectionAnd)]),
            new("force-update", BitIconName.Refresh, Localizer[nameof(AppStrings.AppHomeForceUpdate)], Localizer[nameof(AppStrings.AppHomeWhenAVersionHasToGoItsUsersUpdate)])
        ]),
        new("identity", Localizer[nameof(AppStrings.AppHomeACompleteIdentitySolutionInsideYourProject)], Localizer[nameof(AppStrings.AppHomeNoIdentityProviderToPayAndYourUsers)],
        [
            new("social", BitIconName.People, Localizer[nameof(AppStrings.AppHomeSocialSignIn)], Localizer[nameof(AppStrings.AppHomeOneTapWithGoogleAppleMicrosoftAndThe)]),
            new("two-step", BitIconName.CellPhone, Localizer[nameof(AppStrings.AppHomeTwoStepSignIn)], Localizer[nameof(AppStrings.AppHomeACodeBySMSEmailOrAnAuthenticatorApp)]),
            // Magic links and quick sign-in are cards of their own, each with its film
            new("magic", BitIconName.Mail, Localizer[nameof(AppStrings.AppHomeMagicLinks)], Localizer[nameof(AppStrings.AppHomeMagicLinksText)]),
            new("quick-sign-in", BitIconName.Signin, Localizer[nameof(AppStrings.AppHomeQuickSignIn)], Localizer[nameof(AppStrings.AppHomeQuickSignInText)]),
            new("return-url", BitIconName.ReturnToSession, Localizer[nameof(AppStrings.AppHomeBackOnTheSamePage)], Localizer[nameof(AppStrings.AppHomeStartSigningInOnYourPhoneOpenThe)]),
            // Face ID and fingerprint are cards of their own, each with its film
            new("face-id", BitIconName.NUIFace, Localizer[nameof(AppStrings.AppHomeFaceID)], Localizer[nameof(AppStrings.AppHomeFaceIDText)]),
            new("fingerprint", BitIconName.Fingerprint, Localizer[nameof(AppStrings.AppHomeFingerprint)], Localizer[nameof(AppStrings.AppHomeFingerprintText)]),
            new("elevated", BitIconName.Lock, Localizer[nameof(AppStrings.AppHomeElevatedSessions)], Localizer[nameof(AppStrings.AppHomeBeforeSomethingSensitiveProveItSYou)]),
            new("privileged", BitIconName.Devices3, Localizer[nameof(AppStrings.AppHomePrivilegedSessions)], Localizer[nameof(AppStrings.AppHomeLimitHowManyDevicesAnAccountCanUseAt)]),
            new("accounts", BitIconName.Contact, Localizer[nameof(AppStrings.AppHomeSeveralAccounts)], Localizer[nameof(AppStrings.AppHomeSwitchBetweenAccountsWithoutSigning)]),
            new("groups", BitIconName.Permissions, Localizer[nameof(AppStrings.AppHomeFeaturesByUserGroup)], Localizer[nameof(AppStrings.AppHomeSensitiveFeaturesForAdminsNewOnesFor)]),
            new("ai-sign-in", BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeSignInFromYourAI)], Localizer[nameof(AppStrings.AppHomeWithOAuth21TheAIYouUseEveryDayCan)]),
            // Single sign-on, reCAPTCHA, and the firewall with DDoS protection are three cards
            new("sso", BitIconName.ContactCard, Localizer[nameof(AppStrings.AppHomeEnterpriseSingleSignOn)], Localizer[nameof(AppStrings.AppHomeEnterpriseSingleSignOnText)]),
            new("recaptcha", BitIconName.Blocked2, "reCAPTCHA", Localizer[nameof(AppStrings.AppHomeReCAPTCHAText)]),
            new("firewall", BitIconName.Shield, Localizer[nameof(AppStrings.AppHomeFirewallAndDDoSProtection)], Localizer[nameof(AppStrings.AppHomeFirewallAndDDoSProtectionText)])
        ]),
        new("assistant", Localizer[nameof(AppStrings.AppHomeAnAIAssistantThatStaysOnTopic)], Localizer[nameof(AppStrings.AppHomeItAnswersAboutYourAppAndNothingElse)],
        [
            new("focused", BitIconName.Chat, Localizer[nameof(AppStrings.AppHomeFocused)], Localizer[nameof(AppStrings.AppHomeOffTopicQuestionsArePolitelyTurned)]),
            new("device-support", BitIconName.CellPhone, Localizer[nameof(AppStrings.AppHomeSupportThatKnowsTheDevice)], Localizer[nameof(AppStrings.AppHomeHowToInstallTheAppOnEdgeOnWindowsIs)]),
            new("search-data", BitIconName.Search, Localizer[nameof(AppStrings.AppHomeSearchesYourData)], Localizer[nameof(AppStrings.AppHomeProductsOrdersAnythingInYourDatabase)]),
            new("images", BitIconName.Photo2, Localizer[nameof(AppStrings.AppHomeSafeWithImages)], Localizer[nameof(AppStrings.AppHomeItWritesTheirAltTextsAndTurns)]),
            new("handover", BitIconName.Contact, Localizer[nameof(AppStrings.AppHomeHandsOverToAPerson)], Localizer[nameof(AppStrings.AppHomeWhenItCanTHelpAHumanTakesOver)]),
            new("human-in-the-loop", BitIconName.Touch, Localizer[nameof(AppStrings.AppHomeHumanInTheLoop)], Localizer[nameof(AppStrings.AppHomeHumanInTheLoopText)]),
            new("voice", BitIconName.Microphone, Localizer[nameof(AppStrings.AppHomeVoiceAndFiles)], Localizer[nameof(AppStrings.AppHomeTextToSpeechSpeechToTextLiveVoiceAnd)]),
            new("brain", BitIconName.Settings, Localizer[nameof(AppStrings.AppHomeABrainYouShape)], Localizer[nameof(AppStrings.AppHomeChangeHowItBehavesFromInsideYourApp)]),
            new("agentic", BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeReadyForAIAgents)], Localizer[nameof(AppStrings.AppHomeBuiltForAgenticAIWithMicrosoftAgent)]),
            new("mcp", BitIconName.PlugConnected, Localizer[nameof(AppStrings.AppHomeMcpYourAppInsideTheirAI)], Localizer[nameof(AppStrings.AppHomeMcpYourAppInsideTheirAIText)])
        ]),
        new("languages", Localizer[nameof(AppStrings.AppHomeEveryLanguageBothDirectionsAnyTime)], Localizer[nameof(AppStrings.AppHomeYourAppSpeaksToEachUserInTheirOwn)],
        [
            new("rtl", BitIconName.LocaleLanguage, Localizer[nameof(AppStrings.AppHomeRightToLeftAndLeftToRight)], Localizer[nameof(AppStrings.AppHomePersianAndArabicLookAsRightAsEnglish)]),
            new("time-zone", BitIconName.Clock, Localizer[nameof(AppStrings.AppHomeTheTimeZoneTheUserPicks)], Localizer[nameof(AppStrings.AppHomeSetInTheAppSMenuAndEveryDateAndTime)]),
            new("translator", BitIconName.Translate, Localizer[nameof(AppStrings.AppHomeAnAITranslator)], Localizer[nameof(AppStrings.AppHomeNewLanguagesTranslatedForYou)]),
            new("found", BitIconName.Search, Localizer[nameof(AppStrings.AppHomeFoundInEveryLanguage)], Localizer[nameof(AppStrings.AppHomeGoogleAndBingIndexEachLanguageSOwn)])
        ]),
        new("workbench", Localizer[nameof(AppStrings.AppHomeDesignWithTheRealThing)], Localizer[nameof(AppStrings.AppHomeTheWorkbenchShowsYourPagesOnSample)],
        [
            new("any-ai", BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeWorksWithAnyAI)], Localizer[nameof(AppStrings.AppHomeNotTiedToOneCompanySDesignTool)]),
            new("real-components", BitIconName.Color, Localizer[nameof(AppStrings.AppHomeRealComponents)], Localizer[nameof(AppStrings.AppHomePagesAreDrawnWithTheComponentsTheApp)]),
            new("states", BitIconName.Devices3, Localizer[nameof(AppStrings.AppHomeEveryStateEveryDevice)], Localizer[nameof(AppStrings.AppHomePhonesTabletsAndDesktopsLightAndDark)]),
            // Side by side, store screenshots and flows are cards of their own
            new("side-by-side", BitIconName.Devices2, Localizer[nameof(AppStrings.AppHomeSideBySideOnEveryDevice)], Localizer[nameof(AppStrings.AppHomeSideBySideOnEveryDeviceText)]),
            new("screenshots", BitIconName.DesktopScreenshot, Localizer[nameof(AppStrings.AppHomeStoreScreenshots)], Localizer[nameof(AppStrings.AppHomeStoreScreenshotsText)]),
            new("flows", BitIconName.Flow, Localizer[nameof(AppStrings.AppHomeFlowsBetweenYourPages)], Localizer[nameof(AppStrings.AppHomeFlowsBetweenYourPagesText)])
        ]),
        new("growth", Localizer[nameof(AppStrings.AppHomeGrowKeepAndEarn)], Localizer[nameof(AppStrings.AppHomeWhatBringsUsersBackIsAlreadyWiredIn)],
        [
            new("review", BitIconName.FavoriteStar, Localizer[nameof(AppStrings.AppHomeStoreReviews)], Localizer[nameof(AppStrings.AppHomeAskForARatingInsideTheAppAtTheRight)]),
            new("messages", BitIconName.Mail, Localizer[nameof(AppStrings.AppHomeEmailAndSMS)], Localizer[nameof(AppStrings.AppHomeSendThemFromYourOwnApp)]),
            new("push", BitIconName.Ringer, Localizer[nameof(AppStrings.NewProjectPush)], Localizer[nameof(AppStrings.AppHomeFreeOfChargeOnTheWebAndroidIOSAnd)]),
            new("links", BitIconName.Link, Localizer[nameof(AppStrings.AppHomeUniversalLinksAndAppLinks)], Localizer[nameof(AppStrings.AppHomeALinkOpensInTheAppWhenItIsInstalled)]),
            new("ads", BitIconName.Money, Localizer[nameof(AppStrings.AppHomeRewardedAds)], Localizer[nameof(AppStrings.AppHomeLetUsersEarnSomethingByWatchingAnAd)]),
            new("payments", BitIconName.ShoppingCart, Localizer[nameof(AppStrings.AppHomePayments)], Localizer[nameof(AppStrings.AppHomeTakePaymentsInTheApp)])
        ]),
        new("admin", Localizer[nameof(AppStrings.AppHomeAnAdminPanelThatKeepsUp)], Localizer[nameof(AppStrings.AppHomeForThePeopleWhoRunTheBusinessEvery)],
        [
            new("live-dashboard", BitIconName.BarChartVerticalFill, Localizer[nameof(AppStrings.AppHomeALiveDashboard)], Localizer[nameof(AppStrings.AppHomeNumbersUpdateByThemselvesAsThings)]),
            new("concurrency", BitIconName.People, Localizer[nameof(AppStrings.AppHomeNoLostChanges)], Localizer[nameof(AppStrings.AppHomeWhenTwoPeopleEditTheSameThingThe)]),
            new("database", BitIconName.Database, Localizer[nameof(AppStrings.AppHomeYourChoiceOfDatabase)], Localizer[nameof(AppStrings.AppHomeSQLServerPostgreSQLMySQLOrSQLite)], Note: DatabaseNote)
        ]),
        new("windows", Localizer[nameof(AppStrings.AppHomeAWindowsAppForTheCounterToo)], Localizer[nameof(AppStrings.AppHomeOneFileToRunAndItKeepsItselfUpToDate)],
        [
            new("portable", BitIconName.WindowsLogo, Localizer[nameof(AppStrings.AppHomePortable)], Localizer[nameof(AppStrings.AppHomeNoInstallerNeededAndItRunsOnWindows7)]),
            new("updates", BitIconName.Sync, Localizer[nameof(AppStrings.AppHomeUpdatesItself)], Localizer[nameof(AppStrings.AppHomeEveryUserGetsTheLatestVersionWithout)]),
            new("kiosk", BitIconName.TVMonitor, Localizer[nameof(AppStrings.AppHomeKioskMode)], Localizer[nameof(AppStrings.AppHomeOnlyYourAppOnTheScreenForShops)])
        ]),
        new("batteries", Localizer[nameof(AppStrings.AppHomeBatteriesIncluded)], Localizer[nameof(AppStrings.AppHomeThePartsOfAnAppNobodyWantsToBuild)],
        [
            new("components", BitIconName.Puzzle, Localizer[nameof(AppStrings.AppHome100Components)], Localizer[nameof(AppStrings.AppHomeDataGridSchedulerChartsRichText)]),
            new("image-editor", BitIconName.Photo2, Localizer[nameof(AppStrings.AppHomeImageEditor)], Localizer[nameof(AppStrings.AppHomeCropResizeAndDrawOnImagesInTheApp)]),
            new("scanner", BitIconName.QRCode, Localizer[nameof(AppStrings.AppHomeBarcodeScanner)], Localizer[nameof(AppStrings.AppHomeScanQRCodesAndBarcodesWithTheCamera)]),
            new("rebrand", BitIconName.Color, Localizer[nameof(AppStrings.AppHomeRebrand)], Localizer[nameof(AppStrings.AppHomeShowTheAIASiteOrADesignYouLikeAnd)]),
            new("port", BitIconName.Forward, Localizer[nameof(AppStrings.AppHomePort)], Localizer[nameof(AppStrings.AppHomeBringAnAppYouAlreadyHaveOverFeature)]),
            new("deploy", BitIconName.Rocket, Localizer[nameof(AppStrings.AppHomeDeploy)], Localizer[nameof(AppStrings.AppHomeToYourOwnServerEasilyAndSecurelyAtA)]),
            new("troubleshoot", BitIconName.Repair, Localizer[nameof(AppStrings.AppHomeTroubleshoot)], Localizer[nameof(AppStrings.AppHomeTellYourAIWhatWentWrongOnTheLiveApp)]),
            new("dev-mcp", BitIconName.PlugConnected, Localizer[nameof(AppStrings.AppHomeYourAILooksIntoTheLiveApp)], Localizer[nameof(AppStrings.AppHomeYourAILooksIntoTheLiveAppText)]),
            new("ai-dlc", BitIconName.TestBeaker, Localizer[nameof(AppStrings.AppHomeAIDLC)], Localizer[nameof(AppStrings.AppHomeTheAIWorksLikeATeamOfExperienced2)]),
            new("up-to-date", BitIconName.PostUpdate, Localizer[nameof(AppStrings.AppHomeAlwaysUpToDate)], Localizer[nameof(AppStrings.AppHomeOurTeamMakes500ImprovementsAMonthOn)])
        ], Localizer[nameof(AppStrings.AppHomeSeeEveryComponent)], "https://blazorui.bitplatform.dev",
        // After the image editor and the barcode scanner, components too, so the Accordion closes the components (2026-11-11)
        new("scanner", Localizer[nameof(AppStrings.AppHomeShowcaseTitle)], Localizer[nameof(AppStrings.AppHomeShowcaseIntro)],
        [
            new("data-grid", BitIconName.Table, Localizer[nameof(AppStrings.AppHomeDataGrid)], Localizer[nameof(AppStrings.AppHomeDataGridText)], Url: "https://blazorui.bitplatform.dev/components/datagrid#example2"),
            new("full-calendar", BitIconName.Calendar, Localizer[nameof(AppStrings.AppHomeFullCalendar)], Localizer[nameof(AppStrings.AppHomeFullCalendarText)], Url: "https://blazorui.bitplatform.dev/components/fullcalendar#example1"),
            new("map", BitIconName.MapPin, Localizer[nameof(AppStrings.AppHomeMap)], Localizer[nameof(AppStrings.AppHomeMapText)], Url: "https://blazorui.bitplatform.dev/components/map#example2"),
            new("pdf-viewer", BitIconName.PDF, Localizer[nameof(AppStrings.AppHomePdfViewer)], Localizer[nameof(AppStrings.AppHomePdfViewerText)], Url: "https://blazorui.bitplatform.dev/components/pdfviewer#example1"),
            new("charts", BitIconName.PieDouble, Localizer[nameof(AppStrings.AppHomeCharts)], Localizer[nameof(AppStrings.AppHomeChartsText)], Url: "https://blazorui.bitplatform.dev/components/chart#pie-pie"),
            new("file-upload", BitIconName.CloudUpload, Localizer[nameof(AppStrings.AppHomeFileUpload)], Localizer[nameof(AppStrings.AppHomeFileUploadText)], Url: "https://blazorui.bitplatform.dev/components/fileupload#example16"),
            new("resource-timeline", BitIconName.TimelineMatrixView, Localizer[nameof(AppStrings.AppHomeResourceTimeline)], Localizer[nameof(AppStrings.AppHomeAndManyOtherComponents)], Url: "https://blazorui.bitplatform.dev/components/resourcetimeline#example10")
        ])),
        new("fast", Localizer[nameof(AppStrings.AppHomeFastAndWatchedOver)], Localizer[nameof(AppStrings.AppHomeLowerCostsFasterPagesAndErrorsYou)],
        [
            new("caching", BitIconName.SpeedHigh, Localizer[nameof(AppStrings.AppHomeCaching)], Localizer[nameof(AppStrings.AppHomePagesFilesAndDataServedFromCloseTo)]),
            new("telemetry", BitIconName.Health, Localizer[nameof(AppStrings.AppHomeTelemetry)], Localizer[nameof(AppStrings.AppHomeErrorsCaughtAndReportedWithSmart)]),
            new("zero-downtime", BitIconName.Rocket, Localizer[nameof(AppStrings.AppHomeZeroDowntimeDeployment)], Localizer[nameof(AppStrings.AppHomeNewVersionsGoLiveWhileYourUsersKeep)])
        ]),
        new("watch", Localizer[nameof(AppStrings.AppHomeWeWatchOverItOnceItSLive)], Localizer[nameof(AppStrings.AppHomeBitFabricKeepsAnEyeOnYourLiveAppAnd)],
        [
            new("health", BitIconName.Heart, Localizer[nameof(AppStrings.AppHomeHealthMonitoring)], Localizer[nameof(AppStrings.AppHomeTheDatabaseEmailSMSAndTheRestChecked)]),
            // The app's other sites, read from its own health check
            new("websites", BitIconName.Website, Localizer[nameof(AppStrings.AppHomeEverythingYouRunEvenAWordPressBlog)], Localizer[nameof(AppStrings.AppHomeListYourLandingPageBlogAndDocsIn)]),
            // The SPF and DMARC of the domain the app sends from, read from its own health check
            new("email-delivery", BitIconName.MailCheck, Localizer[nameof(AppStrings.AppHomeEmailThatReachesTheInbox)], Localizer[nameof(AppStrings.AppHomeYourDomainSSPFAndDMARCChecked)]),
            new("security-scan", BitIconName.Shield, Localizer[nameof(AppStrings.AppHomeSecurityScanner)], Localizer[nameof(AppStrings.AppHomeAdminPagesLeftOpenDefaultPasswords)]),
            new("performance-scan", BitIconName.Stopwatch, Localizer[nameof(AppStrings.AppHomePerformanceScanner)], Localizer[nameof(AppStrings.AppHomeHowFastYourPagesOpenWithAFixYourAI)]),
            new("seo-scan", BitIconName.Search, Localizer[nameof(AppStrings.AppHomeSEOScanner)], Localizer[nameof(AppStrings.AppHomeWhatKeepsYourPagesOutOfGoogleAndBing)]),
            new("accessibility-scan", BitIconName.AccessibilityChecker, Localizer[nameof(AppStrings.AppHomeAccessibilityScanner)], Localizer[nameof(AppStrings.AppHomeWhetherEveryoneCanUseYourAppScreen)]),
            new("status-page", BitIconName.SyncStatus, Localizer[nameof(AppStrings.AppHomeAStatusPage)], Localizer[nameof(AppStrings.AppHomeOnYourOwnDomainSoYourCustomersSee)]),
            // No "New" tag, as everything here is
            new("alerts", BitIconName.Ringer, Localizer[nameof(AppStrings.AppHomeAlertsInSlackTeamsAndDiscord)], Localizer[nameof(AppStrings.AppHomeYourTeamHearsItWhereItAlreadyTalks)])
        ],
        // When the scanners run
        Note: Localizer[nameof(AppStrings.AppHomeBitFabricSeesEveryNewDeploymentOfYours)]),
        new("agents", Localizer[nameof(AppStrings.AppHomeWorksWithEveryAgentOnEveryComputer)], Localizer[nameof(AppStrings.AppHomeBringTheAIYouAlreadyPayFor)],
        [
            new("agents", BitIconName.Robot, Localizer[nameof(AppStrings.AppHomeGitHubCopilotClaudeGemini)], Localizer[nameof(AppStrings.AppHomeOrAModelRunningOnYourOwnComputer2)]),
            new("os", BitIconName.ThisPC, Localizer[nameof(AppStrings.AppHomeMacOSWindowsAndLinux)], Localizer[nameof(AppStrings.AppHomeBuildOnWhicheverComputerYouHave)], Note: ComputersNote)
        ],
        // What each agent reads in the project, and a page for each
        LinkText: Localizer[nameof(AppStrings.ForSeeEveryAgent)], LinkUrl: CultureInfoManager.GetCurrentCultureUrl(PageUrls.For))
    ];

    /// <summary>A few lines rather than a film.</summary>
    private AppHomeNote DatabaseNote => new(
        Localizer[nameof(AppStrings.AppHomeYourAppComesWithPostgreSQLFastProven)],
        [],
        Localizer[nameof(AppStrings.AppHomeIfAClientOfYoursWantsAnotherDatabase)]);

    /// <summary>Which apps each computer runs while you build, and that the pipeline builds them all anyway.</summary>
    private AppHomeNote ComputersNote => new(
        Localizer[nameof(AppStrings.AppHomeBuildOnWhicheverComputerYouHaveWhile)],
        [
            ("Windows", Localizer[nameof(AppStrings.AppHomeAndroidWindowsAndTheWeb)]),
            ("macOS", Localizer[nameof(AppStrings.AppHomeAndroidIOSMacOSAndTheWeb)]),
            ("Linux", Localizer[nameof(AppStrings.AppHomeAndroidAndTheWeb)])
        ],
        Localizer[nameof(AppStrings.AppHomeThatSOnlyForTryingThemAsYouBuildYou)]);

    /// <summary>The landing page's scores, as the samples on landingpage.bitplatform.dev get them on a phone.</summary>
    private string[] Scores => [Localizer[nameof(AppStrings.AppHomePerformance)], Localizer[nameof(AppStrings.Accessibility)], Localizer[nameof(AppStrings.AppHomeBestPractices)], Localizer[nameof(AppStrings.AppHomeSEO)]];

    private (string Name, string Text, string Url)[] Customers =>
    [
        ("Rigid Robotics", Localizer[nameof(AppStrings.AppHomeAnIoTAndMachineLearningPlatformFor)], "https://rigidrobotics.com/"),
        ("Antargyan", Localizer[nameof(AppStrings.AppHomeAppsForMoreThan500000Students)], "https://www.antargyan.ai/"),
        ("SCA", Localizer[nameof(AppStrings.AppHomeAppsForDrivers)], "https://www.sca.com/en/"),
        ("CrystaCode.ai", Localizer[nameof(AppStrings.AppHomeEveryAppItBuildsAgentStudioAmongThem)], "https://crystacode.ai/agent-studio")
    ];

    /// <summary>
    /// bit Fabric itself first, then the bit platform team's demos built on the same template.
    /// bit Fabric's store links aren't known yet, so those buttons lead nowhere in the design. Its Windows app is this
    /// environment's installer (2026-11-10): Production's on bitfabric.co, Test's on Test. The admin panel's iOS app is
    /// its macOS one too.
    /// </summary>
    private AppHomeTry[] Tries =>
    [
        new("bit Fabric", Localizer[nameof(AppStrings.AppHomeBitFabricItselfIsBuiltWithBitFabric)],
            [(Localizer[nameof(AppStrings.AppHomeWeb)], "https://bitfabric.co"), ("Android", null), ("iOS", null), ("Windows", clientCoreSettings.WindowsDownloadUrl), ("macOS", null)]),
        new(Localizer[nameof(AppStrings.AppHomeAdminPanelDemo)], Localizer[nameof(AppStrings.AppHomeABackOfficeBuiltOnTheSameTemplateA)],
            [(Localizer[nameof(AppStrings.AppHomeWeb)], AppHomeNumbers.AdminPanelDemoUrl),
             ("Android", "https://play.google.com/store/apps/details?id=com.bitplatform.AdminPanel.Template"),
             ("iOS", "https://apps.apple.com/app/bit-adminpanel/id6450611349"),
             ("Windows", "https://windows-admin.bitplatform.dev/AdminPanel.Client.Windows-win-Setup.exe"),
             ("macOS", "https://apps.apple.com/app/bit-adminpanel/id6450611349")]),
        new(Localizer[nameof(AppStrings.AppHomeSalesDemo)], Localizer[nameof(AppStrings.AppHomeAShopBuiltOnTheSameTemplateProduct)],
            [(Localizer[nameof(AppStrings.AppHomeWeb)], "https://sales.bitplatform.dev/")])
    ];
}
