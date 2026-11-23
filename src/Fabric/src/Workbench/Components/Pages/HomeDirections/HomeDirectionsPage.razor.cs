using Bit.Fabric.Client.Core.Components.Pages.Home;

namespace Bit.Fabric.Workbench.Components.Pages.HomeDirections;

/// <summary>
/// Competing directions for bitfabric.co's home page (2026-11-19), the page's design options: trust first for a careful
/// European buyer, answer first for answer engines and search results, Concept 2 (production-ready, to the euro) and Concept 3
/// (nothing to promise) of bitfabric-internal/specs/changes/2026-11-18-home-page-concepts.md. Each is a whole page built from
/// the same blocks (DirectionHero, DirectionBand, DirectionTiles, DirectionTable, ...). It inherits SitePageHost so "Get
/// started" and the links to /features open the Workbench's own pages for them, as on the app's home page here.
/// </summary>
public partial class HomeDirectionsPage
{
    public const string Key = "home-directions";

    public const string TrustKey = "trust";

    public const string AnswersKey = "answers";

    public const string EuroKey = "euro";

    public const string PromiseKey = "promise";

    /// <summary>The feature whose film is playing in the dialog, if any.</summary>
    private AppHomeFeature? playing;

    private string Direction => WorkbenchPageCatalog.Find(Key)?.ResolveVariant(Scenario)?.Key ?? TrustKey;

    private void Explain(AppHomeFeature feature) => playing = feature;

    private void CloseFilm() => playing = null;
}
