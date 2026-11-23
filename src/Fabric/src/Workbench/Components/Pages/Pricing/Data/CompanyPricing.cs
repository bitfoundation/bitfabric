using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// Option 2, "People build free, companies pay": a person building on their own never pays; the company that builds and owns
/// the app does, by how many projects it has, at option 1's prices. The companies that only use an app built with bit Fabric
/// never need a license. Prices in euros excluding VAT: they follow option 1's numbers.
/// </summary>
public static class CompanyPricing
{
    public const string Key = "companies";

    public static PricingStrategy Build(IStringLocalizer l) => new(Key,
        l["People build free. Companies pay."],
        l["Building on your own costs nothing. The company that builds and owns the app pays: {0} a month for up to {1} projects, or {2} a month for all of them.", Euros(ProjectPricing.TeamMonthly), ProjectPricing.TeamProjects, Euros(ProjectPricing.CompanyMonthly)])
    {
        Basis = l["The company that builds and owns the app pays, never the people or companies who use it. Prices in euros, excluding VAT."],
        Plans =
        [
            new("person", l["Person"], l["Building on your own, with no company"], new(0, PricingPeriod.None))
            {
                Includes = [l["Up to {0} projects", ProjectPricing.TeamProjects], l["Everything a company gets"], l["Yours for as long as you build on your own"]],
                Action = PricingAction.Start,
                ActionText = l["Get started"]
            },
            new("team", l["Small company"], l["Up to {0} projects", ProjectPricing.TeamProjects], new(ProjectPricing.TeamMonthly, PricingPeriod.Month))
            {
                YearlyPrice = new(ProjectPricing.TeamYearly, PricingPeriod.Year),
                Includes = [l["Every upgrade, applied by your AI"], l["The skills and libraries to keep building"], l["Health checks, monitoring, a status page and scans"]],
                Highlight = l["Recommended"],
                ActionText = l["Choose Small company"]
            },
            new("company", l["Company"], l["Every project of one company"], new(ProjectPricing.CompanyMonthly, PricingPeriod.Month))
            {
                YearlyPrice = new(ProjectPricing.CompanyYearly, PricingPeriod.Year),
                Includes = [l["No limit on projects"], l["Everything in Small company"]],
                ActionText = l["Choose Company"]
            }
        ],
        Samples =
        [
            new(BitIconName.Education, l["A student or a hobbyist"], l["A person, building on their own."], Euros(0)),
            new(BitIconName.People, l["Two co-founders who set up a company"], l["Their company's first product, on Small company."], l["{0} a month", Euros(ProjectPricing.TeamMonthly)]),
            new(BitIconName.CityNext, l["A company with five products"], l["Company, with no limit on projects."], l["{0} a month", Euros(ProjectPricing.CompanyMonthly)]),
            new(BitIconName.Shop, l["A shop using an app built for it"], l["Using an app needs no license. Only its builder has one."], Euros(0))
        ],
        CloudIncluded = PricingShared.CloudIncluded(l),
        Questions =
        [
            new(l["Which company pays: the one that builds the app, or the one that uses it?"], l["The one that builds and owns it. The people and companies who use your app never need a license from us."]),
            PricingShared.WhatIsAProject(l),
            new(l["When does a person need a company license?"], l["When a company owns the app, or pays for it to be built. Building your own idea on your own stays free."]),
            PricingShared.Cheaper(l),
            PricingShared.StopPaying(l),
            PricingShared.ForLife(l),
            PricingShared.Invoice(l),
            PricingShared.Refund(l),
            PricingShared.AfterBeta(l)
        ]
    };
}
