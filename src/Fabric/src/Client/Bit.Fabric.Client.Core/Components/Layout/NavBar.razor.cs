namespace Bit.Fabric.Client.Core.Components.Layout;

public partial class NavBar
{
    /// <summary>
    /// Every address of a page but the one its option links to: the culture-less one the app's own code navigates to, and
    /// the page under each culture, so the option is marked whichever the page was opened by. Several pages for an option
    /// that stands for a group of them, as Compare does for each comparison.
    /// </summary>
    private static string[] AddressesOf(params string[] urls) => [.. urls, .. urls.SelectMany(CultureInfoManager.GetCultureUrls)];
}
