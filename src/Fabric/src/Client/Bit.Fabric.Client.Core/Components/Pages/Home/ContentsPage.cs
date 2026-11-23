namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// bitfabric.co/contents: the home page as it was until it became seven bands that lead a visitor
/// to start, every band of it, rendered by <see cref="FeaturesPage"/>. It isn't for the site's visitors: it's in no menu, and
/// is there for search engines, which the sitemap and llms.txt lead to it, and for a link the team hands to someone.
/// It is temporary, and leaves the onboarding and the site's JSON-LD to the home page.
/// </summary>
[Route(PageUrls.Contents)]
[Route("{culture:nonfile?}" + PageUrls.Contents)]
[AppResponseCache(SharedMaxAge = 3600 * 24, MaxAge = 60 * 5)]
public partial class ContentsPage : FeaturesPage
{
    protected override bool IsOldHomePage => true;
}
