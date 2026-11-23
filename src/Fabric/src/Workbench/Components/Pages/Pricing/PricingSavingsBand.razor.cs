using System.Globalization;
using Bit.Fabric.Workbench.Components.Pages.Pricing.Data;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing;

/// <summary>
/// The same month on a hosted builder and with bit Fabric (<see cref="PricingSavings"/>), in one of four designs: row by row,
/// where both cards have the same rows and bit Fabric's has one more, for its license; the bills the customer pays and to whom;
/// one table; or two bars on one scale, where the builder's share is a color of its own and what the customer keeps is the
/// empty end of bit Fabric's bar.
/// </summary>
public partial class PricingSavingsBand
{
    /// <summary>One of <see cref="PricingSavings"/>'s designs; row by row when none is picked.</summary>
    [Parameter] public string? View { get; set; }

    [Parameter] public string HeadingElement { get; set; } = "h3";

    private PricingSavingBar builder = default!;
    private PricingSavingBar fabric = default!;
    private PricingCostLine[] lines = [];

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        builder = PricingSavings.Builder(Localizer);
        fabric = PricingSavings.Fabric(Localizer);
        lines = PricingSavings.Lines(Localizer);
    }

    private decimal Kept => fabric.Parts.Where(part => part.Kind is PricingSavingKind.Kept).Sum(part => part.Euros);

    private string KeptText => Localizer["You keep {0} a month", Euros(Kept)];

    private decimal BuilderLinesTotal => lines.Sum(line => line.BuilderEuros ?? 0);

    private decimal FabricLinesTotal => lines.Sum(line => line.FabricEuros);

    /// <summary>A builder's row: its amount, or "In its price" for the work it doesn't price on its own.</summary>
    private string BuilderAmount(PricingCostLine line) => line.BuilderEuros is { } euros ? Euros(euros) : Localizer["In its price"];

    /// <summary>Both bars share one scale: the longer of the two, counting what is kept.</summary>
    private decimal Scale => Math.Max(builder.Parts.Sum(part => part.Euros), fabric.Parts.Sum(part => part.Euros));

    private string Width(PricingSavingPart part)
        => (Scale <= 0 ? 0 : part.Euros / Scale * 100).ToString("0.##", CultureInfo.InvariantCulture) + "%";

    private static string KindClass(PricingSavingKind kind) => kind switch
    {
        PricingSavingKind.Ai => "is-ai",
        PricingSavingKind.Hosting => "is-hosting",
        PricingSavingKind.Build => "is-build",
        PricingSavingKind.Share => "is-share",
        PricingSavingKind.License => "is-license",
        _ => "is-kept"
    };

    /// <summary>The bar read out as words, for a screen reader.</summary>
    private string Describe(PricingSavingBar bar)
        => $"{bar.Name}: " + string.Join(", ", bar.Parts.Select(part => $"{part.Label} {Euros(part.Euros)}"));
}
