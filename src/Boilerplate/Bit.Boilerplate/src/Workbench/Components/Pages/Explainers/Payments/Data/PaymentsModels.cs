namespace Boilerplate.Workbench.Components.Pages.Explainers.Payments.Data;

/// <summary>Where the money comes from, and what that means: one card of the short answer.</summary>
public sealed record PaymentsRoute(string IconName, string Title, string Rule, string Cost);

/// <summary>
/// A row of the calculator: what one sale costs with a provider, in percent and a fixed amount in euros, and the percent a
/// subscription adds, with the case the figures are for. <paramref name="InSentence"/> is its name in a sentence, where it differs.
/// </summary>
public sealed record PaymentsFee(string Name, string Basis, bool IsStore, decimal Percent, decimal Fixed, decimal SubscriptionPercent = 0, string? InSentence = null)
{
    public decimal FeeOf(decimal price, bool isSubscription) => Math.Round(price * (Percent + (isSubscription ? SubscriptionPercent : 0)) / 100 + Fixed, 2, MidpointRounding.AwayFromZero);
}

/// <summary>A web payment provider, with the same rows on each so they compare at a glance.</summary>
public sealed record PaymentsProvider(string Name, string Kind, bool IsMerchantOfRecord, string EuCards, string UsCards, string Subscriptions, string Taxes, string GoodFor);

/// <summary>What the stores allow and take in a part of the world, one topic per row.</summary>
public sealed record PaymentsRegion(string Key, string Name, string Covers, PaymentsStoreRule[] Rules);

public sealed record PaymentsStoreRule(string Topic, string GooglePlay, string AppStore);

/// <summary>A point to keep in mind before deciding.</summary>
public sealed record PaymentsPoint(string IconName, string Title, string Text);
