using Bit.Fabric.Workbench.Components.Pages.HomeDirections;
using Microsoft.Extensions.Localization;
using static Bit.Fabric.Workbench.Components.Pages.Compare.Data.CompareShared;

namespace Bit.Fabric.Workbench.Components.Pages.Compare.Data;

/// <summary>
/// bitfabric.co/compare/empty-project: the visitor's own AI agent on an empty project. bit Fabric doesn't replace the agent, it
/// is what the agent starts from, so the page says so first. The AI plans' prices and changes are from
/// bitfabric-internal/.agents/gtm/research/competitors.md, sections 1b and 4, and the leak figure from its section 7 (UpGuard,
/// which left out builder-hosted apps), all read on 19 November 2026.
/// </summary>
public static class EmptyProjectComparison
{
    public const string Key = "empty-project";

    public static CompareSubject Build(IStringLocalizer l) => new(Key, l["Your AI on an empty project"])
    {
        TableTitle = [l["bit Fabric and an empty project, side by side"]],
        Answer = l["An empty project is the better choice if you want to pick every part and build the foundations with your AI. bit Fabric is the better choice if you want the same AI to start from an app already built and tested on five platforms."],
        FitTitle = [l["An empty project or bit Fabric?"]],
        FitLead = l["Either way, your own AI builds it on your computer. The difference is what it starts from."],
        CardLine = l["The same AI, starting from nothing or from an app that's built and tested"],
        CardIcon = BitIconName.NewFolder,
        Columns = [l["Your AI on an empty project"], "bit Fabric"],
        Rows =
        [
            Row(l, CompareTopic.DoesWell,
                F(l["Every part is yours from the first line, in any stack you pick, and your AI plan is the only bill while you build"])),
            Row(l, CompareTopic.DayOne,
                F(l["An empty folder. Identity, roles, tenants, languages and every platform are built from scratch, one prompt at a time"])),
            Row(l, CompareTopic.Where,
                F(l["Wherever you and your AI put them; all of it is yours"])),
            Row(l, CompareTopic.StopPaying,
                F(l["Nothing of your app pauses; only the building waits for your AI plan"]),
                F(l["The same: nothing of your app pauses, and only the building waits for your AI plan"])),
            Row(l, CompareTopic.Platforms,
                F(l["Each platform is another app for your AI to build and test"])),
            Row(l, CompareTopic.Cost,
                F(l["Your AI plan: Copilot Pro $10 a month, Claude Pro or ChatGPT Plus $20, and a server once your app is live"], "AG-5", "AG-1", "AG-8")),
            Row(l, CompareTopic.Ai,
                F(l["The agent you pick; its provider sees your prompts, on its plan's terms"]),
                F(l["The same agents, or a model on your own computer: Copilot, Codex, Claude, Gemini, Antigravity or Cursor. bit Fabric never sees your prompts"])),
            Row(l, CompareTopic.Production,
                F(l["Your AI writes the security as you prompt. UpGuard found 16,326 Supabase databases exposing readable tables: a database key in the browser, with no access rules"], "INC-4"),
                F(l["Built and tested before your first prompt: no database key reaches the browser, and production is read-only for the AI"])),
            Row(l, CompareTopic.VendorStops,
                F(l["Your code is yours. AI plans do change: Gemini CLI stopped serving personal Google accounts on 18 June 2026, and Copilot moved to usage-based billing on 1 June 2026"], "AG-10", "AG-6"),
                F(l["Your app keeps running if bit Fabric stops, and it works with every agent, so a plan that changes is one you can leave"]))
        ],
        SourceLine = l["The AI plans' prices and changes come from their makers' own pages, and the leak figure from UpGuard, all read on {0}. Each number leads to its source.", ReadOn],
        Choices =
        [
            new(l["Choose an empty project if"],
            [
                l["You want to pick every language, framework and service yourself"],
                l["You enjoy building the foundations with your AI"],
                l["Your app is small, with no need for roles, tenants or more than one platform"],
                l["Your AI plan should be your only bill while you build"]
            ]),
            new(l["Choose bit Fabric if"],
            [
                l["You want your AI to start from a complete identity solution, multi-tenancy and an admin panel, built and tested"],
                l["You need web, Android, iOS, Windows and macOS from one codebase"],
                l["You want security that doesn't rest on what your AI writes in one prompt"],
                l["You'd like your AI to work like a team: ask, plan, design in the Workbench, test and review"]
            ])
        ],
        Gaps =
        [
            StackGap(l),
            new(BitIconName.Download, l["More to install"], l["Besides your agent, the line installs the tools your project needs, into your user folder, with no administrator rights."]),
            PaymentsGap(l)
        ],
        Questions =
        [
            new(l["Does bit Fabric replace my AI agent?"],
                l["No. bit Fabric is what your agent starts from. Your agent still does the building, on your computer, guided by the project's own instructions and skills."]),
            new(l["Can I still change anything?"],
                l["Yes. It's your code, in your repository, and your AI changes any part of it the way it would on an empty project."]),
            WhichAi(l),
            AfterBeta(l)
        ],
        MoreSources = PageSources
    };
}
