namespace Bit.Fabric.Workbench.Components.Pages.Site;

/// <summary>
/// Hosts the app's home page, which needs nothing from the server. Its onboarding shows on a visitor's first visit only: the app
/// tells a first visit from the browser's record of the tour, and here the Visit switch says which visit the view is (See
/// WorkbenchOnboardingStore). The tour lives in the preview's layout, as the app's lives in its own, since it goes to the
/// wizard and back (See PreviewOnboardingHost).
/// </summary>
public partial class HomeHost
{
    /// <summary>The page's switch between a returning visitor, who sees no onboarding, and a first visit.</summary>
    public const string VisitSwitch = "visit";

    public const string FirstVisit = "first";
}
