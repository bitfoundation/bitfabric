using Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing;

/// <summary>The other way to pay that a strategy offers next to its plans, with its numbers worked out in a small table.</summary>
public partial class PricingTable
{
    [Parameter, EditorRequired] public PricingAlternative Alternative { get; set; } = default!;

    [Parameter] public string HeadingElement { get; set; } = "h3";
}
