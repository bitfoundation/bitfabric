namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>
/// The cards of one grid. While one card's "More info" is open, every card keeps the height it had, so the closed ones beside
/// it stay even rather than shrinking to their content or stretching to the open one.
/// </summary>
public sealed class AppHomeCardGroup
{
    private readonly List<AppHomeFeatureCard> cards = [];

    public void Add(AppHomeFeatureCard card) => cards.Add(card);

    public void Remove(AppHomeFeatureCard card) => cards.Remove(card);

    public async Task Hold()
    {
        foreach (var card in cards.ToArray())
        {
            var box = await card.Element.GetBoundingClientRect();
            await card.Element.SetStyleProperty("min-height", $"{box.Height.ToString(CultureInfo.InvariantCulture)}px");
        }
    }

    public async Task Release()
    {
        foreach (var card in cards.ToArray())
        {
            await card.Element.RemoveStyleProperty("min-height");
        }
    }
}
