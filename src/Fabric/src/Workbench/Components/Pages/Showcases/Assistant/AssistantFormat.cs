namespace Bit.Fabric.Workbench.Components.Pages.Showcases.Assistant;

/// <summary>Formats the assistant's cards' money, counts and percentages in the current culture, with the euro as currency.</summary>
public static class AssistantFormat
{
    /// <summary>An amount in euros, in the culture's number format with the given decimals.</summary>
    public static string Money(decimal value, int decimals = 2)
    {
        var format = (NumberFormatInfo)CultureInfo.CurrentCulture.NumberFormat.Clone();
        format.CurrencySymbol = "€";
        format.CurrencyDecimalDigits = decimals;

        return value.ToString("C", format);
    }

    public static string Number(decimal value) => value.ToString("N0", CultureInfo.CurrentCulture);

    /// <summary>A percentage with a plus sign for growth, as a trend's change shows it.</summary>
    public static string SignedPercent(decimal value) => $"{(value >= 0 ? "+" : string.Empty)}{value.ToString("P1", CultureInfo.CurrentCulture)}";
}
