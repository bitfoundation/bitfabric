using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Pricing.Data.PricingFormat;

namespace Bit.Fabric.Workbench.Components.Pages.Pricing.Data;

/// <summary>
/// Option 1, "By projects" (brainstorming, not a ruling): every project has a license file from bit CLI, a month's trial
/// first. Then free for those who can't pay, €10 a month for up to three projects, with health checks, monitoring, a status
/// page and the scanners, or €200 a month with no limit on projects, as long as they are the same company's. The number of
/// projects is the real difference between the paid plans, not what a company can afford. "Project" is what bit CLI makes;
/// how to say it without ambiguity is still open (bitfabric-internal/specs/product.md). Prices in euros excluding VAT: change
/// a number here and the page follows.
/// </summary>
public static class ProjectPricing
{
    public const string Key = "projects";

    /// <summary>How many projects the Team license covers; the Company license has no limit.</summary>
    public const int TeamProjects = 3;

    public const decimal TeamMonthly = 10;
    public const decimal TeamYearly = 100;
    public const decimal CompanyMonthly = 200;
    public const decimal CompanyYearly = 2_000;

    /// <summary>The trial license bit CLI writes into every new project.</summary>
    public const int TrialDays = 30;

    public static PricingStrategy Build(IStringLocalizer l) => new(Key,
        l["Pay for the projects you build. Nothing if you can't."],
        l["Every new project starts with a {0}-day trial. After it, the license is free if you can't pay, {1} a month for up to {2} projects, or {3} a month for every project of your company.", TrialDays, Euros(TeamMonthly), TeamProjects, Euros(CompanyMonthly)])
    {
        Basis = PricingShared.ProjectBasis(l),
        Plans =
        [
            new("free", l["Free"], l["When you can't pay yet"], new(0, PricingPeriod.None))
            {
                Includes = [l["Up to {0} projects", TeamProjects], l["Everything in Team"], l["Renewed each year, as long as you need it"]],
                Action = PricingAction.Start,
                ActionText = l["Get started"]
            },
            new("team", l["Team"], l["Up to {0} projects", TeamProjects], new(TeamMonthly, PricingPeriod.Month))
            {
                YearlyPrice = new(TeamYearly, PricingPeriod.Year),
                Includes = [l["Every upgrade, applied by your AI"], l["The skills and libraries to keep building"], l["Health checks, monitoring, a status page and scans"]],
                Highlight = l["Recommended"],
                ActionText = l["Choose Team"]
            },
            new("company", l["Company"], l["Every project of one company"], new(CompanyMonthly, PricingPeriod.Month))
            {
                YearlyPrice = new(CompanyYearly, PricingPeriod.Year),
                Includes = [l["No limit on projects"], l["Everything in Team"], l["For the company's own projects"]],
                ActionText = l["Choose Company"]
            }
        ],
        Samples =
        [
            new(BitIconName.Education, l["A student learning to build"], l["The free license, for as long as it's needed."], Euros(0)),
            new(BitIconName.People, l["Two co-founders on their first product"], l["One project on Team, with room for two more."], l["{0} a month", Euros(TeamMonthly)]),
            new(BitIconName.CityNext, l["A company with five products"], l["Company, with no limit on projects."], l["{0} a month", Euros(CompanyMonthly)]),
            new(BitIconName.Contact, l["A freelancer building for a client"], l["Team while building. The client takes its own license at handover."], l["{0} a month", Euros(TeamMonthly)])
        ],
        Extra = (l["What your license gives you"], l["Everything you need to keep building your app."],
        [
            new(BitIconName.Sync, l["Every upgrade"], l["Each release reaches your project through your AI, with every fix in it."]),
            new(BitIconName.Robot, l["The skills and libraries"], l["What your AI builds with, from identity to deployment."]),
            new(BitIconName.Health, l["Your live app watched"], l["Health checks, monitoring, a status page and security scans."]),
            new(BitIconName.Unlock, l["Open source after a year"], l["Each version becomes open source (MIT) a year after its release."])
        ]),
        CloudIncluded = PricingShared.CloudIncluded(l),
        Questions =
        [
            PricingShared.WhatIsAProject(l),
            PricingShared.Cheaper(l),
            new(l["Who decides that I can't pay?"], l["You do. Tell us, and the free license is yours. We never see your code or your books."]),
            new(l["Do I need a license if I pay nothing?"], l["Yes, a free one. bit CLI makes a {0}-day trial license with every new project, and a free license after it takes a few clicks.", TrialDays]),
            PricingShared.StopPaying(l),
            PricingShared.ForLife(l),
            PricingShared.Invoice(l),
            PricingShared.Refund(l),
            PricingShared.AfterBeta(l)
        ]
    };
}
