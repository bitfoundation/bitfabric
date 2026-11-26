namespace Bit.Fabric.Client.Core.Components.Pages.Management.Growth;

/// <summary>A figure's change against the period before, with an arrow and a word for screen readers, so its color never stands alone.</summary>
public partial class GrowthDeltaChip
{
    [Parameter] public GrowthDelta? Delta { get; set; }
}
