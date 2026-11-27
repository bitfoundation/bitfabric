using Bit.Fabric.Workbench.Components.Pages.AppPages.Settings.Data;

[assembly: System.Reflection.Metadata.MetadataUpdateHandler(typeof(Bit.Fabric.Workbench.Infrastructure.Services.WorkbenchPageCatalog))]

namespace Bit.Fabric.Workbench.Infrastructure.Services;

/// <summary>
/// The pages the Workbench shows, with their texts, icons, categories, design options and switches. A new page is added here,
/// in <see cref="Build"/>, and shows up while the Workbench runs: hot reload applies an edit to a method, not to an
/// initializer, which would need a restart, and <see cref="ClearCache"/> has the list built again after it.
/// </summary>
public static class WorkbenchPageCatalog
{
    private static IReadOnlyList<WorkbenchPageInfo>? pages;

    public static IReadOnlyList<WorkbenchPageInfo> Pages => pages ??= Build();

    /// <summary>Called by hot reload after it applies an edit (<see cref="System.Reflection.Metadata.MetadataUpdateHandlerAttribute"/>).</summary>
    public static void ClearCache(Type[]? updatedTypes) => pages = null;

    private static IReadOnlyList<WorkbenchPageInfo> Build() =>
    [
        new("theme",
            nameof(WorkbenchStrings.ThemePageTitle),
            nameof(WorkbenchStrings.ThemePageDescription),
            BitIconName.Color,
            typeof(ThemePage),
            nameof(WorkbenchStrings.CategoryDesignSystem),
            ["BitButton", "BitTextField", "BitDropdown", "BitNavBar", "BitDialog", "BitDataGrid", "BitAiChat"],
            nameof(WorkbenchStrings.ThemePageCaption))
        {
            Switches =
            [
                // The page's tabs, in their order: the token families, the overlays, then the demo's categories of components.
                new("section", nameof(WorkbenchStrings.ThemeSwitchSection),
                [
                    new("colors", nameof(WorkbenchStrings.ThemeColors)),
                    new("typography", nameof(WorkbenchStrings.ThemeTypography)),
                    new("shape", nameof(WorkbenchStrings.ThemeShape)),
                    new("shadows", nameof(WorkbenchStrings.ThemeShadows)),
                    new("sizes", nameof(WorkbenchStrings.ThemeSizes)),
                    new("motion", nameof(WorkbenchStrings.ThemeMotion)),
                    new("layers", nameof(WorkbenchStrings.ThemeLayers)),
                    new("overlays", nameof(WorkbenchStrings.ThemeOverlays)),
                    new("buttons", nameof(WorkbenchStrings.ThemeButtons)),
                    new("inputs", nameof(WorkbenchStrings.ThemeInputs)),
                    new("pickers", nameof(WorkbenchStrings.ThemePickers)),
                    new("layouts", nameof(WorkbenchStrings.ThemeLayouts)),
                    new("lists", nameof(WorkbenchStrings.ThemeLists)),
                    new("navs", nameof(WorkbenchStrings.ThemeNavigation)),
                    new("notifications", nameof(WorkbenchStrings.ThemeNotifications)),
                    new("progress", nameof(WorkbenchStrings.ThemeProgressSection)),
                    new("surfaces", nameof(WorkbenchStrings.ThemeSurfaces)),
                    new("utilities", nameof(WorkbenchStrings.ThemeUtilities)),
                    new("extras", nameof(WorkbenchStrings.ThemeExtras)),
                    new("ai", nameof(WorkbenchStrings.ThemeAi))
                ]),
                new("density", nameof(WorkbenchStrings.ThemeSwitchDensity),
                [
                    new("default", nameof(WorkbenchStrings.ThemeStateDefault)),
                    new("compact", nameof(WorkbenchStrings.ThemeDensityCompact)),
                    new("spacious", nameof(WorkbenchStrings.ThemeDensitySpacious))
                ]),
                new("overlay", nameof(WorkbenchStrings.ThemeSwitchOverlay),
                [
                    new("none", nameof(WorkbenchStrings.ThemeOverlayNone)),
                    new("dropdown", nameof(WorkbenchStrings.ThemeOverlayDropdown)),
                    new("date", nameof(WorkbenchStrings.ThemeOverlayDatePicker)),
                    new("menu", nameof(WorkbenchStrings.ThemeOverlayMenu)),
                    new("callout", nameof(WorkbenchStrings.ThemeOverlayCallout)),
                    new("dialog", nameof(WorkbenchStrings.ThemeOverlayDialog)),
                    new("modal", nameof(WorkbenchStrings.ThemeOverlayModal)),
                    new("panel", nameof(WorkbenchStrings.ThemeOverlayPanel)),
                    new("snackbar", nameof(WorkbenchStrings.ThemeOverlaySnackBar))
                ])
            ]
        },
        new("assistant",
            nameof(WorkbenchStrings.AssistantTitle),
            nameof(WorkbenchStrings.AssistantDescription),
            BitIconName.Robot,
            typeof(AssistantPage),
            nameof(WorkbenchStrings.CategoryAi),
            ["BitAiChat", "BitAiConversationList", "BitAiToolCallView", "BitAiGenerativeUiView", "BitAiArtifactView"],
            nameof(WorkbenchStrings.AssistantCaption)),
        // The app's own settings page, rendered from Client.Core on the view's sample account (Pages/AppPages/Settings/Data).
        new("settings",
            nameof(WorkbenchStrings.SettingsTitle),
            nameof(WorkbenchStrings.SettingsDescription),
            BitIconName.Equalizer,
            // The app's own page, on the view's sample account (See SettingsHost).
            typeof(SettingsHost),
            nameof(WorkbenchStrings.CategoryAppPages),
            ["BitAccordionList", "BitPivot", "BitOtpInput", "BitFileUpload"],
            nameof(WorkbenchStrings.SettingsCaption))
        {
            Switches =
            [
                new(SettingsHost.SectionSwitch, nameof(WorkbenchStrings.SettingsSwitchSection),
                [
                    new(PageUrls.SettingsSections.Profile, nameof(WorkbenchStrings.SettingsSectionProfile)),
                    new(PageUrls.SettingsSections.Account, nameof(WorkbenchStrings.SettingsSectionAccount)),
                    new(PageUrls.SettingsSections.Tfa, nameof(WorkbenchStrings.SettingsSectionTfa)),
                    new(PageUrls.SettingsSections.Sessions, nameof(WorkbenchStrings.SettingsSectionSessions)),
                    new(PageUrls.SettingsSections.Privacy, nameof(WorkbenchStrings.SettingsSectionPrivacy)),
                ]),
                new(SettingsSampleAccount.TwoFactorSwitch, nameof(WorkbenchStrings.SettingsSwitchTwoFactor),
                [
                    new("off", nameof(WorkbenchStrings.SettingsTwoFactorOff)),
                    new(SettingsSampleAccount.On, nameof(WorkbenchStrings.SettingsTwoFactorOn))
                ]),
                new(SettingsSampleAccount.PasskeySwitch, nameof(WorkbenchStrings.SettingsSwitchPasskey),
                [
                    new("off", nameof(WorkbenchStrings.SettingsPasskeyOff)),
                    new(SettingsSampleAccount.On, nameof(WorkbenchStrings.SettingsPasskeyOn))
                ]),
                // What removing a session, changing the email or deleting the account first ask for (AuthManager.TryEnterElevatedAccessMode).
                new(WorkbenchElevation.Switch, nameof(WorkbenchStrings.SettingsSwitchConfirm),
                [
                    new("recent", nameof(WorkbenchStrings.SettingsConfirmRecent)),
                    new(WorkbenchElevation.AskCase, nameof(WorkbenchStrings.SettingsConfirmAsk))
                ])
            ]
        },
        new(NotAuthorizedHost.PageKey,
            nameof(WorkbenchStrings.NotAuthorizedTitle),
            nameof(WorkbenchStrings.NotAuthorizedDescription),
            BitIconName.Lock,
            typeof(NotAuthorizedHost),
            nameof(WorkbenchStrings.CategoryAppPages),
            ["BitImage", "BitStack", "BitButton"],
            nameof(WorkbenchStrings.NotAuthorizedCaption))
        {
            Switches =
            [
                // One per branch of the app's page; its loading one is the Loading data state.
                new(NotAuthorizedHost.CaseSwitch, nameof(WorkbenchStrings.NotAuthorizedSwitchCase),
                [
                    new(NotAuthorizedPage.NoTenantCase, nameof(WorkbenchStrings.NotAuthorizedCaseNoTenant)),
                    new(NotAuthorizedPage.NoTenantsCase, nameof(WorkbenchStrings.NotAuthorizedCaseNoTenants)),
                    new(NotAuthorizedPage.PrivilegeCase, nameof(WorkbenchStrings.NotAuthorizedCasePrivilege)),
                    new(NotAuthorizedPage.ForbiddenCase, nameof(WorkbenchStrings.NotAuthorizedCaseForbidden)),
                    new(NotAuthorizedPage.SignedOutCase, nameof(WorkbenchStrings.SignedOut))
                ])
            ]
        },
        // bitfabric.co's own pages: the app's home page, then the wizard it leads to, rendered from Client.Core (See SitePageHost).
        new(SitePageHost.HomeKey,
            "Home page",
            "The app's home page, bitfabric.co's: the hero, then seven bands that lead a visitor to start, with its onboarding on a first visit",
            BitIconName.Home,
            typeof(HomeHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitButton", "BitModal", "BitAccordion", "BitToggle", "BitLink"],
            "Build with AI. Own everything.")
        {
            // A first visit shows the onboarding, which a returning visitor never sees again
            Switches =
            [
                new(HomeHost.VisitSwitch, "Visit",
                [
                    new("returning", "A returning visitor"),
                    new(HomeHost.FirstVisit, "A first visit")
                ])
            ]
        },
        // Everything every project comes with, each with its film, moved off the home page (bitfabric-internal/specs/home-page.md,
        // 2026-11-18). /contents, the old home page whole, is the app's alone: it's for search engines and links the team hands
        // out, not to design.
        new(SitePageHost.FeaturesKey,
            "Features",
            "The app's /features page, bitfabric.co's: everything every project comes with, band by band, each feature with its film",
            BitIconName.Puzzle,
            typeof(FeaturesHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitButton", "BitModal", "BitProgress", "BitCarousel", "BitAccordion", "BitLink"],
            "What every project comes with"),
        // Why a real app costs less (bitfabric-internal/specs/home-page.md, "The cost page", 2026-11-21), the app's own page
        new(SitePageHost.CostKey,
            "Why it costs less",
            "The app's /cost page, bitfabric.co's: judge the cost of your project, not the price of a token, in three steps. Tokens per task, with Anthropic's own figures as the example, tokens per project, where much is already built and rework is cut, so the project costs less",
            BitIconName.Lightbulb,
            typeof(CostHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitButton", "BitAccordion", "BitIcon", "BitLink"],
            "Judge the cost of your project"),
        // bit CLI, the tool every project is made and kept with, the app's own page; bitplatform.dev's bit CLI page leads to it
        new(SitePageHost.CliKey,
            "bit CLI",
            "The app's /cli page, bitfabric.co's: what bit CLI does for you, stage by stage, that your AI runs it for you, every command in one line, how to install it and how to check the build you run",
            BitIconName.CommandPrompt,
            typeof(CliHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitButton", "BitAccordion", "BitIcon", "BitLink"],
            "Your AI runs it for you"),
        // Competing directions for bitfabric.co's home page, to compare with the app's own (app-home) and with each other (2026-11-19)
        new(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage.Key,
            "Home page directions",
            "Four complete home pages for bitfabric.co, side by side with the app's own: trust first for a careful European buyer, answer first for AI answer engines and search, production-ready to the euro, and nothing to promise. Placeholders mark what isn't known yet",
            BitIconName.Home,
            typeof(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitButton", "BitAccordion", "BitTag", "BitModal", "BitToggle"],
            "Your app, your server, your AI")
        {
            Variants =
            [
                new(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage.TrustKey, "1. Trust first: who we are, what we see"),
                new(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage.AnswersKey, "2. Answer first: quoted by AI and search"),
                new(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage.EuroKey, "3. Day one, to the euro"),
                new(Bit.Fabric.Workbench.Components.Pages.HomeDirections.HomeDirectionsPage.PromiseKey, "4. Nothing to promise")
            ]
        },
        new(SitePageHost.NewProjectKey,
            nameof(WorkbenchStrings.NewProjectTitle),
            nameof(WorkbenchStrings.NewProjectDescription),
            BitIconName.Rocket,
            typeof(NewProjectHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitChoiceGroup", "BitToggle", "BitPanel", "BitAccordionList"],
            nameof(WorkbenchStrings.NewProjectCaption)),
        // The page for a project that already exists (bitfabric-internal/specs/getting-started.md, 2026-11-18): a teammate's, or
        // one on another computer
        new(SitePageHost.SetupProjectKey,
            "Set up an existing project",
            "The app's page that sets up a project that already exists: its repository's address, the computer and its apps, then the one line that clones it and gets it ready",
            BitIconName.CloudDownload,
            typeof(SetupProjectHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitTextField", "BitChoiceGroup", "BitToggle", "BitMessage"],
            "Your project, on any computer"),
        // The app's growth page (bitfabric-internal/specs/growth.md), rendered from Client.Core on made-up sample worlds,
        // counted the way the server counts (Components/Pages/Growth/Data).
        new("growth",
            "Growth",
            "The app's growth page for global admins: projects made each day, Trials that became Licenses and how fast, licensed projects in production and the teams behind them. Totals only, on made-up sample numbers",
            BitIconName.AreaChart,
            typeof(Bit.Fabric.Workbench.Components.Pages.Growth.GrowthHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitChart", "BitButtonGroup", "BitShimmer", "BitEmptyState"],
            "Projects and licenses, over time")
        {
            Switches =
            [
                new(Bit.Fabric.Workbench.Components.Pages.Growth.Data.GrowthSamples.HistorySwitch, "History",
                [
                    new(Bit.Fabric.Workbench.Components.Pages.Growth.Data.GrowthSamples.Months, "Months of growth"),
                    new(Bit.Fabric.Workbench.Components.Pages.Growth.Data.GrowthSamples.FirstDay, "A first day"),
                    new(Bit.Fabric.Workbench.Components.Pages.Growth.Data.GrowthSamples.SlowWeek, "A slow week")
                ])
            ]
        },
        // Monitoring (bitfabric-internal/specs/monitoring.md): the app's own pages, rendered from Client.Core on the sample world of
        // Components/Pages/Monitoring/Data, so QA sees every state of what ships. Texts are literals, as the Workbench's own.
        new("projects",
            "Projects",
            "The app's projects page: each app with its health, its last 30 days and its scans, filtered and searched; no team yet, none yet, or signed out",
            BitIconName.Health,
            typeof(ProjectsHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitTag", "BitShimmer", "BitImage", "BitSearchBox"],
            "Every part of your app, watched")
        {
            Switches =
            [
                new(ProjectsHost.AmountSwitch, "Projects",
                [
                    new("some", "Eight, in every state"),
                    new("many", "Fourteen, as an agency has")
                ]),
                new(ProjectsHost.TeamSwitch, "Team",
                [
                    new("yes", "In a team"),
                    new(ProjectsHost.NoTeam, "No team yet")
                ])
            ]
        },
        new("project-connect",
            "Add a project",
            "The app's add page: the app's address, what bit Fabric finds there, the approval in the customer's own app and the first checks",
            BitIconName.Add,
            typeof(ProjectConnectHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitTextField", "BitMessage", "BitRingLoading", "BitTag"],
            "Connected in a minute, without touching your code")
        {
            Switches =
            [
                new(ProjectConnectHost.CaseSwitch, "Case",
                [
                    new("start", "Typing the address"),
                    new("found", "Found at the address"),
                    new("static", "Found, its report on its server"),
                    new("unknown-version", "Found, version unknown"),
                    new("already", "Found, watched already"),
                    new("not-bit", "No bit app there"),
                    new("unreachable", "The address doesn't answer"),
                    new(WorkbenchApprovalNavigation.ApprovingCase, "Approving in the app"),
                    new(WorkbenchApprovalNavigation.DeclinedCase, "The approval was declined"),
                    new("callback", "Back from the approval, approved"),
                    new("callback-declined", "Back from the approval, declined"),
                    new(ProjectConnectHost.ReconnectCase, "Reconnecting an app"),
                    new(ProjectConnectHost.ConnectedCase, "Connected, readings arriving"),
                    new(ProjectConnectHost.AllInCase, "Connected, every location in"),
                    new(ProjectConnectHost.BlockedCase, "Connected, notifications blocked")
                ])
            ]
        },
        new("project-ops",
            "A project",
            "The app's project page: health, history, incidents, performance, scans, SLA, locations, status page, alerts and settings",
            BitIconName.ServerProcesses,
            typeof(ProjectOpsHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitPivot", "BitResourceTimeline", "BitChart", "BitTimeline", "BitAccordion"],
            "What happened, part by part, minute by minute")
        {
            Switches =
            [
                new(ProjectOpsHost.ProjectSwitch, "Project",
                [
                    new("yourapp", "Healthy, everything set up"),
                    new("fieldnote", "Email failing, someone's on it"),
                    new("brightcart", "Down, nobody on it yet"),
                    new("ledgerline", "In maintenance"),
                    new("crewboard", "Needs reconnecting"),
                    new("parcelpoint", "Just connected"),
                    new("inkpad", "Cache warning, parts not configured"),
                    new("stockroom", "Disconnected")
                ]),
                new(ProjectOpsHost.TabSwitch, "Tab",
                [
                    new("overview", "Overview"),
                    new("map", "Application map"),
                    new("history", "History"),
                    new("incidents", "Incidents"),
                    new("performance", "Performance"),
                    new("scans", "Scans"),
                    new("sla", "SLA"),
                    new("locations", "Locations"),
                    new("status", "Status page"),
                    new("alerts", "Alerts"),
                    new("settings", "Settings")
                ]),
                // What a tab shows once one of its buttons is pressed; each opens its own tab.
                new(ProjectOpsHost.PressSwitch, "Pressed",
                [
                    new("none", "Nothing yet"),
                    new("test-alert", "Alerts: a test alert sent"),
                    new("credits", "SLA: changing the credits"),
                    new("disconnect", "Settings: disconnecting"),
                    new("remove", "Settings: removing it")
                ])
            ]
        },
        // A project's telemetry (bitfabric-internal/specs/monitoring.md, 2026-11-27), designed here on sample numbers before the
        // app builds it: the ready cards, the cards and alerts the customer's AI added through MCP, each deleted here.
        new(Components.Pages.ProjectTelemetry.ProjectTelemetryPage.Key,
            "Telemetry",
            "A project's telemetry: the ready cards every project gets, the cards and alerts its own AI added through MCP and the way to ask for more, arranged by dragging, sized and hidden by each person, and deleted here",
            BitIconName.Diagnostic,
            typeof(Components.Pages.ProjectTelemetry.ProjectTelemetryPage),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["Bortable", "BitPopConfirm", "BitCopyButton", "BitMenuButton", "BitToggle", "BitEmptyState"],
            "Ask your AI for a card or an alert")
        {
            Variants =
            [
                new(Components.Pages.ProjectTelemetry.ProjectTelemetryPage.ByAreaKey, "1. By area, in groups that fold away"),
                new(Components.Pages.ProjectTelemetry.ProjectTelemetryPage.OneBoardKey, "2. One board, filtered by area")
            ],
            Switches =
            [
                new(Components.Pages.ProjectTelemetry.Data.TelemetryDashboardData.AiSwitch, "Your AI's",
                [
                    new("some", "Two cards and three alerts"),
                    new(Components.Pages.ProjectTelemetry.Data.TelemetryDashboardData.AiNone, "Nothing yet")
                ]),
                new(Components.Pages.ProjectTelemetry.Data.TelemetryDashboardData.AlertSwitch, "Alerts",
                [
                    new("firing", "A job alert firing"),
                    new(Components.Pages.ProjectTelemetry.Data.TelemetryDashboardData.AlertQuiet, "All quiet")
                ])
            ]
        },
        new("status-page",
            "Status page",
            "The app's public status page on the customer's own domain: no sign-in, the visitor's language, a link to the app and following without an account",
            BitIconName.Globe,
            typeof(StatusPageHost),
            nameof(WorkbenchStrings.CategoryBitFabric),
            ["BitImage", "BitIcon", "BitText"],
            "Your customers know before they ask")
        {
            // Served on the customer's status subdomain, so the app's shell is never around it.
            HasAppShell = false,
            Switches =
            [
                new(StatusPageHost.ProjectSwitch, "State",
                [
                    new("yourapp", "Everything works, two languages"),
                    new("fieldnote", "Email failing"),
                    new("brightcart", "The app is down"),
                    new("ledgerline", "In maintenance"),
                    new("crewboard", "bit Fabric can't check it"),
                    new("inkpad", "Not published")
                ]),
                new(StatusPageHost.FollowSwitch, "Following",
                [
                    new("none", "Not yet"),
                    new("following", "Following"),
                    new("unfollowed", "Stopped following"),
                    new("denied", "The browser blocks it"),
                    new("ios", "On an iPhone")
                ])
            ]
        }
    ];

    /// <summary>The page with this key, ignoring case, or null.</summary>
    public static WorkbenchPageInfo? Find(string? key) => Pages.FirstOrDefault(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
}
