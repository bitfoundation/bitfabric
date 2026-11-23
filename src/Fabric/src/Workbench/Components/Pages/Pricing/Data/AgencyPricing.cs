using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// Option 3, "A license for agencies". Option 1's Company license covers one company's own projects, and an agency's projects
/// belong to its clients, so an agency gets a license of its own that covers every client project while it is being built; at
/// handover the client takes its own license, on option 1's rules. Agencies and freelancers are not the main customer, so this
/// is a side door, not the page's lead. No referral share. Prices in euros excluding VAT: they follow option 1's numbers.
/// </summary>
public static class AgencyPricing
{
    public const string Key = "agencies";

    public static PricingStrategy Build(IStringLocalizer l) => new(Key,
        l["Build for clients on an agency license. Hand over theirs."],
        l["Your agency license covers every client project while you build it. At handover your client takes its own license: free if it can't pay, {0} a month for up to {1} projects, or {2} for all of them.", Euros(ProjectPricing.TeamMonthly), ProjectPricing.TeamProjects, Euros(ProjectPricing.CompanyMonthly)])
    {
        Basis = l["Yours while you build, your client's after handover. Prices in euros, excluding VAT."],
        Plans =
        [
            new("agency", l["Agency"], l["Every client project while you build it"], new(ProjectPricing.CompanyMonthly, PricingPeriod.Month))
            {
                YearlyPrice = new(ProjectPricing.CompanyYearly, PricingPeriod.Year),
                Includes = [l["No limit on client projects"], l["Your agency's own projects too"], l["Status pages and SLA reports for your clients"]],
                Highlight = l["For agencies"],
                ActionText = l["Choose Agency"]
            },
            new("client", l["Your client's license"], l["From handover"], new(0, PricingPeriod.None))
            {
                PriceNote = l["Or {0} or {1} a month, by its number of projects", Euros(ProjectPricing.TeamMonthly), Euros(ProjectPricing.CompanyMonthly)],
                Includes = [l["Moves with the app at handover"], l["Your client keeps developing under it"]],
                Action = PricingAction.Start,
                ActionText = l["Get started"]
            }
        ],
        UnderPlans = new(BitIconName.Contact, l["A freelancer with one or two clients at a time?"], l["Team, at {0} a month, covers up to {1} projects, client projects included.", Euros(ProjectPricing.TeamMonthly), ProjectPricing.TeamProjects]),
        Extra = (l["At handover, the app becomes your client's"], l["Your client gets the code, the server and the license."],
        [
            new(BitIconName.Certificate, l["The license moves to the client"], l["bit CLI moves the license file with the project, and your client keeps developing under it."]),
            new(BitIconName.Heart, l["The app keeps running"], l["Whatever happens to your license, or to us, the app they paid for keeps running on their server."])
        ]),
        CloudIncluded = PricingShared.CloudIncluded(l),
        Questions =
        [
            new(l["Does my client need a license?"], l["Yes, from handover, on the same rules as everyone: free if it can't pay, otherwise {0} or {1} a month.", Euros(ProjectPricing.TeamMonthly), Euros(ProjectPricing.CompanyMonthly)]),
            new(l["What happens to my clients' apps if I stop paying?"], l["The apps you handed over are your clients', under their own licenses, and keep running on their servers."]),
            new(l["When is a project a client project?"], l["When you build it for someone else to own and hand it over. What you build for your own agency is covered too."]),
            PricingShared.WhatIsAProject(l),
            PricingShared.Invoice(l),
            PricingShared.Refund(l),
            PricingShared.AfterBeta(l)
        ]
    };
}
