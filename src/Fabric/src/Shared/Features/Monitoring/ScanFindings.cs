using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// Every finding a scanner gives, by its stable key (bitfabric-internal/specs/scanners.md, Decisions, 2026-11-13). A
/// finding is stored as its key, its parameters, its severity and where it was seen, never as text, so each viewer reads it
/// in their own language: what it means in plain words (<see cref="Texts.Title"/>), why it matters and the fix, formatted
/// with its parameters.
/// </summary>
public static class ScanFindings
{
    // Security
    public const string HttpsRedirectMissing = nameof(HttpsRedirectMissing);
    public const string HstsMissing = nameof(HstsMissing);
    /// <summary>{0}: the days HSTS's max-age lasts.</summary>
    public const string HstsShort = nameof(HstsShort);
    public const string CspMissing = nameof(CspMissing);
    public const string CspUnsafeInline = nameof(CspUnsafeInline);
    public const string CspUnsafeEval = nameof(CspUnsafeEval);
    public const string FrameProtectionMissing = nameof(FrameProtectionMissing);
    public const string NosniffMissing = nameof(NosniffMissing);
    public const string ReferrerPolicyMissing = nameof(ReferrerPolicyMissing);
    /// <summary>{0}: the cookies' names.</summary>
    public const string CookieNotSecure = nameof(CookieNotSecure);
    /// <summary>{0}: the cookies' names.</summary>
    public const string CookieNotHttpOnly = nameof(CookieNotHttpOnly);
    /// <summary>{0}: the cookies' names.</summary>
    public const string CookieNoSameSite = nameof(CookieNoSameSite);
    /// <summary>{0}: the headers and their values.</summary>
    public const string ServerVersionDisclosed = nameof(ServerVersionDisclosed);
    public const string HealthzOpen = nameof(HealthzOpen);
    public const string HangfireOpen = nameof(HangfireOpen);
    public const string DevMcpOpen = nameof(DevMcpOpen);
    public const string ApiDocsOpen = nameof(ApiDocsOpen);
    public const string ODataMetadataOpen = nameof(ODataMetadataOpen);

    // Domain and certificate
    /// <summary>{0}: the host.</summary>
    public const string CertificateExpired = nameof(CertificateExpired);
    /// <summary>{0}: the host; {1}: the days left.</summary>
    public const string CertificateExpiresSoon = nameof(CertificateExpiresSoon);
    /// <summary>{0}: the host.</summary>
    public const string CertificateNameMismatch = nameof(CertificateNameMismatch);
    /// <summary>{0}: the host.</summary>
    public const string CertificateChainInvalid = nameof(CertificateChainInvalid);
    /// <summary>{0}: the registered domain.</summary>
    public const string DomainExpired = nameof(DomainExpired);
    /// <summary>{0}: the registered domain; {1}: the days left.</summary>
    public const string DomainExpiresSoon = nameof(DomainExpiresSoon);

    // SEO
    public const string NoIndex = nameof(NoIndex);
    public const string RobotsBlocksAll = nameof(RobotsBlocksAll);
    public const string NotPrerendered = nameof(NotPrerendered);
    public const string TitleMissing = nameof(TitleMissing);
    public const string TitleLength = nameof(TitleLength);
    public const string DescriptionMissing = nameof(DescriptionMissing);
    public const string DescriptionLength = nameof(DescriptionLength);
    /// <summary>{0}: the title the pages share.</summary>
    public const string DuplicateTitles = nameof(DuplicateTitles);
    public const string DuplicateDescriptions = nameof(DuplicateDescriptions);
    public const string CanonicalMissing = nameof(CanonicalMissing);
    public const string LangMissing = nameof(LangMissing);
    public const string HreflangMissing = nameof(HreflangMissing);
    /// <summary>{0}: the Open Graph tags missing, such as <c>og:image</c>.</summary>
    public const string SocialPreviewMissing = nameof(SocialPreviewMissing);
    /// <summary>{0}: how many images.</summary>
    public const string ImagesWithoutAlt = nameof(ImagesWithoutAlt);
    public const string RobotsTxtMissing = nameof(RobotsTxtMissing);
    public const string RobotsTxtNoSitemap = nameof(RobotsTxtNoSitemap);
    public const string SitemapMissing = nameof(SitemapMissing);

    // Performance (Lighthouse; the metrics' findings come from the phone runs, which Google ranks by)
    /// <summary>{0}: the seconds until the main content shows, on a phone.</summary>
    public const string LcpSlow = nameof(LcpSlow);
    /// <summary>{0}: the layout shift score, on a phone.</summary>
    public const string ClsHigh = nameof(ClsHigh);
    /// <summary>{0}: the milliseconds the page doesn't respond while it loads, on a phone.</summary>
    public const string TbtHigh = nameof(TbtHigh);
    /// <summary>{0}: the seconds until anything shows, on a phone.</summary>
    public const string FcpSlow = nameof(FcpSlow);
    /// <summary>{0}: the KiB the images could be smaller by.</summary>
    public const string ImagesTooBig = nameof(ImagesTooBig);
    /// <summary>{0}: how many files.</summary>
    public const string FilesNotCached = nameof(FilesNotCached);
    /// <summary>{0}: the milliseconds they hold up the first paint.</summary>
    public const string RenderBlocking = nameof(RenderBlocking);
    /// <summary>{0}: the KiB of scripts and styles the page doesn't use or could be smaller by.</summary>
    public const string UnusedCode = nameof(UnusedCode);
    /// <summary>{0}: the seconds of work on the main thread.</summary>
    public const string MainThreadBusy = nameof(MainThreadBusy);
    /// <summary>{0}: how many third parties.</summary>
    public const string ThirdPartyCode = nameof(ThirdPartyCode);
    /// <summary>{0}: the milliseconds until the server's first byte.</summary>
    public const string PageSlowToArrive = nameof(PageSlowToArrive);
    public const string LcpImageFoundLate = nameof(LcpImageFoundLate);
    /// <summary>{0}: how many elements.</summary>
    public const string LayoutShiftCauses = nameof(LayoutShiftCauses);
    public const string FontsHideText = nameof(FontsHideText);
    /// <summary>{0}: how many elements the page has.</summary>
    public const string DomTooLarge = nameof(DomTooLarge);
    /// <summary>{0}: how many requests.</summary>
    public const string OldHttpProtocol = nameof(OldHttpProtocol);
    /// <summary>{0}: the KiB the page downloads.</summary>
    public const string PageTooHeavy = nameof(PageTooHeavy);
    public const string BackForwardCacheBlocked = nameof(BackForwardCacheBlocked);
    public const string ViewportNotSet = nameof(ViewportNotSet);
    /// <summary>{0}: the Lighthouse audit's id, such as <c>uses-rel-preconnect</c>.</summary>
    public const string PerformanceAuditFailed = nameof(PerformanceAuditFailed);

    // Accessibility (Lighthouse's axe-core audits)
    /// <summary>{0}: how many elements.</summary>
    public const string ContrastTooLow = nameof(ContrastTooLow);
    /// <summary>{0}: how many buttons.</summary>
    public const string ButtonsWithoutName = nameof(ButtonsWithoutName);
    /// <summary>{0}: how many images.</summary>
    public const string ImagesWithoutDescription = nameof(ImagesWithoutDescription);
    /// <summary>{0}: how many links.</summary>
    public const string LinksWithoutName = nameof(LinksWithoutName);
    /// <summary>{0}: how many fields.</summary>
    public const string FieldsWithoutLabel = nameof(FieldsWithoutLabel);
    public const string PageLanguageWrong = nameof(PageLanguageWrong);
    public const string ZoomBlocked = nameof(ZoomBlocked);
    /// <summary>{0}: how many elements.</summary>
    public const string AriaMisused = nameof(AriaMisused);
    /// <summary>{0}: how many elements.</summary>
    public const string TapTargetsTooSmall = nameof(TapTargetsTooSmall);
    /// <summary>{0}: how many elements.</summary>
    public const string StructureUnclear = nameof(StructureUnclear);
    /// <summary>{0}: how many elements.</summary>
    public const string KeyboardNavigation = nameof(KeyboardNavigation);
    /// <summary>{0}: the Lighthouse audit's id, such as <c>video-caption</c>.</summary>
    public const string AccessibilityAuditFailed = nameof(AccessibilityAuditFailed);

    /// <summary>The resx keys of a finding's three texts.</summary>
    public sealed record Texts(string Title, string Why, string Fix);

    public static IReadOnlyDictionary<string, Texts> All { get; } = new Dictionary<string, Texts>
    {
        [HttpsRedirectMissing] = new(nameof(AppStrings.ScanFindingHttpsRedirectMissing), nameof(AppStrings.ScanFindingHttpsRedirectMissingWhy), nameof(AppStrings.ScanFindingHttpsRedirectMissingFix)),
        [HstsMissing] = new(nameof(AppStrings.ScanFindingHstsMissing), nameof(AppStrings.ScanFindingHstsMissingWhy), nameof(AppStrings.ScanFindingHstsMissingFix)),
        [HstsShort] = new(nameof(AppStrings.ScanFindingHstsShort), nameof(AppStrings.ScanFindingHstsShortWhy), nameof(AppStrings.ScanFindingHstsShortFix)),
        [CspMissing] = new(nameof(AppStrings.ScanFindingCspMissing), nameof(AppStrings.ScanFindingCspMissingWhy), nameof(AppStrings.ScanFindingCspMissingFix)),
        [CspUnsafeInline] = new(nameof(AppStrings.ScanFindingCspUnsafeInline), nameof(AppStrings.ScanFindingCspUnsafeInlineWhy), nameof(AppStrings.ScanFindingCspUnsafeInlineFix)),
        [CspUnsafeEval] = new(nameof(AppStrings.ScanFindingCspUnsafeEval), nameof(AppStrings.ScanFindingCspUnsafeEvalWhy), nameof(AppStrings.ScanFindingCspUnsafeEvalFix)),
        [FrameProtectionMissing] = new(nameof(AppStrings.ScanFindingFrameProtectionMissing), nameof(AppStrings.ScanFindingFrameProtectionMissingWhy), nameof(AppStrings.ScanFindingFrameProtectionMissingFix)),
        [NosniffMissing] = new(nameof(AppStrings.ScanFindingNosniffMissing), nameof(AppStrings.ScanFindingNosniffMissingWhy), nameof(AppStrings.ScanFindingNosniffMissingFix)),
        [ReferrerPolicyMissing] = new(nameof(AppStrings.ScanFindingReferrerPolicyMissing), nameof(AppStrings.ScanFindingReferrerPolicyMissingWhy), nameof(AppStrings.ScanFindingReferrerPolicyMissingFix)),
        [CookieNotSecure] = new(nameof(AppStrings.ScanFindingCookieNotSecure), nameof(AppStrings.ScanFindingCookieNotSecureWhy), nameof(AppStrings.ScanFindingCookieNotSecureFix)),
        [CookieNotHttpOnly] = new(nameof(AppStrings.ScanFindingCookieNotHttpOnly), nameof(AppStrings.ScanFindingCookieNotHttpOnlyWhy), nameof(AppStrings.ScanFindingCookieNotHttpOnlyFix)),
        [CookieNoSameSite] = new(nameof(AppStrings.ScanFindingCookieNoSameSite), nameof(AppStrings.ScanFindingCookieNoSameSiteWhy), nameof(AppStrings.ScanFindingCookieNoSameSiteFix)),
        [ServerVersionDisclosed] = new(nameof(AppStrings.ScanFindingServerVersionDisclosed), nameof(AppStrings.ScanFindingServerVersionDisclosedWhy), nameof(AppStrings.ScanFindingServerVersionDisclosedFix)),
        [HealthzOpen] = new(nameof(AppStrings.ScanFindingHealthzOpen), nameof(AppStrings.ScanFindingHealthzOpenWhy), nameof(AppStrings.ScanFindingHealthzOpenFix)),
        [HangfireOpen] = new(nameof(AppStrings.ScanFindingHangfireOpen), nameof(AppStrings.ScanFindingHangfireOpenWhy), nameof(AppStrings.ScanFindingHangfireOpenFix)),
        [DevMcpOpen] = new(nameof(AppStrings.ScanFindingDevMcpOpen), nameof(AppStrings.ScanFindingDevMcpOpenWhy), nameof(AppStrings.ScanFindingDevMcpOpenFix)),
        [ApiDocsOpen] = new(nameof(AppStrings.ScanFindingApiDocsOpen), nameof(AppStrings.ScanFindingApiDocsOpenWhy), nameof(AppStrings.ScanFindingApiDocsOpenFix)),
        [ODataMetadataOpen] = new(nameof(AppStrings.ScanFindingODataMetadataOpen), nameof(AppStrings.ScanFindingODataMetadataOpenWhy), nameof(AppStrings.ScanFindingODataMetadataOpenFix)),

        [CertificateExpired] = new(nameof(AppStrings.ScanFindingCertificateExpired), nameof(AppStrings.ScanFindingCertificateExpiredWhy), nameof(AppStrings.ScanFindingCertificateExpiredFix)),
        [CertificateExpiresSoon] = new(nameof(AppStrings.ScanFindingCertificateExpiresSoon), nameof(AppStrings.ScanFindingCertificateExpiresSoonWhy), nameof(AppStrings.ScanFindingCertificateExpiresSoonFix)),
        [CertificateNameMismatch] = new(nameof(AppStrings.ScanFindingCertificateNameMismatch), nameof(AppStrings.ScanFindingCertificateNameMismatchWhy), nameof(AppStrings.ScanFindingCertificateNameMismatchFix)),
        [CertificateChainInvalid] = new(nameof(AppStrings.ScanFindingCertificateChainInvalid), nameof(AppStrings.ScanFindingCertificateChainInvalidWhy), nameof(AppStrings.ScanFindingCertificateChainInvalidFix)),
        [DomainExpired] = new(nameof(AppStrings.ScanFindingDomainExpired), nameof(AppStrings.ScanFindingDomainExpiredWhy), nameof(AppStrings.ScanFindingDomainExpiredFix)),
        [DomainExpiresSoon] = new(nameof(AppStrings.ScanFindingDomainExpiresSoon), nameof(AppStrings.ScanFindingDomainExpiresSoonWhy), nameof(AppStrings.ScanFindingDomainExpiresSoonFix)),

        [NoIndex] = new(nameof(AppStrings.ScanFindingNoIndex), nameof(AppStrings.ScanFindingNoIndexWhy), nameof(AppStrings.ScanFindingNoIndexFix)),
        [RobotsBlocksAll] = new(nameof(AppStrings.ScanFindingRobotsBlocksAll), nameof(AppStrings.ScanFindingRobotsBlocksAllWhy), nameof(AppStrings.ScanFindingRobotsBlocksAllFix)),
        [NotPrerendered] = new(nameof(AppStrings.ScanFindingNotPrerendered), nameof(AppStrings.ScanFindingNotPrerenderedWhy), nameof(AppStrings.ScanFindingNotPrerenderedFix)),
        [TitleMissing] = new(nameof(AppStrings.ScanFindingTitleMissing), nameof(AppStrings.ScanFindingTitleMissingWhy), nameof(AppStrings.ScanFindingTitleMissingFix)),
        [TitleLength] = new(nameof(AppStrings.ScanFindingTitleLength), nameof(AppStrings.ScanFindingTitleLengthWhy), nameof(AppStrings.ScanFindingTitleLengthFix)),
        [DescriptionMissing] = new(nameof(AppStrings.ScanFindingDescriptionMissing), nameof(AppStrings.ScanFindingDescriptionMissingWhy), nameof(AppStrings.ScanFindingDescriptionMissingFix)),
        [DescriptionLength] = new(nameof(AppStrings.ScanFindingDescriptionLength), nameof(AppStrings.ScanFindingDescriptionLengthWhy), nameof(AppStrings.ScanFindingDescriptionLengthFix)),
        [DuplicateTitles] = new(nameof(AppStrings.ScanFindingDuplicateTitles), nameof(AppStrings.ScanFindingDuplicateTitlesWhy), nameof(AppStrings.ScanFindingDuplicateTitlesFix)),
        [DuplicateDescriptions] = new(nameof(AppStrings.ScanFindingDuplicateDescriptions), nameof(AppStrings.ScanFindingDuplicateDescriptionsWhy), nameof(AppStrings.ScanFindingDuplicateDescriptionsFix)),
        [CanonicalMissing] = new(nameof(AppStrings.ScanFindingCanonicalMissing), nameof(AppStrings.ScanFindingCanonicalMissingWhy), nameof(AppStrings.ScanFindingCanonicalMissingFix)),
        [LangMissing] = new(nameof(AppStrings.ScanFindingLangMissing), nameof(AppStrings.ScanFindingLangMissingWhy), nameof(AppStrings.ScanFindingLangMissingFix)),
        [HreflangMissing] = new(nameof(AppStrings.ScanFindingHreflangMissing), nameof(AppStrings.ScanFindingHreflangMissingWhy), nameof(AppStrings.ScanFindingHreflangMissingFix)),
        [SocialPreviewMissing] = new(nameof(AppStrings.ScanFindingSocialPreviewMissing), nameof(AppStrings.ScanFindingSocialPreviewMissingWhy), nameof(AppStrings.ScanFindingSocialPreviewMissingFix)),
        [ImagesWithoutAlt] = new(nameof(AppStrings.ScanFindingImagesWithoutAlt), nameof(AppStrings.ScanFindingImagesWithoutAltWhy), nameof(AppStrings.ScanFindingImagesWithoutAltFix)),
        [RobotsTxtMissing] = new(nameof(AppStrings.ScanFindingRobotsTxtMissing), nameof(AppStrings.ScanFindingRobotsTxtMissingWhy), nameof(AppStrings.ScanFindingRobotsTxtMissingFix)),
        [RobotsTxtNoSitemap] = new(nameof(AppStrings.ScanFindingRobotsTxtNoSitemap), nameof(AppStrings.ScanFindingRobotsTxtNoSitemapWhy), nameof(AppStrings.ScanFindingRobotsTxtNoSitemapFix)),
        [SitemapMissing] = new(nameof(AppStrings.ScanFindingSitemapMissing), nameof(AppStrings.ScanFindingSitemapMissingWhy), nameof(AppStrings.ScanFindingSitemapMissingFix)),

        [LcpSlow] = new(nameof(AppStrings.ScanFindingLcpSlow), nameof(AppStrings.ScanFindingLcpSlowWhy), nameof(AppStrings.ScanFindingLcpSlowFix)),
        [ClsHigh] = new(nameof(AppStrings.ScanFindingClsHigh), nameof(AppStrings.ScanFindingClsHighWhy), nameof(AppStrings.ScanFindingClsHighFix)),
        [TbtHigh] = new(nameof(AppStrings.ScanFindingTbtHigh), nameof(AppStrings.ScanFindingTbtHighWhy), nameof(AppStrings.ScanFindingTbtHighFix)),
        [FcpSlow] = new(nameof(AppStrings.ScanFindingFcpSlow), nameof(AppStrings.ScanFindingFcpSlowWhy), nameof(AppStrings.ScanFindingFcpSlowFix)),
        [ImagesTooBig] = new(nameof(AppStrings.ScanFindingImagesTooBig), nameof(AppStrings.ScanFindingImagesTooBigWhy), nameof(AppStrings.ScanFindingImagesTooBigFix)),
        [FilesNotCached] = new(nameof(AppStrings.ScanFindingFilesNotCached), nameof(AppStrings.ScanFindingFilesNotCachedWhy), nameof(AppStrings.ScanFindingFilesNotCachedFix)),
        [RenderBlocking] = new(nameof(AppStrings.ScanFindingRenderBlocking), nameof(AppStrings.ScanFindingRenderBlockingWhy), nameof(AppStrings.ScanFindingRenderBlockingFix)),
        [UnusedCode] = new(nameof(AppStrings.ScanFindingUnusedCode), nameof(AppStrings.ScanFindingUnusedCodeWhy), nameof(AppStrings.ScanFindingUnusedCodeFix)),
        [MainThreadBusy] = new(nameof(AppStrings.ScanFindingMainThreadBusy), nameof(AppStrings.ScanFindingMainThreadBusyWhy), nameof(AppStrings.ScanFindingMainThreadBusyFix)),
        [ThirdPartyCode] = new(nameof(AppStrings.ScanFindingThirdPartyCode), nameof(AppStrings.ScanFindingThirdPartyCodeWhy), nameof(AppStrings.ScanFindingThirdPartyCodeFix)),
        [PageSlowToArrive] = new(nameof(AppStrings.ScanFindingPageSlowToArrive), nameof(AppStrings.ScanFindingPageSlowToArriveWhy), nameof(AppStrings.ScanFindingPageSlowToArriveFix)),
        [LcpImageFoundLate] = new(nameof(AppStrings.ScanFindingLcpImageFoundLate), nameof(AppStrings.ScanFindingLcpImageFoundLateWhy), nameof(AppStrings.ScanFindingLcpImageFoundLateFix)),
        [LayoutShiftCauses] = new(nameof(AppStrings.ScanFindingLayoutShiftCauses), nameof(AppStrings.ScanFindingLayoutShiftCausesWhy), nameof(AppStrings.ScanFindingLayoutShiftCausesFix)),
        [FontsHideText] = new(nameof(AppStrings.ScanFindingFontsHideText), nameof(AppStrings.ScanFindingFontsHideTextWhy), nameof(AppStrings.ScanFindingFontsHideTextFix)),
        [DomTooLarge] = new(nameof(AppStrings.ScanFindingDomTooLarge), nameof(AppStrings.ScanFindingDomTooLargeWhy), nameof(AppStrings.ScanFindingDomTooLargeFix)),
        [OldHttpProtocol] = new(nameof(AppStrings.ScanFindingOldHttpProtocol), nameof(AppStrings.ScanFindingOldHttpProtocolWhy), nameof(AppStrings.ScanFindingOldHttpProtocolFix)),
        [PageTooHeavy] = new(nameof(AppStrings.ScanFindingPageTooHeavy), nameof(AppStrings.ScanFindingPageTooHeavyWhy), nameof(AppStrings.ScanFindingPageTooHeavyFix)),
        [BackForwardCacheBlocked] = new(nameof(AppStrings.ScanFindingBackForwardCacheBlocked), nameof(AppStrings.ScanFindingBackForwardCacheBlockedWhy), nameof(AppStrings.ScanFindingBackForwardCacheBlockedFix)),
        [ViewportNotSet] = new(nameof(AppStrings.ScanFindingViewportNotSet), nameof(AppStrings.ScanFindingViewportNotSetWhy), nameof(AppStrings.ScanFindingViewportNotSetFix)),
        [PerformanceAuditFailed] = new(nameof(AppStrings.ScanFindingPerformanceAuditFailed), nameof(AppStrings.ScanFindingPerformanceAuditFailedWhy), nameof(AppStrings.ScanFindingPerformanceAuditFailedFix)),

        [ContrastTooLow] = new(nameof(AppStrings.ScanFindingContrastTooLow), nameof(AppStrings.ScanFindingContrastTooLowWhy), nameof(AppStrings.ScanFindingContrastTooLowFix)),
        [ButtonsWithoutName] = new(nameof(AppStrings.ScanFindingButtonsWithoutName), nameof(AppStrings.ScanFindingButtonsWithoutNameWhy), nameof(AppStrings.ScanFindingButtonsWithoutNameFix)),
        [ImagesWithoutDescription] = new(nameof(AppStrings.ScanFindingImagesWithoutDescription), nameof(AppStrings.ScanFindingImagesWithoutDescriptionWhy), nameof(AppStrings.ScanFindingImagesWithoutDescriptionFix)),
        [LinksWithoutName] = new(nameof(AppStrings.ScanFindingLinksWithoutName), nameof(AppStrings.ScanFindingLinksWithoutNameWhy), nameof(AppStrings.ScanFindingLinksWithoutNameFix)),
        [FieldsWithoutLabel] = new(nameof(AppStrings.ScanFindingFieldsWithoutLabel), nameof(AppStrings.ScanFindingFieldsWithoutLabelWhy), nameof(AppStrings.ScanFindingFieldsWithoutLabelFix)),
        [PageLanguageWrong] = new(nameof(AppStrings.ScanFindingPageLanguageWrong), nameof(AppStrings.ScanFindingPageLanguageWrongWhy), nameof(AppStrings.ScanFindingPageLanguageWrongFix)),
        [ZoomBlocked] = new(nameof(AppStrings.ScanFindingZoomBlocked), nameof(AppStrings.ScanFindingZoomBlockedWhy), nameof(AppStrings.ScanFindingZoomBlockedFix)),
        [AriaMisused] = new(nameof(AppStrings.ScanFindingAriaMisused), nameof(AppStrings.ScanFindingAriaMisusedWhy), nameof(AppStrings.ScanFindingAriaMisusedFix)),
        [TapTargetsTooSmall] = new(nameof(AppStrings.ScanFindingTapTargetsTooSmall), nameof(AppStrings.ScanFindingTapTargetsTooSmallWhy), nameof(AppStrings.ScanFindingTapTargetsTooSmallFix)),
        [StructureUnclear] = new(nameof(AppStrings.ScanFindingStructureUnclear), nameof(AppStrings.ScanFindingStructureUnclearWhy), nameof(AppStrings.ScanFindingStructureUnclearFix)),
        [KeyboardNavigation] = new(nameof(AppStrings.ScanFindingKeyboardNavigation), nameof(AppStrings.ScanFindingKeyboardNavigationWhy), nameof(AppStrings.ScanFindingKeyboardNavigationFix)),
        [AccessibilityAuditFailed] = new(nameof(AppStrings.ScanFindingAccessibilityAuditFailed), nameof(AppStrings.ScanFindingAccessibilityAuditFailedWhy), nameof(AppStrings.ScanFindingAccessibilityAuditFailedFix))
    };

    /// <summary>A finding's three texts in the viewer's language, or null for a key this build doesn't know.</summary>
    public static (string Title, string Why, string Fix)? Localize(IStringLocalizer localizer, ProjectScanFindingDto finding)
    {
        if (finding.Key is null || All.TryGetValue(finding.Key, out var texts) is false)
            return null;

        object[] parameters = [.. finding.Parameters];

        return (localizer[texts.Title, parameters], localizer[texts.Why, parameters], localizer[texts.Fix, parameters]);
    }

    /// <summary>
    /// Every kind of scan, in the order a project's page and card show them, as the approved card design orders them. The
    /// server asks only for the kinds it can run (See IProjectScanner.IsAvailable): Performance and Accessibility need its
    /// Lighthouse service.
    /// </summary>
    public static ProjectScanKind[] AvailableKinds { get; } = [ProjectScanKind.Security, ProjectScanKind.Performance, ProjectScanKind.Seo, ProjectScanKind.Accessibility, ProjectScanKind.DomainAndCertificate];

    /// <summary>The kinds a card shows on their own, the rest folded into "+N more", as the approved card design draws it.</summary>
    public static bool IsMainKind(ProjectScanKind kind) => kind is ProjectScanKind.Security or ProjectScanKind.Performance or ProjectScanKind.Seo or ProjectScanKind.Accessibility;
}
