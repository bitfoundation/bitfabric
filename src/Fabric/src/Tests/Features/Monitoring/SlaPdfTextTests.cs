using Bit.Fabric.Server.Api.Features.Monitoring.Services;

namespace Bit.Fabric.Tests.Features.Monitoring;

/// <summary>
/// The SLA report's PDF draws every language the app speaks with the fonts it brings (Features/Monitoring/Fonts), since
/// the server's image has none of its own: what a server's font would have drawn shows there as empty boxes.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class SlaPdfTextTests
{
    [TestMethod]
    public void TheReport_Should_DrawEveryLanguageOfTheApp_WithItsOwnFonts()
    {
        foreach (var (displayName, culture) in CultureInfoManager.SupportedCultures)
        {
            // The language's own name, a month and a percentage as the report prints them in it.
            var line = $"{displayName} · {new DateTime(2027, 3, 1).ToString("MMMM yyyy", culture)} · {99.95.ToString("0.00", culture)}{culture.NumberFormat.PercentSymbol}";

            using var fonts = new SlaPdfText(culture);

            Assert.IsTrue(fonts.DrawsWithItsOwnFonts(line), $"{culture.Name}: {line}");
            Assert.IsTrue(fonts.DrawsWithItsOwnFonts(line, bold: true), $"{culture.Name}, bold: {line}");
        }
    }
}
