namespace Boilerplate.Workbench.Components.Pages.Explainers.Payments.Data;

/// <summary>
/// What the payments explainer says: each provider's and each store's published fees and rules, as read from their own
/// pages on 25 November 2026 (Sources). Standard published prices only; a contract, a country or a volume changes them.
/// </summary>
public static class PaymentsFacts
{
    public const string ReadOn = "25 November 2026";

    public static PaymentsRoute[] Routes { get; } =
    [
        new(BitIconName.Website, "On your website or web app", "Any payment provider you like. The stores have no say in it.",
            "About 1.5% to 3.5% plus a fixed fee per sale, or 5% + 50¢ with a merchant of record that also handles VAT."),
        new(BitIconName.CellPhone, "Digital goods in your Android app", "Google Play Billing, unless your country lets you bill on your own or link to your website.",
            "10% to 30% of each sale, by country, by your yearly earnings and by how you bill."),
        new(BitIconName.MobileSelected, "Digital goods in your iOS app", "Apple's In-App Purchase, unless your country lets you use another provider or link to your website.",
            "15% to 30% of each sale, and less where you may link out."),
        new(BitIconName.ShoppingCart, "Physical goods and services", "Any payment provider, in any app: both stores forbid their own billing for them.",
            "The stores take nothing; your provider's fee is all you pay.")
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
        new("Stripe", "Payment processor", false,
            "1.5% + €0.25 for standard EEA cards, 2.8% + €0.25 for premium ones, 3.15% + €0.25 from outside the EEA; iDEAL €0.29",
            "2.9% + 30¢ for domestic cards",
            "Stripe Billing: 0.7% of what it bills",
            "Yours to collect and file; Stripe Tax helps, from 0.5% per sale",
            "Most apps: one API for cards, wallets and local methods in many countries"),
        new("Mollie", "Payment processor", false,
            "1.80% + €0.25 for EEA consumer cards, 2.90% + €0.25 for business cards; iDEAL | Wero €0.32",
            "Not its market: Mollie is built for European businesses",
            "Recurring payments through its API",
            "Yours to collect and file",
            "A European business that wants iDEAL and local methods, with no monthly fee"),
        new("Adyen", "Payment processor", false,
            "$0.13 + interchange++ + 0.60% for Visa and Mastercard; iDEAL $0.13 + €0.22",
            "$0.13 + interchange++ + 0.60% for Visa and Mastercard",
            "Stored cards for recurring payments",
            "Yours to collect and file",
            "Large volumes across many countries; a minimum monthly invoice may apply"),
        new("PayPal", "Payment processor and wallet", false,
            "3.40% + €0.35 for domestic sales in the Netherlands, none extra from the EEA",
            "3.49% + $0.49 for PayPal Checkout, 2.99% + $0.49 for cards",
            "Recurring payments with PayPal subscriptions",
            "Yours to collect and file",
            "Buyers who would rather pay with their PayPal account"),
        new("Paddle", "Merchant of record", true,
            "5% + 50¢ per sale, everything included",
            "5% + 50¢ per sale, everything included",
            "Included",
            "Paddle is the seller: it collects and files VAT and sales tax",
            "Selling software worldwide without registering for taxes in each country"),
        new("Stripe Managed Payments", "Merchant of record", true,
            "3.5% on top of Stripe's card fee",
            "3.5% on top of Stripe's card fee",
            "Billing's 0.7% on top",
            "Stripe is the seller of record and handles them",
            "Staying on Stripe with taxes handled; works with Checkout and Payment Links only")
    ];

    public static PaymentsRegion[] Regions { get; } =
    [
        new("eu", "The EU", "The European Economic Area. Apple's terms since 1 October 2026, Google's fees since 30 June 2026.",
        [
            new("The store's own billing",
                "10% on your first $1M a year and on subscriptions, 20% above that (25% for users who installed before 30 June 2026), plus a 5% billing fee.",
                "In-App Purchase: 26%, or 15% in the Small Business Program and for subscriptions after their first year."),
            new("Your own payment inside the app",
                "Allowed: the same service fee, without the 5% billing fee.",
                "Allowed with another payment provider: 20%, or 10% for small businesses and subscriptions after year one."),
            new("A link to pay on your website",
                "Allowed through external offers: 10% on subscriptions and on your first $1M, 20% on other purchases.",
                "Allowed: 15% of sales made within 7 days of the tap, or 10% for small businesses."),
            new("Physical goods and services",
                "Must not use Play Billing. Any provider; Google takes nothing.",
                "Must not use In-App Purchase. Any provider; Apple takes nothing.")
        ]),
        new("us", "The US", "Google's fees since 30 June 2026. Apple's rules for links follow a court order of April 2025.",
        [
            new("The store's own billing",
                "10% on your first $1M a year and on subscriptions, 20% above that (25% for earlier installs), plus a 5% billing fee.",
                "In-App Purchase: 30%, or 15% in the Small Business Program and for subscriptions after their first year."),
            new("Your own payment inside the app",
                "Allowed: the same service fee, without the 5% billing fee.",
                "Not allowed; a button or a link to your website is."),
            new("A link to pay on your website",
                "Allowed: 10% on your first $1M and on subscriptions, 20% otherwise, on purchases within 24 hours of the tap.",
                "Allowed, buttons included, with no commission for now. Apple asks to charge one; the Supreme Court hears the case on 11 January 2027."),
            new("Physical goods and services",
                "Must not use Play Billing. Any provider; Google takes nothing.",
                "Must not use In-App Purchase. Any provider; Apple takes nothing.")
        ]),
        new("elsewhere", "Most other countries", "Outside the EU, the UK, the US, Australia and Japan, which have rules or fees of their own.",
        [
            new("The store's own billing",
                "15% on your first $1M a year and on subscriptions, 30% above. The lower fees reach South Korea, Turkey and South Africa on 31 December 2026, and the rest by 30 September 2027.",
                "In-App Purchase: 30%, or 15% in the Small Business Program and for subscriptions after their first year."),
            new("Your own payment inside the app",
                "Only where Google offers User Choice Billing next to its own, which lowers the fee by 4 points.",
                "Not allowed, apart from countries with laws of their own, such as South Korea."),
            new("A link to pay on your website",
                "Not offered in most countries.",
                "Reader apps only, such as for books, music or video, with Apple's entitlement for it."),
            new("Physical goods and services",
                "Must not use Play Billing. Any provider; Google takes nothing.",
                "Must not use In-App Purchase. Any provider; Apple takes nothing.")
        ])
    ];

    public static PaymentsPoint[] Points { get; } =
    [
        new(BitIconName.Invoice, "Who files the taxes",
            "With a payment processor you are the seller, so VAT and sales tax are yours to collect and file. A merchant of record is the seller instead, and does it for you, for a higher fee."),
        new(BitIconName.RecurringTask, "Where a subscription is managed",
            "One bought in the store is changed, cancelled and refunded in the store; one bought on your website, on your website. Keep both on your server, so the app unlocks the same thing either way."),
        new(BitIconName.Warning, "Lemon Squeezy is closing",
            "It stopped taking new sellers on 16 November 2026 and shuts down on 31 March 2027; its sellers move to Stripe Managed Payments. Don't start on it."),
        new(BitIconName.History, "The rules keep moving",
            "Apple's US case reaches the Supreme Court on 11 January 2027, and Google's lower fees reach more countries on 31 December 2026 and 30 September 2027. Read each source again before you decide.")
    ];

    public static ExplainerSource[] Sources { get; } =
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
