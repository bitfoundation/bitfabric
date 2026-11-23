namespace Bit.Fabric.Client.Core.Components.Common;

public partial class AppSharePreview
{
    /// <summary>
    /// bit Fabric's card, 1200 by 630 as Open Graph asks: its mark, its tagline and the five platforms. Server.Web serves it,
    /// so neither the offline cache nor the native apps carry it. Its version changes with the picture, as networks keep a
    /// preview by its address.
    /// </summary>
    private const string ImagePath = "images/og/bit-fabric.png?v=1";

    [AutoInject] private NavigationManager navigationManager = default!;
    [AutoInject] private IStringLocalizer<AppStrings> localizer = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter, EditorRequired] public string Description { get; set; } = default!;

    /// <summary>Absolute, as the networks read it: the page's own canonical url, culture kept, query dropped.</summary>
    private string Url => new Uri(navigationManager.Uri).GetCanonicalUrl();

    private string Image => new Uri(new Uri(navigationManager.BaseUri), ImagePath).ToString();

    private string ImageAlt => localizer[nameof(AppStrings.AppSharePreviewImageAlt)];

    /// <summary>Open Graph writes a locale with an underscore: fa_IR, en_US.</summary>
    private static string OpenGraphLocale(CultureInfo culture) => culture.Name.Replace('-', '_');
}
