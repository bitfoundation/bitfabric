namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// Where the buyer pays, and the share of a sale it costs: <paramref name="Low"/> to <paramref name="High"/> percent,
/// drawn on a scale of 0 to 30%.
/// </summary>
public sealed record PaymentsRoute(string IconName, string Where, string Share, string Caption, decimal Low, decimal High);

/// <summary>
/// A row of the calculator: what one sale costs with a provider, in percent and a fixed amount in euros, and the percent a
/// subscription adds, with the case the figures are for. <paramref name="InSentence"/> is its name in a sentence, where it differs.
/// </summary>
public sealed record PaymentsFee(string Name, string Basis, bool IsStore, decimal Percent, decimal Fixed, decimal SubscriptionPercent = 0, string? InSentence = null)
{
    public decimal FeeOf(decimal price, bool isSubscription) => Math.Round(price * (Percent + (isSubscription ? SubscriptionPercent : 0)) / 100 + Fixed, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// A web payment provider at a glance: what an EU card sale costs, whether it is the seller and so files the taxes, what
/// it adds, and who it suits, in a few words each.
/// </summary>
public sealed record PaymentsProvider(string Name, string Fee, string FeeCase, bool IsMerchantOfRecord, string[] Gives, string GoodFor);

public enum PaymentsAllowed
{
    Yes,
    Limited,
    No
}

/// <summary>What a store allows and takes for one way of paying, in a figure and a few words.</summary>
public sealed record PaymentsTerm(PaymentsAllowed Allowed, string Share, string Note);

/// <summary>A way a buyer of digital goods can pay inside an app, with Google Play's and the App Store's terms for it.</summary>
public sealed record PaymentsWay(string IconName, string Name, PaymentsTerm GooglePlay, PaymentsTerm AppStore);

/// <summary>A part of the world, and since when its terms hold.</summary>
public sealed record PaymentsRegion(string Key, string Name, string Since, PaymentsWay[] Ways);

/// <summary>Something to keep in mind before deciding, in a line.</summary>
public sealed record PaymentsPoint(string IconName, string Text);
