using Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing;

/// <summary>The tiles of a band, such as what stays true or what the customer pays others; as a row, the cloud add-on's cells.</summary>
public partial class PricingTiles
{
    [Parameter] public PricingTile[] Tiles { get; set; } = [];

    [Parameter] public string HeadingElement { get; set; } = "h4";

    /// <summary>Side by side in one bordered card, divided by lines, rather than a grid of loose tiles.</summary>
    [Parameter] public bool IsRow { get; set; }
}
