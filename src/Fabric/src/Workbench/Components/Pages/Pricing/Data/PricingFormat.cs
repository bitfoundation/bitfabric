using System.Globalization;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// How the pricing pages write amounts: euros with Latin digits and a comma between thousands in every language, since the
/// page is in English for now, and revenue bands the short way founders say them (€100K, €1M).
/// </summary>
public static class PricingFormat
{
    /// <summary>An amount, such as €3,990, or €0.50 when it has cents.</summary>
    public static string Euros(decimal euros) => "€" + euros.ToString(euros == decimal.Truncate(euros) ? "N0" : "N2", CultureInfo.InvariantCulture);

    /// <summary>A revenue band's edge, such as €250K or €10M.</summary>
    public static string Revenue(decimal euros) => euros switch
    {
        >= 1_000_000 => "€" + (euros / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => "€" + (euros / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "K",
        _ => Euros(euros)
    };

    /// <summary>How many months a yearly price saves against twelve monthly ones, when it is a whole number.</summary>
    public static int? MonthsOff(PricingAmount monthly, PricingAmount? yearly)
    {
        if (yearly is null || monthly.Euros <= 0)
            return null;

        var months = 12 - yearly.Euros / monthly.Euros;

        return months > 0 && months == decimal.Truncate(months) ? (int)months : null;
    }
}
