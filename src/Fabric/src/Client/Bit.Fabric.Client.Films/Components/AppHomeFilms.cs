using static Bit.Fabric.Client.Films.Components.AppHomeDevice;
using static Bit.Fabric.Client.Films.Components.AppHomeMark;
using static Bit.Fabric.Client.Films.Components.AppHomePerson;
using static Bit.Fabric.Client.Films.Components.AppHomePicture;
using static Bit.Fabric.Client.Films.Components.AppHomeTone;

namespace Bit.Fabric.Client.Films.Components;

/// <summary>
/// Every feature's film, by the feature's id: a few scenes of the visitor's own app, on the device that shows the feature
/// best, each with the example that fits it best: a shop's orders and products, a team's tasks, the companies a SaaS serves.
/// Not one business for all of them, and no clinic: a visitor saw themselves as the patient, not as the one building the
/// app. Where people take turns, their faces show whose screen it is. Sample content that shows another language, such as
/// the right-to-left scenes, is written in that language and is not translated with the page. A feature with a note (the
/// database, the computers to build on) shows its few lines instead and has no film. One copy for the app's home page and
/// the Workbench's design of it.
/// </summary>
public sealed class AppHomeFilms(IStringLocalizer<AppStrings> localizer)
{
    /// <summary>
    /// A film's captions as one text, for the prerendered page (bitfabric-internal/specs/home-page.md, "Search engines read
    /// the films"). The captions run on from scene to scene ("...on an iPhone too..."), so the dots where one hands over to
    /// the next become a comma.
    /// Here rather than in the home page, so the page holds no lambda typed with one of this library's types: a Blazor
    /// WebAssembly page that did would fail at its first lambda of any kind, before the library is loaded.
    /// </summary>
    public string CaptionsOf(string id) =>
        System.Text.RegularExpressions.Regex.Replace(string.Join(" ", Of(id).Select(scene => scene.Caption)), @"(\.\.\.|…)\s+(\.\.\.|…)", ", ");

    public AppHomeFilmScene[] Of(string id) => id switch
    {
        // Every platform, from one project. Its apps on every platform have no film: their card says it all
        "website" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSearchEnginesReadEveryPageOfYour)],
                [new FilmField(localizer[nameof(AppStrings.AppHomeHandmadeCoffeeMugs)], Typing: true),
                 new FilmRow(BitIconName.Globe, localizer[nameof(AppStrings.AppHomeHandmadeMugsMadeToOrder)], "yourapp.com", Mark: New) { At = 2.2 },
                 new FilmRow(BitIconName.Globe, localizer[nameof(AppStrings.AppHomeAnotherShop)], "example.com") { At = 2.4 }], Browser) { Url = "google.com" },
            new(localizer[nameof(AppStrings.AppHomeAndItOpensAtOnceEvenOnASlowPhone)], [.. ShopHome]) { Url = "yourapp.com" }
        ],
        "admin-panel" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheBackOfficeForYouAndYourTeamThe)],
                [new FilmTitle(localizer[nameof(AppStrings.AppDashboardTitle)]), new FilmStat("128", localizer[nameof(AppStrings.AppHomeOrdersThisWeek)]), new FilmBars([40, 55, 35, 70, 60, 85, 75])], Browser) { Url = "yourapp.com/admin" },
            new(localizer[nameof(AppStrings.AppHomeEveryOrderInADataGridToSearchSortAnd)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), OrdersGrid], Browser) { Url = "yourapp.com/admin/orders" },
            new(localizer[nameof(AppStrings.AppHomeTheUsersAndWhatEachOfThemMayDo)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeUsers)]), new FilmRow(BitIconName.Contact, "Emma", "emma@yourapp.com", localizer[nameof(AppStrings.AppHomeAdmin)]) { Person = Emma },
                 new FilmRow(BitIconName.Contact, "Lucas", "lucas@yourapp.com", localizer[nameof(AppStrings.CalendarResourceColumnHeader)]) { Person = Lucas },
                 new FilmRow(BitIconName.Contact, "Sara", "sara@example.com", localizer[nameof(AppStrings.AppHomeCustomer)]) { Person = Sara }], Browser) { Url = "yourapp.com/admin/users" },
            new(localizer[nameof(AppStrings.AppHomeAndTheSettingsInTheSameProjectAsYour)],
                [new FilmTitle(localizer[nameof(AppStrings.SettingsTitle)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeFreeShipping)], End: localizer[nameof(AppStrings.AppHomeOver), "$50"]),
                 new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeOrderEmails)], End: localizer[nameof(AppStrings.ThemeStateOn)], EndTone: Good)], Browser) { Url = "yourapp.com/admin/settings" }
        ],
        "tenants" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourOneAppServesManyCompaniesEmma)],
                [new FilmStep("Acme Coffee"), new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1042], localizer[nameof(AppStrings.AppHomeEspressoBeans)], "$48"),
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1043], localizer[nameof(AppStrings.AssistantStockOatMilk)], "$12")], Laptop) { Url = "yourapp.com", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeLucasWorksAtBloomFlowersAndSeesOnly)],
                [new FilmStep("Bloom Flowers"), new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 77], localizer[nameof(AppStrings.AppHomeSpringBouquet)], "$30"),
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 78], localizer[nameof(AppStrings.AppHomeRedRoses)], "$45")], Laptop) { Url = "yourapp.com", Person = Lucas },
            new(localizer[nameof(AppStrings.AppHomeAndSaraWhoWorksForBothPicksThe)],
                [new FilmAppBar(localizer[nameof(AppStrings.NewProjectSampleOrders)], Sara), new FilmTitle(localizer[nameof(AppStrings.AppHomeSwitchCompany)]),
                 new FilmChoice("Acme Coffee", Mark: Picked), new FilmChoice("Bloom Flowers", Tap: 1.4)], Laptop) { Url = "yourapp.com", Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeItSGDPRFriendlyTooUsersCanTakeTheir)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourData)]), new FilmButton(localizer[nameof(AppStrings.AppHomeDownloadMyData)], Tap: 1.4, Outline: true), new FilmButton(localizer[nameof(AppStrings.AppHomeDeleteMyAccount)], Outline: true)], Laptop) { Url = "yourapp.com", Person = Sara }
        ],
        "gdpr" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEachUserPicksWhatTheyAllow)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourPrivacyChoices)]),
                 new FilmRow(BitIconName.BarChartVerticalFill, localizer[nameof(AppStrings.AppHomeUsageStatistics)], End: localizer[nameof(AppStrings.ThemeStateOn)], EndTone: Good),
                 new FilmRow(BitIconName.Money, localizer[nameof(AppStrings.AppHomePersonalizedAds)], End: localizer[nameof(AppStrings.ThemeStateOff)], Mark: New) { At = 1 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeDownloadsEverythingTheAppKeepsAbout)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeDownloadYourData)]), new FilmButton(localizer[nameof(AppStrings.AppHomeDownloadMyData)], Tap: 1),
                 new FilmRow(BitIconName.Download, "your-data.zip", localizer[nameof(AppStrings.AppHomeProfileOrdersAndFiles)], Mark: New) { At = 1.6 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndDeletesTheirAccountWithAllItHeld)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeAccount)]), new FilmButton(localizer[nameof(AppStrings.AppHomeDeleteMyAccount)], Tap: 1, Tone: Bad, Outline: true),
                 new FilmNote(localizer[nameof(AppStrings.AppHomeYourAccountAndItsDataAreDeleted)]) { At = 1.8 }]) { Person = Sara }
        ],
        "offline" =>
        [
            new(localizer[nameof(AppStrings.AppHomeNoConnectionTheAppKeepsWorkingAnd)],
                [new FilmNote(localizer[nameof(AppStrings.AppHomeOffline)], Bad), new FilmTitle(localizer[nameof(AppStrings.ThemeNotes)]),
                 new FilmRow(BitIconName.Edit, localizer[nameof(AppStrings.AppHomeOrderMoreBoxes)], End: localizer[nameof(AppStrings.AppHomeSavedHere)], Mark: New) { At = 1 },
                 new FilmRow(BitIconName.Edit, localizer[nameof(AppStrings.AppHomeCallTheSupplier)], End: localizer[nameof(AppStrings.AppHomeSavedHere)])]),
            new(localizer[nameof(AppStrings.AppHomeAndWhenItSBackEverythingSyncsBy)],
                [new FilmNote(localizer[nameof(AppStrings.AppHomeBackOnline)]), new FilmTitle(localizer[nameof(AppStrings.ThemeNotes)]),
                 new FilmRow(BitIconName.Edit, localizer[nameof(AppStrings.AppHomeOrderMoreBoxes)], End: localizer[nameof(AppStrings.AppHomeSynced)], EndTone: Good, Mark: New) { At = .8 },
                 new FilmRow(BitIconName.Edit, localizer[nameof(AppStrings.AppHomeCallTheSupplier)], End: localizer[nameof(AppStrings.AppHomeSynced)], EndTone: Good, Mark: New) { At = 1.1 }])
        ],

        // Sign-in
        "force-update" =>
        [
            new(localizer[nameof(AppStrings.AppHomeWhenAnOldVersionHasToGoEveryoneStill)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders,
                 new FilmSheet(localizer[nameof(AppStrings.AppHomeTimeForAnUpdateThisOneIsRequiredTo)], localizer[nameof(AppStrings.AppHomeUpdate)], Tap: 2) { At = .8 }]) { Label = localizer[nameof(AppStrings.AppHomeVersion), "2.3"] },
            new(localizer[nameof(AppStrings.AppHomeATapAndTheyReOnTheNewOne)],
                [new FilmNote(localizer[nameof(AppStrings.AppHomeVersion), "2.4"]), new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders]) { Label = localizer[nameof(AppStrings.AppHomeVersion), "2.4"] },
            new(localizer[nameof(AppStrings.AppHomeAndOnTheWebAndOnWindowsTheAppUpdates)],
                [new FilmNote(localizer[nameof(AppStrings.AppHomeUpdatedToVersion), "2.4"]) { At = .5 }, new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders], Browser) { Url = "yourapp.com" }
        ],
        "social" =>
        [
            new(localizer[nameof(AppStrings.AppHomeUsersSignInWithAnAccountTheyAlready)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignIn)]), new FilmButton(localizer[nameof(AppStrings.AppHomeContinueWithGoogle)], Tap: 1.4, Outline: true), new FilmButton(localizer[nameof(AppStrings.AppHomeContinueWithApple)], Outline: true),
                 new FilmButton(localizer[nameof(AppStrings.AppHomeContinueWithMicrosoft)], Outline: true)]),
            new(localizer[nameof(AppStrings.AppHomePickItWithOneTap)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeChooseAnAccount)]), new FilmRow(BitIconName.Contact, "Sara", "sara@gmail.com") { Person = Sara, Tap = 1.2 },
                 new FilmRow(BitIconName.AddFriend, localizer[nameof(AppStrings.AppHomeUseAnotherAccount)])]) { Url = "accounts.google.com" },
            new(localizer[nameof(AppStrings.AppHomeAndTheyReInNoPasswordNoFormNoEmailTo)],
                [new FilmDone(localizer[nameof(AppStrings.AppHomeWelcomeSara)], Sub: localizer[nameof(AppStrings.AppHomeNoExtraSteps)])]),
            new(localizer[nameof(AppStrings.AppHomeGoogleAppleMicrosoftGitHubXFacebook)],
                [new FilmLogos(localizer[nameof(AppStrings.AppHomeAndManyMoreReadyToSwitchOn)])])
        ],
        "two-step" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYouSignInWithYourPhoneNumber)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignIn)]), new FilmField("+31 6 1234 5678"), new FilmButton(localizer[nameof(AppStrings.AppHomeSendCode)], Tap: 1.2)]) { Milliseconds = 2600 },
            new(localizer[nameof(AppStrings.AppHomeTheCodeArrivesBySMSAndThePhoneOffers)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeStep1Of2)]), new FilmTitle(localizer[nameof(AppStrings.AppHomeEnterTheCodeWeSent)]), new FilmCode("482913", Fill: false),
                 new FilmBanner(localizer[nameof(AppStrings.AppHomeMessages)], localizer[nameof(AppStrings.AppHomeYourCodeIs), "482913"]) { At = .4 },
                 new FilmSheet(localizer[nameof(AppStrings.AppHomeLetTheAppReadThisOneCode)], localizer[nameof(AppStrings.AssistantApprove)], Tap: 2.3) { At = 1.1 }]) { Milliseconds = 3400 },
            new(localizer[nameof(AppStrings.AppHomeOneTapAndTheCodeIsInNothingToCopy)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeStep1Of2)]), new FilmTitle(localizer[nameof(AppStrings.AppHomeEnterTheCodeWeSent)]), new FilmCode("482913"), new FilmNote(localizer[nameof(AppStrings.AppHomeFilledInByItself)]) { At = 1 }]) { Milliseconds = 2600 },
            new(localizer[nameof(AppStrings.AppHomeTheSecondStepMustBeAnotherWaySMSWas)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeStep2Of2)]), new FilmTitle(localizer[nameof(AppStrings.AppHomeOneMoreWayToProveItSYou)]),
                 new FilmChoice(localizer[nameof(AppStrings.AppHomeTextMessage)], localizer[nameof(AppStrings.AppHomeUsedInStep1)], Used), new FilmChoice(localizer[nameof(AppStrings.AppHomeAuthenticatorApp)], Mark: Picked, Tap: 1.6), new FilmChoice(localizer[nameof(AppStrings.AppHomeEmail)])]) { Milliseconds = 3800 },
            new(localizer[nameof(AppStrings.AppHomeTheCodeFromAnAuthenticatorAppProves)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeStep2Of2)]), new FilmTitle(localizer[nameof(AppStrings.AppHomeEnterTheCodeFromYourApp)]), new FilmCode("731904")]) { Milliseconds = 2600 },
            new(localizer[nameof(AppStrings.AppHomeSignedInWithTwoDifferentProofs)], [new FilmDone(localizer[nameof(AppStrings.AppHomeYouReSignedIn)])]) { Milliseconds = 3000 }
        ],
        // Magic links and quick sign-in, each a card and a film of its own
        "magic" =>
        [
            new(localizer[nameof(AppStrings.AppHomeNoPasswordToRememberTheAppEmailsYouA)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignIn)]), new FilmField("sara@example.com", localizer[nameof(AppStrings.AppHomeEmail)]), new FilmButton(localizer[nameof(AppStrings.AppHomeEmailMeALink)], Tap: 1.2)]),
            new(localizer[nameof(AppStrings.AppHomeTheSameEmailHasA6DigitCode)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeTapToSignInToYourApp)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignIn)], Tap: 2.4),
                 new FilmText(localizer[nameof(AppStrings.AppHomeOrEnterThisCode)]) { At = .6 }, new FilmCode("530172") { At = .8 }]) { Url = "mail.example.com" },
            new(localizer[nameof(AppStrings.AppHomeOneTapOnTheLinkOrTheCodeAndYouReIn)], [new FilmDone(localizer[nameof(AppStrings.AppHomeYouReSignedIn)])])
        ],
        "quick-sign-in" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSaraIsChattingWithYourAppSAIAssistant)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeDoYouHaveTheBlueMugInStock)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeYes4LargeOnesAreLeft)]) { At = .9 },
                 new FilmBubble(localizer[nameof(AppStrings.AppHomePutOneAsideForMe)], Mine: true) { At = 1.8 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAtHerThirdMessageItAsksHerToSignIn)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeDoYouHaveTheBlueMugInStock)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeYes4LargeOnesAreLeft)]),
                 new FilmBubble(localizer[nameof(AppStrings.AppHomePutOneAsideForMe)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeSignInAndIllPutItAsideForYou)]) { At = .4 },
                 new FilmDialog(localizer[nameof(AppStrings.AppHomeSignInToContinue)], [new FilmField("sara@example.com", localizer[nameof(AppStrings.AppHomeEmail)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignIn)], Tap: 2.6)]) { At = 1.2 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndTheChatGoesOnWhereItWas)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomePutOneAsideForMe)], Mine: true), new FilmNote(localizer[nameof(AppStrings.AppHomeSignedInNothingLost)]) { At = .3 },
                 new FilmBubble(localizer[nameof(AppStrings.AppHomeDoneSaraALargeBlueMugIsYoursUntil)]) { At = 1 }]) { Person = Sara }
        ],
        "return-url" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSaraLikesAMugOnHerPhoneAndHasToSign)],
                [.. ProductPage, new FilmButton(localizer[nameof(AppStrings.AppHomeAddToCart)], Tap: 1.2)]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeSoSheAsksForALinkByEmail)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignIn)]), new FilmField("sara@example.com", localizer[nameof(AppStrings.AppHomeEmail)]), new FilmButton(localizer[nameof(AppStrings.AppHomeEmailMeALink)], Tap: 1.2)]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeOpensTheEmailOnHerComputer)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeInbox)]), new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeYourApp)], localizer[nameof(AppStrings.AppHomeTapToSignInToYourApp)], Mark: New),
                 new FilmButton(localizer[nameof(AppStrings.AppHomeSignIn)], Tap: 1.4)], Laptop) { Url = "mail.example.com", Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndLandsOnThatSameMugSignedIn)],
                [new FilmNote(localizer[nameof(AppStrings.AppHomeSignedIn)]) { At = .4 }, .. ProductPage], Laptop) { Url = "yourapp.com/products/blue-mug", Person = Sara }
        ],
        // Face ID and fingerprint, each a card and a film of its own
        "face-id" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSignInWithoutAPassword)], [new FilmTitle(localizer[nameof(AppStrings.AppHomeWelcomeBack)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignInWithFaceID)], Tap: 1.2)]),
            new(localizer[nameof(AppStrings.AppHomeThePhoneLooksAtYourFace)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeFaceID)]), new FilmPicture(Face), new FilmText(localizer[nameof(AppStrings.AppHomeHoldThePhoneInFrontOfYou)])]),
            new(localizer[nameof(AppStrings.AppHomeAndYouReIn)], [new FilmDone(localizer[nameof(AppStrings.AppHomeYouReSignedIn)])])
        ],
        "fingerprint" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSignInWithoutAPassword)], [new FilmTitle(localizer[nameof(AppStrings.AppHomeWelcomeBack)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignInWithYourFingerprint)], Tap: 1.2)]),
            new(localizer[nameof(AppStrings.AppHomeYouTouchTheSensor)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeFingerprint)]), new FilmPicture(Fingerprint), new FilmText(localizer[nameof(AppStrings.AppHomeTouchTheSensorWithYourFinger)])]),
            new(localizer[nameof(AppStrings.AppHomeAndYouReIn)], [new FilmDone(localizer[nameof(AppStrings.AppHomeYouReSignedIn)])])
        ],
        "elevated" =>
        [
            new(localizer[nameof(AppStrings.AppHomeDeletingAUserIsSensitive)],
                [.. UsersList(lucasGone: false), new FilmNote(localizer[nameof(AppStrings.AppHomeSignedIn2HoursAgo)], Neutral)], Browser) { Url = "yourapp.com/admin/users" },
            new(localizer[nameof(AppStrings.AppHomeSoFirstTheAppAsksYouToProveItSYou)],
                [.. UsersList(lucasGone: false),
                 new FilmDialog(localizer[nameof(AppStrings.AppHomeIsItReallyYou)], [new FilmText(localizer[nameof(AppStrings.AppHomeEnterTheCodeWeSentBeforeYouDelete)]), new FilmCode("519204") { At = 1.1 }, new FilmButton(localizer[nameof(AppStrings.AppHomeConfirm)], Tap: 2.7)]) { At = .3 }],
                Browser) { Url = "yourapp.com/admin/users" },
            new(localizer[nameof(AppStrings.AppHomeThenLucasIsDeletedAndForAFewMinutes)],
                [.. UsersList(lucasGone: true), new FilmNote(localizer[nameof(AppStrings.AppHomeLucasWasDeleted)]) { At = .3 }, new FilmNote(localizer[nameof(AppStrings.AppHomeUnlockedFor500)], Neutral) { At = .8 }], Browser) { Url = "yourapp.com/admin/users" }
        ],
        "privileged" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSaySaraSPlanAllowsTwoDevicesAtOnce)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignedInOn)]), new FilmRow(BitIconName.CellPhone, localizer[nameof(AppStrings.AppHomePhone)], localizer[nameof(AppStrings.AppHomeThisDevice)], Mark: New),
                 new FilmNote(localizer[nameof(AppStrings.AppHomeOfDevices), 1, 2], Neutral)]) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomePhone)], Milliseconds = 5200 },
            new(localizer[nameof(AppStrings.AppHomeThenOnHerLaptopTwoOfTwo)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignedInOn)]), new FilmRow(BitIconName.CellPhone, localizer[nameof(AppStrings.AppHomePhone)]),
                 new FilmRow(BitIconName.ThisPC, localizer[nameof(AppStrings.AppHomeLaptop)], localizer[nameof(AppStrings.AppHomeThisDevice)], Mark: New), new FilmNote(localizer[nameof(AppStrings.AppHomeOfDevices), 2, 2], Neutral)], Laptop) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndHerTabletHitsTheWallTwoIsTheLimit)],
                [new FilmDone(localizer[nameof(AppStrings.AppHomeTooManyDevices)], Bad, localizer[nameof(AppStrings.AppHomeYourPlanAllowsAtOnce), 2]),
                 new FilmRow(BitIconName.CellPhone, localizer[nameof(AppStrings.AppHomePhone)], End: localizer[nameof(AppStrings.AppHomeSignOut)], EndTone: Bad) { Tap = 2.4 }, new FilmRow(BitIconName.ThisPC, localizer[nameof(AppStrings.AppHomeLaptop)])],
                Tablet) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeTablet)] },
            new(localizer[nameof(AppStrings.AppHomeAndThenTheTabletComesInAsOnStreaming)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignedInOn)]), new FilmRow(BitIconName.ThisPC, localizer[nameof(AppStrings.AppHomeLaptop)]),
                 new FilmRow(BitIconName.Tablet, localizer[nameof(AppStrings.AppHomeTablet)], localizer[nameof(AppStrings.AppHomeThisDevice)], Mark: New) { At = .4 },
                 new FilmRow(BitIconName.CellPhone, localizer[nameof(AppStrings.AppHomePhone)], End: localizer[nameof(AppStrings.SignedOut)], Mark: Used), new FilmNote(localizer[nameof(AppStrings.AppHomeOfDevices), 2, 2], Neutral)],
                Tablet) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeTablet)] }
        ],
        "accounts" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSaraAndEmmaShareTheShopSTabletSaraIs)],
                [new FilmAppBar(localizer[nameof(AppStrings.AppHomeTasks)], Sara), new FilmStep("sara@acme.com"), .. SaraTasks], Tablet) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeEmmaOpensTheMenuAndPicksHerOwn)],
                [new FilmAppBar(localizer[nameof(AppStrings.AppHomeTasks)], Sara, Tap: .3), new FilmStep("sara@acme.com"), .. SaraTasks,
                 new FilmMenu([new FilmRow(BitIconName.Contact, "Sara", "sara@acme.com", Mark: Picked) { Person = Sara },
                     new FilmRow(BitIconName.Contact, "Emma", "emma@acme.com") { Person = Emma, Tap = 2 },
                     new FilmRow(BitIconName.AddFriend, localizer[nameof(AppStrings.AppHomeAddAnotherAccount)]),
                     new FilmRow(BitIconName.Settings, localizer[nameof(AppStrings.NewProjectSampleProfile)], Mark: Off), new FilmRow(BitIconName.Sunny, localizer[nameof(AppStrings.ThemeLight)], Mark: Off)]) { At = .8 }], Tablet) { Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndSheSInHersWhileSaraStaysSignedIn)],
                [new FilmAppBar(localizer[nameof(AppStrings.AppHomeTasks)], Emma), new FilmStep("emma@acme.com"),
                 new FilmRow(BitIconName.Task, localizer[nameof(AppStrings.AppHomeUpdateThePrices)], End: localizer[nameof(AppStrings.AssistantToday)], Mark: New) { At = .2 },
                 new FilmRow(BitIconName.Task, localizer[nameof(AppStrings.AppHomeReplyToNoah)], End: localizer[nameof(AppStrings.AppHomeMon)], Mark: New) { At = .35 },
                 new FilmNote(localizer[nameof(AppStrings.AppHomeSwitchedSaraIsStillSignedIn)]) { At = .8 }], Tablet) { Person = Emma }
        ],
        "groups" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEachUserSeesTheFeaturesOfTheirGroupA)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeMember)]), new FilmTitle(localizer[nameof(AppStrings.ThemeOverlayMenu)]), .. MemberMenu]) { Person = Noah },
            new(localizer[nameof(AppStrings.AppHomeEarlyAdoptersSeeNewOnesFirst)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeEarlyAdopter)]), new FilmTitle(localizer[nameof(AppStrings.ThemeOverlayMenu)]), .. MemberMenu,
                 new FilmRow(BitIconName.Robot, localizer[nameof(AppStrings.AppHomeAIReports)], End: localizer[nameof(AppStrings.AppHomeNew)], Mark: New, EndTone: Accent) { At = .4 }]) { Person = Lucas },
            new(localizer[nameof(AppStrings.AppHomeHigherPlansGetPremiumOnes)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomePremium)]), new FilmTitle(localizer[nameof(AppStrings.ThemeOverlayMenu)]), .. MemberMenu, new FilmRow(BitIconName.Diamond, localizer[nameof(AppStrings.AppHomeExports)], Mark: New) { At = .4 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndSensitiveOnesStayWithTheAdmins)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeAdmin)]), new FilmTitle(localizer[nameof(AppStrings.ThemeOverlayMenu)]), .. MemberMenu, new FilmRow(BitIconName.Permissions, localizer[nameof(AppStrings.AppHomeUsersAndRoles)], Mark: New) { At = .4 }]) { Person = Emma }
        ],
        "ai-sign-in" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYouAskTheAIYouUseEveryDay)],
                [AskForMugs, new FilmBubble(localizer[nameof(AppStrings.AppHomeINeedToSignInToYourappComForYouFirst)]) { At = 1.2 }], Browser) { Url = "grok.com" },
            new(localizer[nameof(AppStrings.AppHomeItSignsInToYourAppWithOAuth21AndYou)],
                [AskForMugs, new FilmDialog("yourapp.com", [new FilmText(localizer[nameof(AppStrings.AppHomeGrokWantsToSeeYourOrdersAndOrderFor)]), new FilmButton(localizer[nameof(AppStrings.AssistantApprove)], Tap: 1.8)]) { At = .3 }], Browser) { Url = "yourapp.com/sign-in" },
            new(localizer[nameof(AppStrings.AppHomeAndThenItActsForYouInsideYourApp)],
                [AskForMugs, new FilmBubble(localizer[nameof(AppStrings.AppHomeDoneTwoBlueMugsArrivingOnFriday), "$48"]) { At = .6 }], Browser) { Url = "grok.com" }
        ],
        // Single sign-on, reCAPTCHA, and the firewall with DDoS protection, each a card and a film of its own
        "sso" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSingleSignOnWithKeycloakStaffSignIn)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignIn)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignInWithYourCompany)], Tap: 1.4), new FilmText(localizer[nameof(AppStrings.AppHomeOrWithYourEmail)])], Browser) { Url = "yourapp.com/sign-in", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeOnTheirCompanySOwnSignInPage)],
                [new FilmTitle("Acme"), new FilmField("emma@acme.com", localizer[nameof(AppStrings.AppHomeEmail)]), new FilmButton(localizer[nameof(AppStrings.AppHomeSignIn)], Tap: 1.4)], Browser) { Url = "sso.acme.com", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndTheyReInWithNoNewPasswordToKeep)],
                [new FilmAppBar(localizer[nameof(AppStrings.AppDashboardTitle)], Emma), new FilmNote(localizer[nameof(AppStrings.AppHomeSignedIn)]) { At = .4 },
                 new FilmStat("128", localizer[nameof(AppStrings.AppHomeOrdersThisWeek)])], Browser) { Url = "yourapp.com/admin", Person = Emma }
        ],
        "recaptcha" =>
        [
            new(localizer[nameof(AppStrings.AppHomePeopleGetThroughWithOneTap)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeCreateAnAccount)]), new FilmField("sara@example.com", localizer[nameof(AppStrings.AppHomeEmail)]), new FilmChoice(localizer[nameof(AppStrings.AppHomeIMNotARobot)], Mark: Picked, Tap: 1.2)], Browser) { Url = "yourapp.com/sign-up", Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeWhileBotsAreTurnedAwayBeforeTheyMake)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSignUpsToday)]), new FilmRow(BitIconName.Contact, "Sara", "sara@example.com", localizer[nameof(AppStrings.AppHomeNew)], EndTone: Good) { Person = Sara },
                 new FilmRow(BitIconName.Blocked2, localizer[nameof(AppStrings.AppHomeAttemptsByBots), "1,240"], End: localizer[nameof(AppStrings.AppHomeBlocked)], Mark: New, EndTone: Bad) { At = .7 }], Browser) { Url = "yourapp.com/admin/users" }
        ],
        "firewall" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAWebApplicationFirewallStopsAttacks)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeTraffic)]),
                 new FilmRow(BitIconName.Shield, localizer[nameof(AppStrings.AppHomeAnAttackOnAKnownSecurityFlaw)], End: localizer[nameof(AppStrings.AppHomeBlocked)], Mark: New, EndTone: Bad) { At = .6 },
                 new FilmRow(BitIconName.People, localizer[nameof(AppStrings.AppHomeYourUsers)], End: localizer[nameof(AppStrings.AppHomeServed)], EndTone: Good)], Browser),
            new(localizer[nameof(AppStrings.AppHomeAndAFloodOfTrafficMeantToTakeItDown)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeTraffic)]),
                 new FilmRow(BitIconName.Shield, localizer[nameof(AppStrings.AppHomeAFloodOf2MillionRequestsAMinute)], End: localizer[nameof(AppStrings.AppHomeBlocked)], Mark: New, EndTone: Bad) { At = .6 },
                 new FilmRow(BitIconName.People, localizer[nameof(AppStrings.AppHomeYourUsers)], End: localizer[nameof(AppStrings.AppHomeServed)], EndTone: Good)], Browser)
        ],

        // The AI assistant
        "focused" =>
        [
            new(localizer[nameof(AppStrings.AppHomeItAnswersAboutYourApp)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeHowDoIReturnAnOrder)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeOpenOrdersTapTheOrderThenReturnIt)]) { At = 1 }]),
            new(localizer[nameof(AppStrings.AppHomeAndPolitelyTurnsTheRestDown)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeWhoWinsTheMatchTonight)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeICanOnlyHelpWithOurShopAnythingAbout)]) { At = 1 }])
        ],
        "device-support" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAskedInEdgeOnWindows)],
                [HowToInstall, new FilmBubble(localizer[nameof(AppStrings.AppHomeClickTheInstallButtonAtTheEndOfEdgeS)]) { At = 1 }], Desktop) { Label = "Windows · Edge" },
            new(localizer[nameof(AppStrings.AppHomeAndOnAnAndroidPhoneTheAnswerFitsThe)],
                [HowToInstall, new FilmBubble(localizer[nameof(AppStrings.AppHomeOpenChromeSMenuAndTapAddToHomeScreen)]) { At = 1 }]) { Label = "Android · Chrome" }
        ],
        "search-data" =>
        [
            new(localizer[nameof(AppStrings.AppHomeItSearchesYourAppSOwnData)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeDoYouHaveTheBlueMugInStock)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeYesInTwoSizes)]) { At = 1 },
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeBlueMugSmall)], End: localizer[nameof(AppStrings.AssistantUnitsLeft), 12], Mark: New) { At = 1.4 },
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeBlueMugLarge)], End: localizer[nameof(AppStrings.AssistantUnitsLeft), 4], Mark: New) { At = 1.6 }]),
            new(localizer[nameof(AppStrings.AppHomeInTheUserSOwnLanguage)],
                [new FilmBubble("ماگ آبی موجود دارید؟", Mine: true), new FilmBubble("بله، در دو اندازه:") { At = 1 },
                 new FilmRow(BitIconName.ShoppingCart, "ماگ آبی، کوچک", End: "۱۲ عدد", Mark: New) { At = 1.4 },
                 new FilmRow(BitIconName.ShoppingCart, "ماگ آبی، بزرگ", End: "۴ عدد", Mark: New) { At = 1.6 }]) { Rtl = true }
        ],
        "images" =>
        [
            new(localizer[nameof(AppStrings.AppHomeItWritesTheAltTextOfEveryImage)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeNewProduct)]), new FilmPicture(Photo), new FilmField(localizer[nameof(AppStrings.AppHomeABlueMugOnAWoodenTableInTheSun)], localizer[nameof(AppStrings.AppHomeAltTextWrittenByAI)], Typing: true) { At = .6 }]),
            new(localizer[nameof(AppStrings.AppHomeAndTurnsInappropriateOnesAway)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeNewProduct)]), new FilmPicture(Blocked), new FilmNote(localizer[nameof(AppStrings.AppHomeThisImageCanTBePosted)], Bad) { At = .8 }])
        ],
        "handover" =>
        [
            new(localizer[nameof(AppStrings.AppHomeWhenTheAssistantCanTHelp)],
                [ChargedTwice, new FilmBubble(localizer[nameof(AppStrings.AppHomeICanTLookIntoPaymentsSoIMAskingA)]) { At = 1 }]),
            new(localizer[nameof(AppStrings.AppHomeAPersonTakesOverWithTheWhole)],
                [ChargedTwice, new FilmNote(localizer[nameof(AppStrings.AppHomeEmmaJoinedTheChat)], Neutral) { At = .4 }, new FilmBubble(localizer[nameof(AppStrings.AppHomeHiSaraIMEmmaIVeRefundedTheSecond)]) { At = 1.2 }])
        ],
        // The confirm card is the chat's own (UserApprovalCard), with its texts: a tap on screen, not a typed yes
        "human-in-the-loop" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAskedToClearTheAppSDataOnThisDevice)],
                [ClearAppData, .. ClearAppDataCard(at: 1, tap: 2.8)]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeItAsksForATapOnAConfirmCardSoNothing)],
                [ClearAppData, .. ClearAppDataCard(at: 0, tap: null), new FilmNote(localizer[nameof(AppStrings.AiChatApprovalClearAppFilesApproved)]) { At = .5 }]) { Person = Sara }
        ],
        "voice" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTalkToIt)], [new FilmPicture(Voice, localizer[nameof(AppStrings.AppHomeListening)]), new FilmBubble(localizer[nameof(AppStrings.AppHomeWhereSMyOrder)], Mine: true) { At = 1.6 }]),
            new(localizer[nameof(AppStrings.AppHomeHearItAnswer)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeWhereSMyOrder)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeOnItsWayItArrivesOnFridayMorning)]) { At = .4 }, new FilmNote(localizer[nameof(AppStrings.AppHomeReadingItOutLoud)], Accent) { At = .8 }]),
            new(localizer[nameof(AppStrings.AppHomeOrGiveItAFileToRead)],
                [new FilmRow(BitIconName.Attach, "invoice.pdf", "240 KB", Mark: New), new FilmBubble(localizer[nameof(AppStrings.AppHomeThisInvoiceIsDueOnMonday), "$240"]) { At = 1 }])
        ],
        "brain" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYouTellYourAIAssistantHowToBehaveIn)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeAIAssistantSettings)]), new FilmField(localizer[nameof(AppStrings.AppHomeBeBriefAndWarmTellPeopleAboutThis)], localizer[nameof(AppStrings.AppHomeInstructions)], Typing: true),
                 new FilmButton(localizer[nameof(AppStrings.Save)], Tap: 2.4), new FilmNote(localizer[nameof(AppStrings.AppHomeSavedAndLiveNow)]) { At = 2.8 }], Laptop) { Url = "yourapp.com/admin/assistant", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndAMomentLaterItTalksToYour)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeHi)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeHiSaraThisWeekEveryMugIs20OffShallI)]) { At = .8 }]) { Person = Sara }
        ],
        "agentic" =>
        [
            new(localizer[nameof(AppStrings.AppHomeItDoesnTOnlyAnswerItActsWithTheTools)],
                [ChangeOrder, new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeLookingAtOrder), 1044], Mark: New) { At = .8 },
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeChangingTheMug)], Mark: New) { At = 1.6 }]),
            new(localizer[nameof(AppStrings.AppHomeBuiltOnMicrosoftAgentFramework)],
                [ChangeOrder, new FilmBubble(localizer[nameof(AppStrings.AppHomeDoneOrderHasTheBlueMugNowIEmailedYou), 1044]) { At = .5 },
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1044], localizer[nameof(AppStrings.AppHomeBlueMug)], "$24", Mark: New) { At = 1 }])
        ],
        // The app's MCP server in grok.com, with bit Fabric itself as the example
        "mcp" =>
        [
            new(localizer[nameof(AppStrings.AppHomeForExampleBitFabricItselfInGrokCom)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeAddCustomConnector)]), new FilmField("https://api.bitfabric.co/mcp", localizer[nameof(AppStrings.AppHomeServerUrl)], Typing: true),
                 new FilmButton(localizer[nameof(AppStrings.Add)], Tap: 2.4)], Browser) { Url = "grok.com", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeSignInOnceAndAllowIt)],
                [new FilmTitle("bit Fabric"), new FilmField("emma@yourapp.com", localizer[nameof(AppStrings.AppHomeEmail)]),
                 new FilmText(localizer[nameof(AppStrings.AppHomeGrokWantsToSeeYourSitesHealth)]), new FilmButton(localizer[nameof(AppStrings.AssistantApprove)], Tap: 1.8)], Browser) { Url = "bitfabric.co/sign-in", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeThenAskAboutYourSitesHealth)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeGiveMeAStatusReportOnMySitesHealth)], Mine: true), new FilmBubble(localizer[nameof(AppStrings.AppHomeHereSHowYourSitesAreDoing)]) { At = 1 },
                 new FilmRow(BitIconName.Globe, "yourapp.com", localizer[nameof(AppStrings.AppHomeAllHealthy)], "✓", Mark: New, EndTone: Good) { At = 1.4 },
                 new FilmRow(BitIconName.Globe, "shop.yourapp.com", localizer[nameof(AppStrings.AppHomeEmailIsnTAnswering)], "✕", New, Bad) { At = 1.7 }], Browser) { Url = "grok.com", Person = Emma }
        ],

        // Languages
        "rtl" =>
        [
            new(localizer[nameof(AppStrings.AppHomeInEnglishTheAppReadsFromLeftToRight)],
                [new FilmTitle("Welcome"), new FilmText("Handmade mugs, shipped in a day."), new FilmRow(BitIconName.ShoppingCart, "Blue mug", End: "$24"), new FilmButton("Shop now")]) { Label = "English" },
            new(localizer[nameof(AppStrings.AppHomeInPersianAndArabicTheWholeAppTurns)],
                [new FilmTitle("خوش آمدید"), new FilmText("ماگ‌های دست‌ساز، با ارسال یک‌روزه."), new FilmRow(BitIconName.ShoppingCart, "ماگ آبی", End: "۲۴ دلار"), new FilmButton("همین حالا بخرید")]) { Label = "فارسی", Rtl = true }
        ],
        "time-zone" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEachUserPicksTheTimeZoneTheyWantToSee)],
                [new FilmTitle(localizer[nameof(AppStrings.ThemeOverlayMenu)]), new FilmRow(BitIconName.Clock, localizer[nameof(AppStrings.AppHomeTimeZone)], End: localizer[nameof(AppStrings.AppHomeTehran)], Mark: Picked),
                 new FilmRow(BitIconName.Calendar, localizer[nameof(AppStrings.AppHomeCalendar)], End: localizer[nameof(AppStrings.AppHomeGregorian)])]),
            new(localizer[nameof(AppStrings.AppHomeEveryTimeInTheAppIsShownInIt)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrderArrives), 1042], localizer[nameof(AppStrings.AppHome7November)], localizer[nameof(AppStrings.AppHome1030TehranTime)])]),
            new(localizer[nameof(AppStrings.AppHomeAndInAnotherCalendarIfTheyLike)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrderArrives), 1042], "۱۶ آبان ۱۴۰۵", "10:30"), new FilmNote(localizer[nameof(AppStrings.AppHomePersianCalendar)], Neutral)])
        ],
        "translator" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAddALanguageAndTheAITranslatesThe)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeLanguages)]), English,
                 new FilmRow(BitIconName.LocaleLanguage, "Français", End: localizer[nameof(AppStrings.AppHomeTranslating)], Mark: New, EndTone: Accent) { At = .6 }], Browser) { Url = "yourapp.com/admin/languages" },
            new(localizer[nameof(AppStrings.AppHomeInMoments)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeLanguages)]), English, new FilmRow(BitIconName.LocaleLanguage, "Français", End: "100%", EndTone: Good)], Browser) { Url = "yourapp.com/admin/languages" },
            new(localizer[nameof(AppStrings.AppHomeAndYourAppSpeaksIt)],
                [new FilmTitle("Bienvenue"), new FilmText("Des tasses faites main, livrées en un jour."), new FilmButton("Acheter")]) { Label = "Français" }
        ],
        "found" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEachLanguageHasPagesOfItsOwnWhich)],
                [new FilmField("tasses faites main"), new FilmRow(BitIconName.Globe, "Tasses faites main, livrées en un jour", "yourapp.com/fr", Mark: New) { At = .8 }], Browser) { Url = "google.com" },
            new(localizer[nameof(AppStrings.AppHomeSoPeopleFindYouInTheirOwnLanguage)],
                [new FilmField("ماگ دست‌ساز"), new FilmRow(BitIconName.Globe, "ماگ‌های دست‌ساز، با ارسال یک‌روزه", "yourapp.com/fa", Mark: New) { At = .8 }], Browser) { Url = "google.com", Rtl = true }
        ],

        // The Workbench
        "any-ai" =>
        [
            new(localizer[nameof(AppStrings.AppHomeWhicheverAIYouUse)],
                [new FilmChips(Agents, 0), new FilmBubble(localizer[nameof(AppStrings.AppHomeMakeTheSignUpPageFriendlier)], Mine: true) { At = .6 },
                 new FilmBubble(localizer[nameof(AppStrings.AppHomeHereItIsInTheWorkbenchOnEveryDevice)]) { At = 1.5 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeDesignsYourPagesInTheWorkbenchNotIn)],
                [new FilmChips(Agents, 1), .. SignUpPage], Browser) { Url = "localhost/workbench/sign-up" }
        ],
        "real-components" =>
        [
            new(localizer[nameof(AppStrings.AppHomeInTheWorkbenchPagesAreDrawnWithYour)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeInTheWorkbench)]), DraggedCalendar], Browser) { Url = "localhost/workbench/calendar" },
            new(localizer[nameof(AppStrings.AppHomeAndInYourAppItSTheVerySameCalendarSo)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeInYourApp)]), DraggedCalendar], Browser) { Url = "yourapp.com/calendar" }
        ],
        "states" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEveryStateOfAPageIsOneClickAwayReady)],
                [new FilmChips(States, 0), .. Orders], Browser) { Url = "localhost/workbench" },
            new(localizer[nameof(AppStrings.AppHomeLoading)], [new FilmChips(States, 1), new FilmRow(BitIconName.ShoppingCart, "· · ·", Mark: Off), new FilmRow(BitIconName.ShoppingCart, "· · ·", Mark: Off)], Browser) { Url = "localhost/workbench" },
            new(localizer[nameof(AppStrings.AppHomeEmpty)], [new FilmChips(States, 2), new FilmText(localizer[nameof(AppStrings.AppHomeNoOrdersYet)]), new FilmButton(localizer[nameof(AppStrings.AppHomeAddAProduct)], Outline: true)], Browser) { Url = "localhost/workbench" },
            new(localizer[nameof(AppStrings.AppHomeOrFailedOnPhonesTabletsAndDesktopsIn)],
                [new FilmChips(States, 3), new FilmNote(localizer[nameof(AppStrings.AppHomeCouldnTLoadTheOrders)], Bad), new FilmButton(localizer[nameof(AppStrings.PosRetry)], Outline: true)], Browser) { Url = "localhost/workbench" }
        ],
        // Side by side, store screenshots and flows, each a card of its own
        "side-by-side" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheWorkbenchShowsOnePageOnAPhoneA)],
                [new FilmFrames([new(Phone, "iPhone"), new(Tablet, "iPad"), new(Desktop, "Windows")])], Browser) { Url = "localhost/workbench/orders" },
            new(localizer[nameof(AppStrings.AppHomeInLightDarkAndRightToLeftTooSoA)],
                [new FilmFrames([new(Phone, localizer[nameof(AppStrings.ThemeLight)]) { Dark = false }, new(Phone, localizer[nameof(AppStrings.Dark)]) { Dark = true }, new(Phone, "فارسی") { Dark = false, Rtl = true },
                     new(Desktop, $"{localizer[nameof(AppStrings.Dark)]} · فارسی") { Dark = true, Rtl = true }], ChangeAt: 1.6)], Browser) { Url = "localhost/workbench/orders" }
        ],
        "screenshots" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAskYourAIForTheStoreScreenshots)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeMakeTheStoreScreenshots)], Mine: true) { At = .4 },
                 new FilmRow(BitIconName.CellPhone, "App Store", End: "✓", Mark: New, EndTone: Good) { At = 1 },
                 new FilmRow(BitIconName.CellPhone, "Google Play", End: "✓", Mark: New, EndTone: Good) { At = 1.3 },
                 new FilmRow(BitIconName.WindowsLogo, "Microsoft Store", End: "✓", Mark: New, EndTone: Good) { At = 1.6 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeAndTheWorkbenchMakesThemPixelExact)],
                [new FilmFrames([new(Phone, "App Store") { Caption = localizer[nameof(AppStrings.AppHomeAllYourOrders)] },
                     new(Phone, "Google Play") { Caption = localizer[nameof(AppStrings.AppHomePayInOneTap)], Tone = Good },
                     new(Desktop, "Microsoft Store") { Caption = localizer[nameof(AppStrings.AppHomeWorksOffline)] }])], Browser) { Url = "localhost/workbench/screenshots" }
        ],
        "flows" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheWorkbenchDrawsHowYourPagesLeadTo)], [new FilmFlow(CheckoutFlow)], Browser) { Url = "localhost/workbench/flows/checkout" },
            new(localizer[nameof(AppStrings.AppHomeSoTheWholeTeamSeesTheJourneyAtA)],
                [new FilmFlow(CheckoutFlow), new FilmNote(localizer[nameof(AppStrings.AppHomeSharedWithTheTeam)], Neutral) { At = 2.4 }], Laptop) { Url = "localhost/workbench/flows/checkout" }
        ],

        // Growth
        "review" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAtAHappyMoment)], [new FilmDone(localizer[nameof(AppStrings.AppHomeOrderPlaced)], Sub: localizer[nameof(AppStrings.AppHomeArrivingOnFriday)])]),
            new(localizer[nameof(AppStrings.AppHomeTheAppAsksForARatingInTheStoreSOwn)],
                [new FilmDone(localizer[nameof(AppStrings.AppHomeOrderPlaced)], Sub: localizer[nameof(AppStrings.AppHomeArrivingOnFriday)]),
                 new FilmDialog(localizer[nameof(AppStrings.AppHomeEnjoyingTheApp)], [new FilmText(localizer[nameof(AppStrings.AppHomeTapAStarToRateItOnGooglePlay)]), new FilmTitle("★★★★★"), new FilmButton(localizer[nameof(AppStrings.AppHomeSubmit)], Tap: 2)]) { At = .4 }])
        ],
        "messages" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourAppSendsEmails)],
                [new FilmDone(localizer[nameof(AppStrings.AppHomeOrderPlaced)]), new FilmBanner(localizer[nameof(AppStrings.AppHomeMail)], localizer[nameof(AppStrings.AppHomeYourOrderIsConfirmed), 1044]) { At = .8 }]),
            new(localizer[nameof(AppStrings.AppHomeAndTextMessagesFromYourOwnApp)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders, new FilmBanner(localizer[nameof(AppStrings.AppHomeMessages)], localizer[nameof(AppStrings.AppHomeYourOrderShipsToday), 1044]) { At = .6 }])
        ],
        "push" =>
        [
            new(localizer[nameof(AppStrings.AppHomeNotificationsReachUsersOnTheWeb)],
                [new FilmTitle("10:24"), new FilmBanner(localizer[nameof(AppStrings.AppHomeYourApp)], localizer[nameof(AppStrings.AppHomeYourOrderIsOutForDelivery)]) { At = .5 }]) { Label = localizer[nameof(AppStrings.AppHomeLockScreen)] },
            new(localizer[nameof(AppStrings.AppHomeAndInsideTheOpenAppLiveAsThings)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders, new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1045], localizer[nameof(AppStrings.AppHomeJustPlaced)], "$26", Mark: New) { At = 1 }], Browser) { Url = "yourapp.com/admin" }
        ],
        "links" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEmmaSendsSaraALinkToAMug)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeMessages)]), new FilmBubble(localizer[nameof(AppStrings.AppHomeLookAtThisOne)]), new FilmButton("yourapp.com/p/blue-mug", Tap: 1.4, Outline: true)]) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeHasTheApp)] },
            new(localizer[nameof(AppStrings.AppHomeSaraHasYourAppSoTheLinkOpensRightIn)],
                [new FilmAppBar(localizer[nameof(AppStrings.AppHomeYourApp)]), .. ProductPage, new FilmButton(localizer[nameof(AppStrings.AppHomeAddToCart)])]) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeHasTheApp)] },
            new(localizer[nameof(AppStrings.AppHomeNoahGetsTheSameLinkButDoesnTHaveThe)],
                [new FilmStep(localizer[nameof(AppStrings.AppHomeMessages)]), new FilmBubble(localizer[nameof(AppStrings.AppHomeLookAtThisOne)]), new FilmButton("yourapp.com/p/blue-mug", Tap: 1.4, Outline: true)]) { Person = Noah, Label = localizer[nameof(AppStrings.AppHomeNoApp)] },
            new(localizer[nameof(AppStrings.AppHomeSoItOpensTheSamePageInHisBrowserWith)],
                [.. ProductPage, new FilmButton(localizer[nameof(AppStrings.AppHomeGetTheApp)], Outline: true)]) { Url = "yourapp.com/p/blue-mug", Person = Noah, Label = localizer[nameof(AppStrings.AppHomeNoApp)] }
        ],
        "ads" =>
        [
            new(localizer[nameof(AppStrings.AppHomeUsersCanEarnSomethingByWatchingAnAd)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeGet50Coins)]), new FilmText(localizer[nameof(AppStrings.AppHomeSpendThemOnADiscountOnYourNextOrder)]), new FilmButton(localizer[nameof(AppStrings.AppHomeWatchAnAd)], Tap: 1.3)]),
            new(localizer[nameof(AppStrings.AppHomeTheyWatchItToTheEnd)], [new FilmPicture(Video, localizer[nameof(AppStrings.AppHomeAd015)])]),
            new(localizer[nameof(AppStrings.AppHomeAndGetTheirReward)], [new FilmDone(localizer[nameof(AppStrings.AppHome50Coins)])])
        ],
        "payments" =>
        [
            new(localizer[nameof(AppStrings.AppHomeUsersPayInsideTheApp)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectFilmCheckout)]), .. Cart, new FilmRow(BitIconName.Money, localizer[nameof(AppStrings.AppHomeCardEndingIn), "4242"]), new FilmButton(localizer[nameof(AppStrings.AppHomePay), "$48"], Tap: 1.4)]),
            new(localizer[nameof(AppStrings.AppHomeAndTheMoneyIsYours)], [new FilmDone(localizer[nameof(AppStrings.AppHomePaid)], Sub: "$48")])
        ],

        // The admin panel
        "live-dashboard" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheDashboardShowsWhatIsHappening)],
                [new FilmTitle(localizer[nameof(AppStrings.AssistantToday)]), new FilmStat("41", localizer[nameof(AppStrings.AppHomeOrders)]), new FilmBars([30, 45, 40, 60, 50, 70, 55])], Browser) { Url = "yourapp.com/admin" },
            new(localizer[nameof(AppStrings.AppHomeAndUpdatesByItselfAsAnOrderComesIn)],
                [new FilmTitle(localizer[nameof(AppStrings.AssistantToday)]), new FilmStat("42", localizer[nameof(AppStrings.AppHomeOrders)]) { At = .5 }, new FilmBars([30, 45, 40, 60, 50, 70, 75]),
                 new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1045], localizer[nameof(AppStrings.AppHomeJustPlaced)], "$26", Mark: New) { At = .5 }], Browser) { Url = "yourapp.com/admin" }
        ],
        "concurrency" =>
        [
            new(localizer[nameof(AppStrings.AppHomeEmmaOpensTheBlueMugSPageItS24)], [.. MugPrice("$24")], Laptop) { Url = "yourapp.com/admin/products/12", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeLucasOpensTheSamePageAtTheSameTime)], [.. MugPrice("$24")], Laptop) { Url = "yourapp.com/admin/products/12", Person = Lucas },
            new(localizer[nameof(AppStrings.AppHomeEmmaChangesThePriceTo26AndSavesIt)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeBlueMug)]), new FilmField("$26", localizer[nameof(AppStrings.AppHomePrice)], Typing: true), new FilmButton(localizer[nameof(AppStrings.Save)], Tap: 2), new FilmNote(localizer[nameof(AppStrings.AppHomeSaved)]) { At = 2.4 }],
                Laptop) { Url = "yourapp.com/admin/products/12", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeThenLucasStillOnTheOldPageChangesIt)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeBlueMug)]), new FilmField("$22", localizer[nameof(AppStrings.AppHomePrice)], Typing: true), new FilmButton(localizer[nameof(AppStrings.Save)], Tap: 2),
                 new FilmDialog(localizer[nameof(AppStrings.AppHomeThisChanged)], [new FilmText(localizer[nameof(AppStrings.AppHomeEmmaChangedThePriceTo26WhileYouWere)]),
                     new FilmButton(localizer[nameof(AppStrings.AppHomeSeeTheChange)], Tap: 3.8)]) { At = 2.5 }], Laptop) { Url = "yourapp.com/admin/products/12", Person = Lucas }
        ],

        // Windows
        "portable" =>
        [
            new(localizer[nameof(AppStrings.AppHomeOneFileAndNoInstaller)], [new FilmStep(localizer[nameof(AppStrings.AppHomeDownloads)]), new FilmRow(BitIconName.Download, "YourApp.exe", "48 MB", Mark: New) { At = .3 }], Desktop),
            new(localizer[nameof(AppStrings.AppHomeOpenItAndItRunsOnWindows7AndLater)], [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders], Desktop)
        ],
        "updates" =>
        [
            new(localizer[nameof(AppStrings.AppHomeWhenYouReleaseANewVersion)], [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders, new FilmBanner(localizer[nameof(AppStrings.AppHomeYourApp)], localizer[nameof(AppStrings.AppHomeUpdatingToVersion), "2.4"]) { At = .8 }], Desktop),
            new(localizer[nameof(AppStrings.AppHomeEveryUserHasItWithoutDoingAnything)], [new FilmNote(localizer[nameof(AppStrings.AppHomeVersion), "2.4"]), new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders], Desktop)
        ],
        "kiosk" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAWindowsAppUsuallyHasTheTaskbarAnd)], [.. OrderHere], Desktop),
            new(localizer[nameof(AppStrings.AppHomeInKioskModeYourAppAloneFillsThe)], [.. OrderHere], Kiosk),
            new(localizer[nameof(AppStrings.AppHomeAndNobodyCanLeaveItOrOpenAnything)],
                [.. OrderHere, new FilmNote(localizer[nameof(AppStrings.AppHomeOnlyThisApp)], Neutral) { At = .4 }], Kiosk)
        ],

        // Batteries included
        "components" =>
        [
            new(localizer[nameof(AppStrings.AppHomeADataGridToSearchSortAndPageThrough)], [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), OrdersGrid], Browser) { Url = "yourapp.com/admin/orders" },
            new(localizer[nameof(AppStrings.AppHomeAFullCalendar)], [new FilmCalendar(localizer[nameof(AppStrings.AppHomeNovember)], CalendarEvents)], Browser) { Url = "yourapp.com/admin/calendar" },
            new(localizer[nameof(AppStrings.AppHomeChartsAnd100MoreReadyToUse)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeSales)]), new FilmStat("$12,480", localizer[nameof(AppStrings.AppHomeThisMonth)]), new FilmBars([20, 35, 50, 45, 65, 80, 90])], Browser) { Url = "yourapp.com/admin/reports" }
        ],
        "image-editor" =>
        [
            new(localizer[nameof(AppStrings.AppHomeCropAPhotoInTheApp)], [new FilmTitle(localizer[nameof(AppStrings.AppHomeProductPhoto)]), new FilmPicture(Crop), new FilmTools(EditorTools, 0)]),
            new(localizer[nameof(AppStrings.AppHomeTurnIt)], [new FilmTitle(localizer[nameof(AppStrings.AppHomeProductPhoto)]), new FilmPicture(Rotate), new FilmTools(EditorTools, 1)]),
            new(localizer[nameof(AppStrings.AppHomeResizeItDrawOnItAndMoreWithout)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeProductPhoto)]), new FilmPicture(Photo, "1080 × 1080"), new FilmTools(EditorTools), new FilmNote(localizer[nameof(AppStrings.AppHomeSaved)]) { At = .6 }])
        ],
        "scanner" =>
        [
            new(localizer[nameof(AppStrings.AppHomePointTheCameraAtACode)], [new FilmTitle(localizer[nameof(AppStrings.AppHomeAddStock)]), new FilmPicture(Scan, localizer[nameof(AppStrings.AppHomeHoldTheBarcodeInTheFrame)])]),
            new(localizer[nameof(AppStrings.AppHomeAndTheAppReadsItAtOnce)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeAddStock)]), new FilmRow(BitIconName.QRCode, localizer[nameof(AppStrings.AppHomeBlueMug)], localizer[nameof(AppStrings.AppHomeInStock), 13], "+1", New, Good)])
        ],
        "rebrand" =>
        [
            new(localizer[nameof(AppStrings.AppHomeShowTheAIASiteOrADesignYouLike)],
                [.. BrandPage("Bloom Flowers", localizer[nameof(AppStrings.AppHomeFreshFlowersDeliveredToday)])], Browser) { Url = "bloomflowers.com", Restyled = true },
            new(localizer[nameof(AppStrings.AppHomeAskItToGiveYourAppThatLook)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeMakeMyAppLookLikeBloomflowersCom)], Mine: true) { At = .4 },
                 new FilmBubble(localizer[nameof(AppStrings.AppHomeOnItItsColorsFontsAndCornersOnEvery)]) { At = 1.4 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeAndEveryPageOfYourAppTakesIt)],
                [.. BrandPage(localizer[nameof(AppStrings.AppHomeYourShop)], localizer[nameof(AppStrings.AppHomeHandmadeMugsShippedInADay)])], Browser) { Url = "yourapp.com", Restyled = true }
        ],
        "port" =>
        [
            new(localizer[nameof(AppStrings.AppHomeBringOverAnAppYouAlreadyHave)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeTheOldApp)]), new FilmRow(BitIconName.People, localizer[nameof(AppStrings.NewProjectSampleCustomers)]), new FilmRow(BitIconName.Money, localizer[nameof(AppStrings.AppHomeInvoices)]), new FilmRow(BitIconName.BarChartVerticalFill, localizer[nameof(AppStrings.AppHomeReports)])], Desktop),
            new(localizer[nameof(AppStrings.AppHomeFeatureByFeatureWithItsBusinessRules)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeBringOverOurOldInvoicingApp)], Mine: true),
                 new FilmRow(BitIconName.People, localizer[nameof(AppStrings.NewProjectSampleCustomers)], End: "✓", EndTone: Good) { At = .6 },
                 new FilmRow(BitIconName.Money, localizer[nameof(AppStrings.AppHomeInvoices)], End: localizer[nameof(AppStrings.AppHomeNow)], Mark: New, EndTone: Accent) { At = 1 },
                 new FilmRow(BitIconName.BarChartVerticalFill, localizer[nameof(AppStrings.AppHomeReports)], End: localizer[nameof(AppStrings.CalendarNextButtonTitle)]) { At = 1.2 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeAndItRunsOnEveryPlatform)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeInvoices)]), new FilmRow(BitIconName.Money, "INV-1042", "Sara", "$240"), new FilmRow(BitIconName.Money, "INV-1043", "Noah", "$80")])
        ],
        "deploy" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAskYourAIToPutTheAppOnline)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomePutMyAppOnline)], Mine: true), new FilmRow(BitIconName.Server, localizer[nameof(AppStrings.AppHomeYourServer)], localizer[nameof(AppStrings.AppHomeSettingUp)], Mark: New) { At = 1 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeItGoesToYourOwnServerAndDomain)],
                [new FilmRow(BitIconName.Server, localizer[nameof(AppStrings.AppHomeYourServer)], End: "✓", EndTone: Good), new FilmRow(BitIconName.Globe, "yourapp.com", End: localizer[nameof(AppStrings.NewProjectSampleLive)], EndTone: Good) { At = .4 },
                 new FilmRow(BitIconName.Lock, "HTTPS", End: "✓", EndTone: Good) { At = .8 }, new FilmRow(BitIconName.Shield, localizer[nameof(AppStrings.AppHomeFirewall)], End: "✓", EndTone: Good) { At = 1.2 }], Editor)
        ],
        "troubleshoot" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSomethingSWrongOnYourLiveAppTellYour)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeSomeUsersCanTCheckOutSinceThis)], Mine: true),
                 new FilmRow(BitIconName.Bug, localizer[nameof(AppStrings.AppHomeErrors)], localizer[nameof(AppStrings.AppHomeCheckoutFailedUsers), 37], localizer[nameof(AppStrings.AppHomeRead)], New) { At = .8 },
                 new FilmRow(BitIconName.Search, localizer[nameof(AppStrings.AppHomeLogsAndBackgroundJobs)], End: localizer[nameof(AppStrings.AppHomeRead)]) { At = 1.3 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeFindsTheCauseAndFixesItWithATestThat)],
                [new FilmRow(BitIconName.Search, localizer[nameof(AppStrings.AppHomeTheCause)], localizer[nameof(AppStrings.AppHomeDiscountCodesWithASpaceAtTheEnd)], "✓", EndTone: Good) { At = .3 },
                 new FilmRow(BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeATestThatFailsWithoutTheFix)], End: "✓", EndTone: Good) { At = .8 },
                 new FilmRow(BitIconName.Code, localizer[nameof(AppStrings.AppHomeTheFix)], End: "✓", EndTone: Good) { At = 1.3 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeSoCheckoutWorksAgainAndStaysThatWay)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourOrder)]), .. Cart, new FilmButton(localizer[nameof(AppStrings.AppHomePlaceOrder)], Tap: .8), new FilmNote(localizer[nameof(AppStrings.AppHomeOrderPlaced2)]) { At = 1.5 }]) { Person = Sara }
        ],
        // dev-mcp: the visitor's own AI reads the live app, read-only, when a third party's service breaks
        "dev-mcp" =>
        [
            new(localizer[nameof(AppStrings.AppHomeAThirdPartyServiceStoppedWorkingThis)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeShippingRatesFromShipFastHavenTWorked)], Mine: true),
                 new FilmRow(BitIconName.PlugConnected, "dev-mcp", localizer[nameof(AppStrings.AppHomeProductionReadOnly)], "✓", EndTone: Good) { At = .8 },
                 new FilmRow(BitIconName.Search, localizer[nameof(AppStrings.AppHomeLogsAndBackgroundJobs)], localizer[nameof(AppStrings.AppHomeShipFastAnswersWithAnErrorSince0800)], localizer[nameof(AppStrings.AppHomeRead)], New) { At = 1.3 },
                 new FilmRow(BitIconName.Database, localizer[nameof(AppStrings.AppHomeDatabase)], localizer[nameof(AppStrings.AppHomeOrdersWaitingForARate), 23], localizer[nameof(AppStrings.AppHomeRead)], New) { At = 1.8 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeSoYourOwnAINotBitFabricFindsThe)],
                [new FilmRow(BitIconName.Search, localizer[nameof(AppStrings.AppHomeTheCause)], localizer[nameof(AppStrings.AppHomeShipFastMovedToANewVersionOfIts)], "✓", EndTone: Good) { At = .3 },
                 new FilmRow(BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeATestThatFailsWithoutTheFix)], End: "✓", EndTone: Good) { At = .8 },
                 new FilmRow(BitIconName.Code, localizer[nameof(AppStrings.AppHomeTheFix)], End: "✓", EndTone: Good) { At = 1.3 },
                 new FilmNote(localizer[nameof(AppStrings.AppHomeNothingChangedOnTheLiveApp)], Neutral) { At = 1.8 }], Editor)
        ],
        "ai-dlc" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheAIWorksLikeATeamOfExperienced)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeAddDiscountCodes)], Mine: true), new FilmRow(BitIconName.Chat, localizer[nameof(AppStrings.AppHomeRequirements)], End: "✓", EndTone: Good) { At = .6 },
                 new FilmRow(BitIconName.Edit, localizer[nameof(AppStrings.AppHomePlan)], End: "✓", EndTone: Good) { At = 1 },
                 new FilmRow(BitIconName.Color, localizer[nameof(AppStrings.AppHomeDesign)], End: localizer[nameof(AppStrings.AppHomeWaitingForYou)], Mark: New, EndTone: Accent) { At = 1.4 }], Editor),
            new(localizer[nameof(AppStrings.AppHomeThenTheCodeWithAutomatedUITests)],
                [new FilmRow(BitIconName.Code, localizer[nameof(AppStrings.AppHomeCode)], End: "✓", EndTone: Good), new FilmRow(BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeUITests)], End: localizer[nameof(AppStrings.AppHomePassed), 48], Mark: New, EndTone: Good) { At = .6 },
                 new FilmRow(BitIconName.Rocket, localizer[nameof(AppStrings.AppHomeReadyToShip)], End: "✓", EndTone: Good) { At = 1.1 }], Editor)
        ],

        "up-to-date" =>
        [
            new(localizer[nameof(AppStrings.AppHomeOurTeamMakes500ImprovementsAMonth)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeWhatSNew)]), new FilmStat("500+", localizer[nameof(AppStrings.AppHomeImprovementsThisMonth)]),
                 new FilmRow(BitIconName.Shield, localizer[nameof(AppStrings.AppHomeSecurityFixes)]) { At = .6 },
                 new FilmRow(BitIconName.SpeedHigh, localizer[nameof(AppStrings.AppHomeFasterPages)]) { At = .9 },
                 new FilmRow(BitIconName.Puzzle, localizer[nameof(AppStrings.AppHomeNewComponents)]) { At = 1.2 }], Browser) { Url = "bitfabric.co/yourapp" },
            new(localizer[nameof(AppStrings.AppHomeYourAIBringsThemIntoYourProjectKeeps)],
                [new FilmBubble(localizer[nameof(AppStrings.AppHomeUpdateMyProject)], Mine: true),
                 new FilmRow(BitIconName.PostUpdate, localizer[nameof(AppStrings.AppHomeThisMonthSImprovements)], End: "✓", EndTone: Good) { At = .6 },
                 new FilmRow(BitIconName.People, localizer[nameof(AppStrings.AppHomeYourTeamSChanges)], End: localizer[nameof(AppStrings.AppHomeKept)], EndTone: Good) { At = 1 },
                 new FilmRow(BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeBuildAndTests)], End: "✓", EndTone: Good) { At = 1.4 }], Editor)
        ],

        // Fast, and watched over
        "caching" =>
        [
            new(localizer[nameof(AppStrings.AppHomeTheFirstVisitLoadsThePageFromYour)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeHandmadeMugsMadeToOrder)]), new FilmStat(localizer[nameof(AppStrings.AppHomeS), "1.2"], localizer[nameof(AppStrings.AppHomeTheFirstVisit)])]) { Url = "yourapp.com" },
            new(localizer[nameof(AppStrings.AppHomeAfterThatItComesFromCloseToTheUser)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeHandmadeMugsMadeToOrder)]), new FilmStat(localizer[nameof(AppStrings.AppHomeS), "0.1"], localizer[nameof(AppStrings.AppHomeEveryVisitAfterIt)]), new FilmNote(localizer[nameof(AppStrings.AppHomeFromTheCache)], Neutral) { At = .5 }]) { Url = "yourapp.com" }
        ],
        "telemetry" =>
        [
            new(localizer[nameof(AppStrings.AppHomeWhenSomethingBreaksForAUser)], [new FilmDone(localizer[nameof(AppStrings.AssistantSomethingWentWrong)], Bad, localizer[nameof(AppStrings.AppHomeWeVeBeenToldAndWeReOnIt)])]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeYouHearAboutItFirstWithWhatItTakesTo)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeErrors)]), new FilmRow(BitIconName.Bug, localizer[nameof(AppStrings.AppHomeCheckoutFailed)], localizer[nameof(AppStrings.AppHomeAndroid15Version), "2.4"], localizer[nameof(AppStrings.AppHomeUsers2), 3], New, Bad) { At = .4 }], Browser)
        ],
        // A film like the other cards', in place of its few lines and drawn the way they are
        "zero-downtime" =>
        [
            new(localizer[nameof(AppStrings.AppHomeSaraIsInTheMiddleOfAChatOnVersion), "2.3"],
                [WhereSMyOrder, new FilmBubble(localizer[nameof(AppStrings.AppHomeOnItsWayItArrivesOnFridayMorning)]) { At = .9 }]) { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeVersion), "2.3"] },
            new(localizer[nameof(AppStrings.AppHomeWhileVersionStartsNextToItTheDatabase), "2.4"],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourServer)]),
                 new FilmRow(BitIconName.Server, localizer[nameof(AppStrings.AppHomeVersion), "2.3"], End: localizer[nameof(AppStrings.AppHomeServing)], EndTone: Good),
                 new FilmRow(BitIconName.Database, localizer[nameof(AppStrings.AppHomeDatabase)], End: localizer[nameof(AppStrings.AppHomeUpdated)], Mark: New, EndTone: Good) { At = .6 },
                 new FilmRow(BitIconName.Server, localizer[nameof(AppStrings.AppHomeVersion), "2.4"], End: localizer[nameof(AppStrings.AppHomeReady)], Mark: New, EndTone: Good) { At = 1.3 }], Browser),
            new(localizer[nameof(AppStrings.AppHomeAndHerChatReconnectsByItselfWithin)],
                [WhereSMyOrder, new FilmBubble(localizer[nameof(AppStrings.AppHomeOnItsWayItArrivesOnFridayMorning)]),
                 new FilmNote(localizer[nameof(AppStrings.AppHomeUpdatedToVersion), "2.4"], Neutral) { At = .4 }, new FilmBubble(localizer[nameof(AppStrings.AppHomeThanks)], Mine: true) { At = 1.1 }])
                { Person = Sara, Label = localizer[nameof(AppStrings.AppHomeVersion), "2.4"] }
        ],

        // We watch over it once it's live: the visitor's project on bitfabric.co, and what their customers see
        "health" =>
        [
            new(localizer[nameof(AppStrings.AppHomeBitFabricChecksEveryPartOfYourApp)],
                [new FilmTitle("yourapp.com"), new FilmStep(Locations), .. HealthOf(emailDown: false),
                 new FilmNote(localizer[nameof(AppStrings.AppHomeOnlyItsHealthNeverYourData)], Neutral) { At = 1.4 }], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeSeesTheMomentOneOfThemStopsAnswering)],
                [new FilmTitle("yourapp.com"), new FilmStep(Locations), .. HealthOf(emailDown: true)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndTellsYouWhatBrokeForYourUsersAnd)],
                [new FilmTitle("yourapp.com"), new FilmDone(localizer[nameof(AppStrings.AppHomeEmailIsnTAnswering)], Bad, localizer[nameof(AppStrings.AppHomeSince1002From3Regions)]),
                 new FilmBanner("bit Fabric", localizer[nameof(AppStrings.AppHomeYourappComSignInWithAnEmailedCodeIs)]) { At = .6 }]) { Person = Emma }
        ],
        // The domain the app sends from, in its health check: DMARC goes missing, and the owner hears it before codes land in spam
        "email-delivery" =>
        [
            new(localizer[nameof(AppStrings.AppHomeBitFabricChecksYourEmailSSPFAndDMARC)],
                [new FilmTitle("yourapp.com"), new FilmStep("noreply@yourapp.com"), .. EmailDeliveryOf(dmarcMissing: false)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeSeesTheMomentOneGoesMissing)],
                [new FilmTitle("yourapp.com"), new FilmStep("noreply@yourapp.com"), .. EmailDeliveryOf(dmarcMissing: true)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndTellsYouBeforeYourCodesLandInSpam)],
                [new FilmTitle("yourapp.com"), new FilmDone(localizer[nameof(AppStrings.AppHomeYourEmailsMayLandInSpam)], Bad, localizer[nameof(AppStrings.AppHomeDMARCIsMissingOnYourappCom)]),
                 new FilmBanner("bit Fabric", localizer[nameof(AppStrings.AppHomeYourappComSignInCodesMayLandInSpam)]) { At = .6 }]) { Person = Emma }
        ],
        // The app's other sites in its health check: the blog goes down, and the report says how
        "websites" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourLandingPageBlogAndDocsWatchedWith)],
                [new FilmTitle("yourapp.com"), new FilmStep(Locations), .. WebsitesOf(blogDown: false)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeYourBlogStopsAnswering)],
                [new FilmTitle("yourapp.com"), new FilmStep(Locations), .. WebsitesOf(blogDown: true)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndYouHearWhatWentWrong)],
                [new FilmTitle("yourapp.com"), new FilmDone(localizer[nameof(AppStrings.AppHomeYourBlogIsnTAnswering)], Bad, localizer[nameof(AppStrings.AppHomeSince1002From3Regions)]),
                 new FilmBanner("bit Fabric", localizer[nameof(AppStrings.AppHomeBlogYourappComAnswers503ServiceUnavailable)]) { At = .6 }]) { Person = Emma }
        ],
        "security-scan" => ScanFilm(localizer[nameof(AppStrings.AppHomeSecurity)], 45, 100,
            localizer[nameof(AppStrings.AppHomeBitFabricLooksAtYourLiveAppTheWay)],
            [(BitIconName.PasswordField, localizer[nameof(AppStrings.AppHomeTheDemoAdminSPasswordStillWorks)]), (BitIconName.Code, localizer[nameof(AppStrings.AppHomeYourAPISDocsAreOpenToEveryone)]), (BitIconName.ShieldAlert, localizer[nameof(AppStrings.AppHomeNoContentSecurityPolicy)])],
            localizer[nameof(AppStrings.AppHomeFixTheSecurityFindings)]),
        "performance-scan" => ScanFilm(localizer[nameof(AppStrings.AppHomePerformance)], 64, 98,
            localizer[nameof(AppStrings.AppHomeBitFabricTimesYourLiveAppSPagesAfter)],
            [(BitIconName.Photo2, localizer[nameof(AppStrings.AppHomeImagesAreBiggerThanTheyNeedToBe)]), (BitIconName.SpeedHigh, localizer[nameof(AppStrings.AppHomePagesArenTCached)]), (BitIconName.Code, localizer[nameof(AppStrings.AppHomeAScriptHoldsUpTheFirstPage)])],
            localizer[nameof(AppStrings.AppHomeFixThePerformanceFindings)]),
        "seo-scan" => ScanFilm(localizer[nameof(AppStrings.AppHomeSEO)], 82, 100,
            localizer[nameof(AppStrings.AppHomeBitFabricReadsYourLiveAppTheWay)],
            [(BitIconName.Globe, localizer[nameof(AppStrings.AppHomeGoogleSees3PagesEmpty)]), (BitIconName.Copy, localizer[nameof(AppStrings.AppHomeTwoPagesShareOneTitle)]), (BitIconName.TextDocument, localizer[nameof(AppStrings.AppHomeProductPagesHaveNoDescription)])],
            localizer[nameof(AppStrings.AppHomeFixTheSEOFindings)]),
        "accessibility-scan" => ScanFilm(localizer[nameof(AppStrings.Accessibility)], 76, 100,
            localizer[nameof(AppStrings.AppHomeBitFabricChecksThatEveryoneCanUse)],
            [(BitIconName.Contrast, localizer[nameof(AppStrings.AppHomeGreyPricesAreHardToRead)]), (BitIconName.ButtonControl, localizer[nameof(AppStrings.AppHomeAButtonHasNoNameForScreenReaders)]), (BitIconName.Photo2, localizer[nameof(AppStrings.AppHomeTwoImagesHaveNoDescription)])],
            localizer[nameof(AppStrings.AppHomeFixTheAccessibilityFindings)]),
        // The status notice: the app tells its users about an incident even while its own server is down
        "status-notice" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourServerIsCompletelyDownYetYourApp)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders,
                 new FilmNote(localizer[nameof(AppStrings.AppHomePlacingOrdersIsnTWorkingRightNow)], Bad) { At = .6 }]) { Person = Sara },
            new(localizer[nameof(AppStrings.AppHomeAndEachUpdateYouPostOnYourStatusPage)],
                [new FilmTitle(localizer[nameof(AppStrings.NewProjectSampleOrders)]), .. Orders,
                 new FilmNote(localizer[nameof(AppStrings.AppHomeUpdateOrdersShouldGoThroughAgain)], Neutral) { At = .6 }], Laptop) { Url = "yourapp.com", Person = Sara }
        ],
        "status-page" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourCustomersGetAStatusPageOnYourOwn)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourAppSStatus)]), new FilmNote(localizer[nameof(AppStrings.AppHomeEverythingIsWorking)]), .. StatusOf(emailDown: false)], Browser) { Url = "status.yourapp.com", Person = Noah },
            new(localizer[nameof(AppStrings.AppHomeThatSaysWhatIsBrokenInTheirWordsThe)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourAppSStatus)]), new FilmNote(localizer[nameof(AppStrings.AppHomeSignInWithEmailIsnTWorkingSMSAnd)], Bad), .. StatusOf(emailDown: true)], Browser) { Url = "status.yourapp.com", Person = Noah },
            new(localizer[nameof(AppStrings.AppHomeAndOnceItSBackHowReliableYouVeBeen)],
                [new FilmTitle(localizer[nameof(AppStrings.AppHomeYourAppSStatus)]), new FilmNote(localizer[nameof(AppStrings.AppHomeEverythingIsWorking)]),
                 new FilmStat("99.96%", localizer[nameof(AppStrings.AppHomeWorkingTheLast90Days)]) { At = .4 },
                 new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeSignInWithEmail)], localizer[nameof(AppStrings.AppHomeFixedAt1041After39Minutes)], "✓", EndTone: Good) { At = .9 }], Browser) { Url = "status.yourapp.com", Person = Noah }
        ],

        // Alerts where the team already talks: the app's email stops, Slack hears it, and Teams hears that it's over
        "alerts" =>
        [
            new(localizer[nameof(AppStrings.AppHomeYourAppSEmailStopsAnswering)],
                [new FilmTitle("yourapp.com"), new FilmStep(Locations), .. HealthOf(emailDown: true)], Browser) { Url = "bitfabric.co/yourapp", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeYourTeamHearsItInSlack)],
                [new FilmAppBar("# yourapp-alerts", Emma),
                 new FilmRow(BitIconName.Heart, "bit Fabric", localizer[nameof(AppStrings.AppHomeYourappComSignInWithAnEmailedCodeIs)]) { At = .3 },
                 new FilmButton(localizer[nameof(AppStrings.AppHomeIMOnIt)], Tap: 1.3, Outline: true) { At = .6 },
                 new FilmNote(localizer[nameof(AppStrings.AppHomeEmmaIsOnIt)]) { At = 1.6 }]) { Label = "Slack", Person = Emma },
            new(localizer[nameof(AppStrings.AppHomeAndInTeamsWhenItSOver)],
                [new FilmAppBar("Ops", Lucas),
                 new FilmRow(BitIconName.Heart, "bit Fabric", localizer[nameof(AppStrings.AppHomeEmailWorksAgainFixedAt1041After39)], End: "✓", EndTone: Good) { At = .3 },
                 new FilmNote(localizer[nameof(AppStrings.AppHomeAPromptForYourAIIsReady)], Neutral) { At = .9 },
                 new FilmBubble(localizer[nameof(AppStrings.AppHomeWhyWasEmailDownFrom1002To1041)], Mine: true) { At = 1.3 }], Laptop) { Label = "Microsoft Teams", Person = Lucas }
        ],

        // Agents and computers
        "agents" =>
        [
            new(localizer[nameof(AppStrings.AppHomeUseGitHubCopilotClaudeOrGemini)], [new FilmChips(Agents, 0), .. GiftCards], Editor),
            new(localizer[nameof(AppStrings.AppHomeOrAModelRunningOnYourOwnComputer)], [new FilmChips(Agents, Agents.Length - 1), .. GiftCards], Editor)
        ],

        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "This feature has no film.")
    };

    // The pieces several films share

    /// <summary>Where bit Fabric checks an app from by default: Germany, plus Eastern North America and Oceania.</summary>
    private string Locations => localizer[nameof(AppStrings.AppHomeGermanyNorthAmericaOceania)];

    /// <summary>A project's parts as bit Fabric's monitoring sees them, with email down or not.</summary>
    private AppHomeFilmPart[] HealthOf(bool emailDown) =>
    [
        new FilmRow(BitIconName.Database, localizer[nameof(AppStrings.AppHomeDatabase)], localizer[nameof(AppStrings.AppHomeMs), 12], "✓", EndTone: Good),
        emailDown
            ? new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeEmail)], localizer[nameof(AppStrings.AppHomeNotAnsweringSince1002)], "✕", New, Bad) { At = .5 }
            : new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeEmail)], localizer[nameof(AppStrings.AppHomeMs), 40], "✓", EndTone: Good),
        new FilmRow(BitIconName.Message, "SMS", localizer[nameof(AppStrings.AppHomeMs), 85], "✓", EndTone: Good)
    ];

    /// <summary>The records of the domain the app sends from, as its email delivery check reads them, with DMARC missing or not.</summary>
    private AppHomeFilmPart[] EmailDeliveryOf(bool dmarcMissing) =>
    [
        new FilmRow(BitIconName.Mail, "SPF", localizer[nameof(AppStrings.AppHomeWhoMaySendEmailAsYourappCom)], "✓", EndTone: Good),
        dmarcMissing
            ? new FilmRow(BitIconName.Shield, "DMARC", localizer[nameof(AppStrings.AppHomeMissingFromYourappComSDNS)], "✕", New, Bad) { At = .5 }
            : new FilmRow(BitIconName.Shield, "DMARC", localizer[nameof(AppStrings.AppHomeWhatInboxesDoWithAFake)], "✓", EndTone: Good)
    ];

    /// <summary>The app's other sites, as its health check lists them: the landing page, a WordPress blog and the docs.</summary>
    private AppHomeFilmPart[] WebsitesOf(bool blogDown) =>
    [
        new FilmRow(BitIconName.Website, "www.yourapp.com", localizer[nameof(AppStrings.AppHomeLandingPage)], "✓", EndTone: Good),
        blogDown
            ? new FilmRow(BitIconName.Blog, "blog.yourapp.com", "503 Service Unavailable", "✕", New, Bad) { At = .5 }
            : new FilmRow(BitIconName.Blog, "blog.yourapp.com", "WordPress", "✓", EndTone: Good),
        new FilmRow(BitIconName.Documentation, "docs.yourapp.com", localizer[nameof(AppStrings.AppHomeDocs)], "✓", EndTone: Good)
    ];

    /// <summary>A status page's parts, in the words of the app's own users.</summary>
    private AppHomeFilmPart[] StatusOf(bool emailDown) =>
    [
        emailDown
            ? new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeSignInWithEmail)], localizer[nameof(AppStrings.AppHomeSince1003)], localizer[nameof(AppStrings.AppHomePartialOutage)], New, Bad) { At = .4 }
            : new FilmRow(BitIconName.Mail, localizer[nameof(AppStrings.AppHomeSignInWithEmail)], End: localizer[nameof(AppStrings.AppHomeWorking)], EndTone: Good),
        new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.NewProjectSampleOrders)], End: localizer[nameof(AppStrings.AppHomeWorking)], EndTone: Good),
        new FilmRow(BitIconName.Ringer, localizer[nameof(AppStrings.ThemeToggle)], End: localizer[nameof(AppStrings.AppHomeWorking)], EndTone: Good)
    ];

    /// <summary>
    /// A scanner's film: the scan of the live app with its score and what to fix first, then the visitor's AI fixing it all
    /// from the prompt the report prepares, and the next scan's score.
    /// </summary>
    private AppHomeFilmScene[] ScanFilm(string score, int before, int after, string scans, (string Icon, string Text)[] findings, string ask) =>
    [
        new(scans,
            [new FilmScore(before, score), .. findings.Select((finding, i) => new FilmRow(finding.Icon, finding.Text, End: i == 0 ? localizer[nameof(AppStrings.AppHomeUrgent)] : localizer[nameof(AppStrings.AppHomeImportant)], EndTone: i == 0 ? Bad : Neutral) { At = .5 + i * .3 })],
            Browser) { Url = "bitfabric.co/yourapp" },
        new(localizer[nameof(AppStrings.AppHomeEachFindingComesWithAFixYourAI)],
            [new FilmBubble(ask, Mine: true), .. findings.Select((finding, i) => new FilmRow(finding.Icon, finding.Text, End: "✓", EndTone: Good) { At = .6 + i * .4 }),
             new FilmRow(BitIconName.TestBeaker, localizer[nameof(AppStrings.AppHomeBuildAndTests)], End: "✓", EndTone: Good) { At = .6 + findings.Length * .4 }], Editor),
        new(localizer[nameof(AppStrings.AppHomeAndTheNextScanShowsIt)],
            [new FilmScore(after, score), .. findings.Select((finding, i) => new FilmRow(finding.Icon, finding.Text, End: localizer[nameof(AppStrings.AppHomeFixed)], EndTone: Good) { At = .5 + i * .2 })],
            Browser) { Url = "bitfabric.co/yourapp" }
    ];

    private AppHomeFilmPart[] Orders =>
    [
        new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1042], localizer[nameof(AppStrings.AppHomeEmma2Mugs)], "$48"),
        new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeOrder2), 1043], localizer[nameof(AppStrings.AppHomeNoahATeapot)], "$35")
    ];

    private FilmGrid OrdersGrid => new(
        [localizer[nameof(AppStrings.AppHomeOrder)], localizer[nameof(AppStrings.AppHomeCustomer)], localizer[nameof(AppStrings.AppHomeTotal)], localizer[nameof(AppStrings.AppHomeStatus)]],
        [
            ["1044", "Sara", "$26", localizer[nameof(AppStrings.AppHomeNew)]],
            ["1043", "Noah", "$35", localizer[nameof(AppStrings.AppHomePaid)]],
            ["1042", "Emma", "$48", localizer[nameof(AppStrings.AppHomeShipped)]]
        ],
        localizer[nameof(AppStrings.AppHomeOf), 1, 3, 128],
        Sorted: 0);

    private AppHomeFilmPart[] Cart => [new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.AppHomeBlueMug)], "× 2", "$48")];

    private AppHomeFilmPart[] ShopHome =>
    [
        new FilmTitle(localizer[nameof(AppStrings.AppHomeHandmadeMugsMadeToOrder)]),
        new FilmText(localizer[nameof(AppStrings.AppHomeMadeByHandInSmallBatchesAndShippedIn)]),
        new FilmButton(localizer[nameof(AppStrings.AppHomeShopNow)])
    ];

    private AppHomeFilmPart[] ProductPage =>
    [
        new FilmPicture(Photo),
        new FilmTitle(localizer[nameof(AppStrings.AppHomeBlueMug)]),
        new FilmText(localizer[nameof(AppStrings.AppHomeHandmade350Ml), "$24"])
    ];

    private AppHomeFilmPart[] UsersList(bool lucasGone) =>
    [
        new FilmTitle(localizer[nameof(AppStrings.AppHomeUsers)]),
        new FilmRow(BitIconName.Contact, "Emma", localizer[nameof(AppStrings.AppHomeAdmin)], localizer[nameof(AppStrings.AppHomeYou)]) { Person = Emma },
        lucasGone
            ? new FilmRow(BitIconName.Contact, "Lucas", localizer[nameof(AppStrings.CalendarResourceColumnHeader)], localizer[nameof(AppStrings.AppHomeDeleted)], Used, Bad) { Person = Lucas }
            : new FilmRow(BitIconName.Contact, "Lucas", localizer[nameof(AppStrings.CalendarResourceColumnHeader)], localizer[nameof(AppStrings.AssistantDelete)], EndTone: Bad) { Person = Lucas, Tap = 1.4 },
        new FilmRow(BitIconName.Contact, "Noah", localizer[nameof(AppStrings.CalendarResourceColumnHeader)], localizer[nameof(AppStrings.AssistantDelete)], EndTone: Bad) { Person = Noah }
    ];

    private AppHomeFilmPart[] SaraTasks =>
    [
        new FilmRow(BitIconName.Task, localizer[nameof(AppStrings.AppHomeShipTheSpringMenu)], End: localizer[nameof(AppStrings.AssistantToday)]),
        new FilmRow(BitIconName.Task, localizer[nameof(AppStrings.AppHomeCallTheRoaster)], End: localizer[nameof(AppStrings.AppHomeFri)])
    ];

    private AppHomeFilmPart[] MemberMenu => [new FilmRow(BitIconName.Home, localizer[nameof(AppStrings.AppHomeHome)]), new FilmRow(BitIconName.ShoppingCart, localizer[nameof(AppStrings.NewProjectSampleOrders)])];

    private FilmBubble AskForMugs => new(localizer[nameof(AppStrings.AppHomeOrderMeTwoBlueMugsOnYourappCom)], Mine: true);

    private FilmBubble HowToInstall => new(localizer[nameof(AppStrings.AppHomeHowDoIInstallTheApp)], Mine: true);

    private FilmBubble ChargedTwice => new(localizer[nameof(AppStrings.AppHomeIWasChargedTwiceForMyOrder)], Mine: true);

    private FilmBubble ChangeOrder => new(localizer[nameof(AppStrings.AppHomeChangeTheMugInMyLastOrderToTheBlue)], Mine: true);

    private FilmBubble WhereSMyOrder => new(localizer[nameof(AppStrings.AppHomeWhereSMyOrder)], Mine: true);

    private FilmBubble ClearAppData => new(localizer[nameof(AppStrings.AppHomeClearTheAppSDataOnThisDevice)], Mine: true);

    /// <summary>The chat's confirm card for clearing the app's files, with its own texts, and its button tapped when <paramref name="tap"/> is given.</summary>
    private AppHomeFilmPart[] ClearAppDataCard(double at, double? tap) =>
    [
        new FilmRow(BitIconName.Warning, localizer[nameof(AppStrings.AiChatApprovalClearAppFilesTitle)],
            $"{localizer[nameof(AppStrings.AiChatApprovalClearAppFilesSignOut)]} · {localizer[nameof(AppStrings.AiChatApprovalClearAppFilesRestart)]}", Mark: at > 0 ? New : None) { At = at },
        new FilmButton(localizer[nameof(AppStrings.AiChatApprovalClearAppFilesApprove)], Tap: tap) { At = at },
        new FilmButton(localizer[nameof(AppStrings.AiChatApprovalClearAppFilesDecline)], Outline: true) { At = at }
    ];

    private string[] CheckoutFlow => [localizer[nameof(AppStrings.AppHomeProducts)], localizer[nameof(AppStrings.AppHomeCart)], localizer[nameof(AppStrings.AppHomeSignIn)], localizer[nameof(AppStrings.NewProjectFilmCheckout)]];

    private FilmRow English => new(BitIconName.LocaleLanguage, "English", End: "100%", EndTone: Good);

    private string[] Agents => ["Copilot", "Codex", "Claude", "Gemini", "Antigravity", "Cursor", localizer[nameof(AppStrings.AppHomeALocalModel)]];

    private string[] States => [localizer[nameof(AppStrings.AppHomeReady)], localizer[nameof(AppStrings.DataLoading)], localizer[nameof(AppStrings.DataEmpty)], localizer[nameof(AppStrings.AppHomeFailed)]];

    private static string[] EditorTools => [BitIconName.Crop, BitIconName.Rotate, BitIconName.Brush];

    private AppHomeFilmPart[] SignUpPage =>
    [
        new FilmTitle(localizer[nameof(AppStrings.AppHomeCreateYourAccount)]),
        new FilmField("sara@example.com", localizer[nameof(AppStrings.AppHomeEmail)]),
        new FilmButton(localizer[nameof(AppStrings.AppHomeCreateMyAccount)])
    ];

    private FilmEvent[] CalendarEvents =>
    [
        new(6, localizer[nameof(AppStrings.RoleDemo)]),
        new(9, localizer[nameof(AppStrings.AppHomeCall)], Neutral),
        new(14, localizer[nameof(AppStrings.AppHomeLaunch)], Good),
        new(17, localizer[nameof(AppStrings.AppHomeLunch)], Neutral),
        new(22, localizer[nameof(AppStrings.ReviewTab)]),
        new(27, localizer[nameof(AppStrings.AppHomeTrip)], Neutral)
    ];

    /// <summary>The calendar with its launch dragged two days later, the same in the Workbench and in the app.</summary>
    private FilmCalendar DraggedCalendar => new(localizer[nameof(AppStrings.AppHomeNovember)], CalendarEvents, Moving: 2, MovesTo: 16, MoveAt: 1.2);

    private AppHomeFilmPart[] MugPrice(string price) => [new FilmTitle(localizer[nameof(AppStrings.AppHomeBlueMug)]), new FilmField(price, localizer[nameof(AppStrings.AppHomePrice)])];

    private AppHomeFilmPart[] OrderHere =>
    [
        new FilmTitle(localizer[nameof(AppStrings.AppHomeWelcome)]),
        new FilmText(localizer[nameof(AppStrings.AppHomeOrderHereAndPickItUpAtTheCounter)]),
        new FilmButton(localizer[nameof(AppStrings.AppHomeStartYourOrder)])
    ];

    private AppHomeFilmPart[] BrandPage(string name, string text) =>
    [
        new FilmTitle(name),
        new FilmText(text),
        new FilmButton(localizer[nameof(AppStrings.AppHomeShopNow)])
    ];

    private AppHomeFilmPart[] GiftCards =>
    [
        new FilmBubble(localizer[nameof(AppStrings.AppHomeAddAPageForGiftCards)], Mine: true) { At = .5 },
        new FilmBubble(localizer[nameof(AppStrings.AppHomeOnItHereSThePlanFirst)]) { At = 1.3 }
    ];
}
