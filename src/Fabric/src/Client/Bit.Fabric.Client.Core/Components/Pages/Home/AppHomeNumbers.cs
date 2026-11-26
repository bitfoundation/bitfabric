namespace Bit.Fabric.Client.Core.Components.Pages.Home;

/// <summary>What the home page, <c>/features</c> and <c>/contents</c> show: the numbers that earn trust, and where they and the live demo are.</summary>
public static class AppHomeNumbers
{
    public const string GitHubUrl = "https://github.com/bitfoundation/bitplatform";

    /// <summary>The bit platform team's admin panel demo on the web, built on the same template.</summary>
    public const string AdminPanelDemoUrl = "https://adminpanel.bitplatform.dev/";

    /// <summary>The template's Workbench on sample data, with its pages, flows, boards and a Progress task, open to anyone.</summary>
    public const string WorkbenchDemoUrl = "https://workbench.bitfabric.co";

    // Written in the page and updated by hand, here only: the texts that name them take them as arguments: 1,382 stars and
    // 127 contributors on 2026-11-07, and the team's 500+ improvements a month on average, the team's own figure of the same
    // day. The downloads are of bit platform's packages on nuget.org, named "downloads" alone, as the persona has never heard
    // of NuGet.
    public const string GitHubStars = "1.4K";
    public const string Contributors = "120+";
    public const string Downloads = "10M+";
    public const string ImprovementsAMonth = "500+";

    public static (string Number, string Label)[] Of(IStringLocalizer<AppStrings> localizer) =>
    [
        (GitHubStars, localizer[nameof(AppStrings.AppHomeGitHubStars)]),
        (Contributors, localizer[nameof(AppStrings.AppHomeContributors)]),
        (Downloads, localizer[nameof(AppStrings.AppHomeDownloads)]),
        (ImprovementsAMonth, localizer[nameof(AppStrings.AppHomeImprovementsAMonth)])
    ];
}
