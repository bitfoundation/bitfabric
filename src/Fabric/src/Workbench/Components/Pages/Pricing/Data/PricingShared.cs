using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// What every strategy's page says the same way: what stays true whatever the customer pays, what they pay others (the home
/// page's calculator and bitfabric-internal/specs/product.md, "The customer's real cost"), what comes with every license once
/// the app is live, what a project is, and the questions they share.
/// </summary>
public static class PricingShared
{
    // What the customer pays others, from the home page's calculator (AppHomeCostCalculator): this site's own bills. The server
    // is Contabo's Cloud VPS 4, 4 cores and 8 GB, €4.40 a month with VAT on a 24-month term (contabo.com/en/vps, read
    // 2026-11-20), the kind bitfabric.co runs on. Contabo is named plainly, with no referral link.
    public const decimal ServerFrom = PricingSavings.OwnServer;
    public const decimal DomainAMonth = 2;
    public const decimal AppleAYear = 99;
    public const decimal ThisSiteAMonth = 11;

    // Watching the live app comes with every license; SMS alerts are the one extra, since each text message costs money to
    // send.
    public const decimal SmsPack = 10;
    public const int SmsPackSize = 100;

    /// <summary>The refund window, a draft; set once here.</summary>
    public const int RefundDays = 30;

    public static PricingTile[] Truths(IStringLocalizer l) =>
    [
        new(BitIconName.Code, l["Your code, data and server are yours"], l["The code is in your repository, your users are in your own database, on your own server, and the keys stay with you."]),
        new(BitIconName.Lock, l["We never see your code, data or prompts"], l["Your AI builds on your computer. You choose which AI sees your code, and on what terms."]),
        new(BitIconName.Robot, l["No credits from us"], l["You bring your own AI plan. We never sell credits or count your prompts."]),
        new(BitIconName.Server, l["Your app keeps working"], l["If you stop paying, or if we disappear, it keeps running on your server, unchanged."])
    ];

    public static PricingTile[] Others(IStringLocalizer l) =>
    [
        new(BitIconName.Robot, l["Your AI plan"], l["The plan you already have with the AI you chose, or a model on your own computer."], l["Your choice"]),
        new(BitIconName.Server, l["A server"], l["Straight from a host you choose: Contabo's smallest has 4 cores and 8 GB of memory, and runs a whole site like this one."], l["From {0} a month", Euros(ServerFrom)]),
        new(BitIconName.Globe, l["A domain"], l["Your app's address, renewed once a year."], l["About {0} a month", Euros(DomainAMonth)]),
        new(BitIconName.CellPhone, l["App stores"], l["Only if your app is in them. Google Play is a single payment."], l["About {0} a year for Apple", Euros(AppleAYear)])
    ];

    /// <summary>The note under what the customer pays others: this site's own bill, as proof.</summary>
    public static string OthersNote(IStringLocalizer l) => l["bitfabric.co itself runs on {0} a month, with every optional item on.", Euros(ThisSiteAMonth)];

    /// <summary>The line above the plans of the options priced by projects.</summary>
    public static string ProjectBasis(IStringLocalizer l)
        => l["A project is one app: its website, its apps for every platform and its server. Prices in euros, excluding VAT."];

    /// <summary>The "Once your app is live" row's first cell: what every license includes.</summary>
    public static PricingTile CloudIncluded(IStringLocalizer l)
        => new(BitIconName.Health, l["Included"], l["Health checks, monitoring, security scans, SLA reports and a status page."], Euros(0));

    public static PricingTile Sms(IStringLocalizer l)
        => new(BitIconName.Message, l["SMS alerts"], l["A text message to your phone when something breaks."], l["{0} per {1}", Euros(SmsPack), SmsPackSize]);

    /// <summary>
    /// What a project is: what bit CLI makes with one command. How to say it so nobody reads it as one platform, one environment
    /// or one customer is still open (bitfabric-internal/specs/product.md); this is one wording to argue with.
    /// </summary>
    public static PricingQuestion WhatIsAProject(IStringLocalizer l)
        => new(l["What counts as a project?"], l["One app, as bit Fabric makes it with one command: its website, its Android, iOS, Windows and macOS apps and its server, with every environment and every customer it serves. A second product is a second project."]);

    /// <summary>Which costs less, said plainly: never as if building took months.</summary>
    public static PricingQuestion Cheaper(IStringLocalizer l)
        => new(l["Is a hosted builder ever cheaper?"], l["Yes, for a landing page or a small, simple app. For a serious product, one whose future matters to you, bit Fabric costs far less: your AI plan and your server come straight from their makers, your tests run on the computer you already have, and the license is {0} or {1} a month.", Euros(ProjectPricing.TeamMonthly), Euros(ProjectPricing.CompanyMonthly)]);

    public static PricingQuestion StopPaying(IStringLocalizer l)
        => new(l["What happens to my app if I stop paying?"], l["It keeps running on your server and in the app stores, unchanged. Renew when you want to build on it again."]);

    public static PricingQuestion ForLife(IStringLocalizer l)
        => new(l["Is there a license for life?"], l["Yes. Pay about two years of your plan once, {0} or {1}, and it never runs out.", Euros(MemberPricing.FoundingOnce), Euros(MemberPricing.FoundingCompanyOnce)]);

    public static PricingQuestion Invoice(IStringLocalizer l)
        => new(l["Can I get an invoice with my VAT number?"], l["Yes. Prices here exclude VAT, and every invoice from our company in the Netherlands carries your company's name and VAT number."]);

    public static PricingQuestion Refund(IStringLocalizer l)
        => new(l["Is there a refund?"], l["Yes. Ask within {0} days of a payment and you get all of it back.", RefundDays]);

    public static PricingQuestion AfterBeta(IStringLocalizer l)
        => new(l["What changes when the beta ends?"], l["During the beta nobody pays. When it ends, these plans start."]);
}
