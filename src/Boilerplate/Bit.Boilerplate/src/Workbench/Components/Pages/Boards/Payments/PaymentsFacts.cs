namespace Boilerplate.Workbench.Components.Pages.Boards.Payments;

/// <summary>
/// What the payments board shows: each provider's and each store's published fees and rules, as read from their own pages
/// on 25 November 2026 (Sources), each cut down to a figure and a few words. Standard published prices only; a contract,
/// a country or a volume changes them.
/// </summary>
public static class PaymentsFacts
{
    public const string ReadOn = "25 November 2026";

    public static PaymentsRoute[] Routes { get; } =
    [
        new(BitIconName.Website, "On your website", "1.5–5%", "Any provider, plus a small fixed fee", 1.5m, 5m),
        new(BitIconName.CellPhone, "In your Android app", "10–30%", "Google Play, for digital goods", 10m, 30m),
        new(BitIconName.MobileSelected, "In your iOS app", "15–30%", "The App Store, less where you may link out", 15m, 30m),
        new(BitIconName.ShoppingCart, "Physical goods", "0%", "To the stores, in any app", 0m, 0m)
    ];

    // The calculator's case: a small business (under $1M a year) selling to a customer in the EU with a standard card.
    public static PaymentsFee[] Fees { get; } =
    [
        new("Stripe", "Standard EEA card; Billing adds 0.7% for a subscription", false, 1.5m, 0.25m, 0.7m),
        new("Mollie", "EEA consumer card", false, 1.8m, 0.25m),
        new("PayPal", "Domestic sale in the Netherlands", false, 3.4m, 0.35m),
        new("Stripe Managed Payments", "Merchant of record: 3.5% on top of the card fee", false, 5.0m, 0.25m, 0.7m),
        new("Paddle", "Merchant of record, VAT handled; its 50¢ counted as €0.50", false, 5.0m, 0.50m),
        new("Google Play", "EEA, Play Billing: 10% service fee and 5% billing fee", true, 15m, 0m),
        new("App Store", "EU, In-App Purchase, Small Business Program", true, 15m, 0m, InSentence: "the App Store")
    ];

    public static PaymentsProvider[] Providers { get; } =
    [
        new("Stripe", "1.5% + €0.25", "Standard EEA card", false, ["Subscriptions +0.7%", "iDEAL €0.29", "US cards"], "Most apps"),
        new("Mollie", "1.8% + €0.25", "EEA consumer card", false, ["Subscriptions", "iDEAL | Wero €0.32", "No monthly fee"], "European businesses"),
        new("Adyen", "IC++ + 0.6% + $0.13", "Visa and Mastercard", false, ["Stored cards", "iDEAL", "Many countries"], "Large volumes"),
        new("PayPal", "3.4% + €0.35", "Domestic sale, Netherlands", false, ["PayPal wallet", "Subscriptions", "US cards"], "Buyers who prefer PayPal"),
        new("Paddle", "5% + 50¢", "Everything included", true, ["Files VAT and sales tax", "Subscriptions", "Worldwide"], "Software sold worldwide"),
        new("Stripe Managed Payments", "+3.5%", "On top of Stripe's card fee", true, ["Files VAT and sales tax", "Checkout and Payment Links"], "Staying on Stripe")
    ];

    public static PaymentsRegion[] Regions { get; } =
    [
        new("eu", "EU", "Apple's terms of 1 October 2026, Google's fees of 30 June 2026",
        [
            new(BitIconName.ShoppingCart, "The store's billing",
                new(PaymentsAllowed.Yes, "10–25%", "+5% billing fee"),
                new(PaymentsAllowed.Yes, "15–26%", "15% for small businesses")),
            new(BitIconName.PaymentCard, "Your own billing, in the app",
                new(PaymentsAllowed.Yes, "10–25%", "No billing fee"),
                new(PaymentsAllowed.Yes, "10–20%", "With another provider")),
            new(BitIconName.Link, "A link to your website",
                new(PaymentsAllowed.Yes, "10–20%", "Through external offers"),
                new(PaymentsAllowed.Yes, "10–15%", "On sales within 7 days"))
        ]),
        new("us", "US", "Google's fees of 30 June 2026, Apple's links by a court order of April 2025",
        [
            new(BitIconName.ShoppingCart, "The store's billing",
                new(PaymentsAllowed.Yes, "10–25%", "+5% billing fee"),
                new(PaymentsAllowed.Yes, "15–30%", "15% for small businesses")),
            new(BitIconName.PaymentCard, "Your own billing, in the app",
                new(PaymentsAllowed.Yes, "10–25%", "No billing fee"),
                new(PaymentsAllowed.No, "Not allowed", "A link out is")),
            new(BitIconName.Link, "A link to your website",
                new(PaymentsAllowed.Yes, "10–20%", "On sales within 24 hours"),
                new(PaymentsAllowed.Yes, "0%", "For now: in court on 11 January 2027"))
        ]),
        new("elsewhere", "Elsewhere", "Outside the EU, the UK, the US, Australia and Japan",
        [
            new(BitIconName.ShoppingCart, "The store's billing",
                new(PaymentsAllowed.Yes, "15–30%", "Lower by 30 September 2027"),
                new(PaymentsAllowed.Yes, "15–30%", "15% for small businesses")),
            new(BitIconName.PaymentCard, "Your own billing, in the app",
                new(PaymentsAllowed.Limited, "−4 points", "With User Choice Billing"),
                new(PaymentsAllowed.No, "Not allowed", "Unless a law allows it")),
            new(BitIconName.Link, "A link to your website",
                new(PaymentsAllowed.No, "Not offered", "In most countries"),
                new(PaymentsAllowed.Limited, "Reader apps", "Books, music, video"))
        ])
    ];

    public static PaymentsPoint[] Points { get; } =
    [
        new(BitIconName.Invoice, "A merchant of record files the VAT for you"),
        new(BitIconName.RecurringTask, "A store subscription is managed in the store"),
        new(BitIconName.Warning, "Lemon Squeezy closes on 31 March 2027"),
        new(BitIconName.History, "The rules move: check each source before you decide")
    ];

    public static BoardSource[] Sources { get; } =
    [
        new("Stripe: pricing (Netherlands)", "https://stripe.com/en-nl/pricing", ReadOn),
        new("Stripe: Managed Payments pricing", "https://support.stripe.com/questions/managed-payments-pricing", ReadOn),
        new("Mollie: pricing", "https://www.mollie.com/nl/pricing", ReadOn),
        new("Adyen: pricing", "https://www.adyen.com/pricing", ReadOn),
        new("PayPal: merchant fees, Netherlands (PDF)", "https://www.paypalobjects.com/marketing/ua/pdf/NL/nl/nl-nl-merchant-fees-04-nov-2026.pdf", ReadOn),
        new("Paddle: pricing", "https://www.paddle.com/pricing", ReadOn),
        new("Lemon Squeezy: saying goodbye", "https://www.lemonsqueezy.com/blog/saying-goodbye-to-lemon-squeezy", ReadOn),
        new("Apple: App Review Guidelines", "https://developer.apple.com/app-store/review/guidelines/", ReadOn),
        new("Apple: apps in the EU", "https://developer.apple.com/support/dma-and-apps-in-the-eu/", ReadOn),
        new("Apple: Small Business Program", "https://developer.apple.com/app-store/small-business-program/", ReadOn),
        new("Google Play: service fees", "https://support.google.com/googleplay/android-developer/answer/112622?hl=en", ReadOn),
        new("Google Play: programs in the US", "https://support.google.com/googleplay/android-developer/answer/15582165?hl=en", ReadOn),
        new("Google Play: programs in the EEA", "https://support.google.com/googleplay/android-developer/answer/12348241?hl=en", ReadOn)
    ];
}
