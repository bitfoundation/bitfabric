using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// Option 4, "A license for life": the first 500 pay once for a license that never runs out. Each costs about two years of
/// the plan it stands for, at option 1's prices, and comes with option 1's limits on projects. Prices in euros excluding VAT:
/// change a number here and the page follows.
/// </summary>
public static class MemberPricing
{
    public const string Key = "members";

    public const int FoundingSeats = 500;

    /// <summary>A license for life costs this many months of the plan it stands for.</summary>
    public const int MonthsOfPlan = 24;

    public const decimal FoundingOnce = ProjectPricing.TeamMonthly * MonthsOfPlan;

    public const decimal FoundingCompanyOnce = ProjectPricing.CompanyMonthly * MonthsOfPlan;

    /// <summary>Sample data: the founding seats still free, as the server would count them.</summary>
    public const int SampleSeatsLeft = 312;

    public static PricingStrategy Build(IStringLocalizer l) => new(Key,
        l["A license for life, for the first {0}.", FoundingSeats],
        l["Pay once, about two years of your plan, and your license never runs out: every upgrade, the skills and your live apps watched, for good. Changed your mind? All of it back within {0} days.", PricingShared.RefundDays])
    {
        Basis = PricingShared.ProjectBasis(l),
        Plans =
        [
            new("founding", l["Founding member"], l["Up to {0} projects", ProjectPricing.TeamProjects], new(FoundingOnce, PricingPeriod.Once))
            {
                PriceNote = l["For life"],
                Includes = [l["Every upgrade, applied by your AI"], l["The skills and libraries to keep building"], l["Health checks, monitoring, a status page and scans"]],
                Highlight = l["Recommended"],
                ActionText = l["Take a founding seat"],
                CountsSeats = true
            },
            new("founding-company", l["Founding company"], l["Every project of one company"], new(FoundingCompanyOnce, PricingPeriod.Once))
            {
                PriceNote = l["For life"],
                Includes = [l["No limit on projects"], l["Everything a founding member gets"]],
                ActionText = l["Take a company seat"]
            },
            new("monthly", l["Rather by the month?"], l["Up to {0} projects", ProjectPricing.TeamProjects], new(ProjectPricing.TeamMonthly, PricingPeriod.Month))
            {
                PriceNote = l["Or {0} a month with no limit on projects", Euros(ProjectPricing.CompanyMonthly)],
                Includes = [l["Free if you can't pay"]],
                ActionText = l["Choose by the month"]
            }
        ],
        CloudIncluded = PricingShared.CloudIncluded(l),
        Questions =
        [
            new(l["What does \"for life\" mean?"], l["Your license never runs out, for as long as bit Fabric runs, with no further payment."]),
            new(l["What if bit Fabric is sold or closes?"], l["The license terms say what happens then, and every version a year old is open source in any case."]),
            new(l["How many seats are left?"], l["The counter on the founding card shows it. The seats close once all {0} are taken.", FoundingSeats]),
            PricingShared.WhatIsAProject(l),
            PricingShared.Invoice(l),
            PricingShared.Refund(l),
            new(l["What changes when the beta ends?"], l["Founding seats can be taken during the beta, and close once all {0} are taken.", FoundingSeats])
        ]
    };
}
