using Bit.Fabric.Client.Core.Components.Pages.Home.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.Home.Compare.Data;

/// <summary>
/// What every comparison says the same way: the rows' topics, bit Fabric's side of each row, how bit Fabric is built on
/// purpose, what it costs and the questions the pages share. bit Fabric's words stay within the claims table of
/// bitfabric-internal/.agents/product-marketing.md: no .NET or C#, never "free" for the whole thing, the agents named
/// as the home page names them, and what comes after the beta said as /pricing and llms.txt say it.
/// </summary>
public static class CompareShared
{
    /// <summary>The day every source was read (bitfabric-internal/.agents/gtm/research/competitors.md), for the JSON-LD's dateModified.</summary>
    public static readonly DateOnly CheckedOn = new(2026, 11, 19);

    /// <summary>The source of the cost band's AI plan price, on every page (Copilot Pro, $10 a month).</summary>
    public static readonly string[] CostSources = ["AG-5"];

    /// <summary>A fact and the ids of the sources it rests on.</summary>
    public static CompareFact F(string text, params string[] sources) => new(text, sources);

    /// <summary>A row of a page that sets one other column next to bit Fabric's; bit Fabric's own fact unless one is given.</summary>
    public static CompareRow Row(IStringLocalizer<AppStrings> l, CompareTopic topic, CompareFact theirs, CompareFact? ours = null)
        => new(topic, [theirs, ours ?? Ours(l, topic)]);

    /// <summary>bit Fabric's column, last on every page.</summary>
    public static CompareColumn FabricColumn => new("bit Fabric");

    public static string TopicLabel(IStringLocalizer<AppStrings> l, CompareTopic topic) => topic switch
    {
        CompareTopic.DoesWell => l[nameof(AppStrings.CompareTopicDoesWell)],
        CompareTopic.DayOne => l[nameof(AppStrings.CompareTopicDayOne)],
        CompareTopic.Where => l[nameof(AppStrings.CompareTopicWhere)],
        CompareTopic.StopPaying => l[nameof(AppStrings.CompareTopicStopPaying)],
        CompareTopic.Platforms => l[nameof(AppStrings.CompareTopicPlatforms)],
        CompareTopic.Cost => l[nameof(AppStrings.CompareTopicCost)],
        CompareTopic.Ai => l[nameof(AppStrings.CompareTopicAi)],
        CompareTopic.GoingLive => l[nameof(AppStrings.CompareTopicGoingLive)],
        CompareTopic.Security => l[nameof(AppStrings.CompareTopicSecurity)],
        CompareTopic.Leaving => l[nameof(AppStrings.CompareTopicLeaving)],
        CompareTopic.Terms => l[nameof(AppStrings.CompareTopicTerms)],
        _ => l[nameof(AppStrings.CompareTopicUpdates)]
    };

    /// <summary>bit Fabric's side of a row, said the same way on every page.</summary>
    public static CompareFact Ours(IStringLocalizer<AppStrings> l, CompareTopic topic) => topic switch
    {
        CompareTopic.DoesWell => F(l[nameof(AppStrings.CompareOursDoesWell)]),
        CompareTopic.DayOne => F(l[nameof(AppStrings.CompareOursDayOne)]),
        CompareTopic.Where => F(l[nameof(AppStrings.CompareOursWhere)]),
        CompareTopic.StopPaying => F(l[nameof(AppStrings.CompareOursStopPaying)]),
        CompareTopic.Platforms => F(l[nameof(AppStrings.CompareOursPlatforms)]),
        CompareTopic.Cost => F(l[nameof(AppStrings.CompareOursCost)], "AG-5"),
        CompareTopic.Ai => F(l[nameof(AppStrings.CompareOursAi)]),
        CompareTopic.GoingLive => F(l[nameof(AppStrings.CompareOursGoingLive)]),
        CompareTopic.Security => F(l[nameof(AppStrings.CompareOursSecurity)]),
        CompareTopic.Leaving => F(l[nameof(AppStrings.CompareOursLeaving)]),
        CompareTopic.Terms => F(l[nameof(AppStrings.CompareOursTerms)]),
        _ => F(l[nameof(AppStrings.CompareOursUpdates)])
    };

    /// <summary>
    /// How bit Fabric is built on purpose, next to a hosted builder: each a choice and what it gives the visitor, said only of
    /// bit Fabric: the site argues bit Fabric's case, so this band is no list of what it lacks. Payments aren't here: the
    /// home page shows them with no caveat.
    /// </summary>
    public static AppHomeTile[] HostedGaps(IStringLocalizer<AppStrings> l) =>
    [
        new(BitIconName.BrowserTab, l[nameof(AppStrings.CompareGapPreviewTitle)], l[nameof(AppStrings.CompareGapPreviewText)]),
        new(BitIconName.Download, l[nameof(AppStrings.CompareGapInstallTitle)], l[nameof(AppStrings.CompareGapInstallText)]),
        new(BitIconName.Server, l[nameof(AppStrings.CompareGapServerTitle)], l[nameof(AppStrings.CompareGapServerText)])
    ];

    public static AppHomeTile StackGap(IStringLocalizer<AppStrings> l)
        => new(BitIconName.Code, l[nameof(AppStrings.CompareGapStackTitle)], l[nameof(AppStrings.CompareGapStackText)]);

    public static AppHomeTile MoreInstallGap(IStringLocalizer<AppStrings> l)
        => new(BitIconName.Download, l[nameof(AppStrings.CompareGapMoreInstallTitle)], l[nameof(AppStrings.CompareGapMoreInstallText)]);

    public static CompareQuestion WhichAi(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.CompareQuestionWhichAi)], l[nameof(AppStrings.CompareAnswerWhichAi)], ["AG-10"]);

    /// <summary>What comes after the beta, as /pricing and llms.txt say it: not decided, and published before it applies.</summary>
    public static CompareQuestion AfterBeta(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.CompareQuestionAfterBeta)], l[nameof(AppStrings.CompareAnswerAfterBeta)], []);

    public static CompareQuestion Hosting(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.CompareQuestionHosting)], l[nameof(AppStrings.CompareAnswerHosting)], []);

    public static CompareQuestion ReplaceAgent(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.CompareQuestionReplaceAgent)], l[nameof(AppStrings.CompareAnswerReplaceAgent)], []);

    /// <summary>
    /// Which costs less, said plainly: a hosted builder for a landing page or a small, simple app,
    /// bit Fabric for a real app, and why. bit Fabric's own case, so it rests on no source; the server's price is the
    /// cost band's (AG-5 is the AI plan's, already on the page).
    /// </summary>
    public static CompareQuestion Cheaper(IStringLocalizer<AppStrings> l)
        => new(l[nameof(AppStrings.CompareQuestionCheaper)], l[nameof(AppStrings.CompareAnswerCheaper)], []);

    /// <summary>The questions of a page about a hosted builder: moving from it, which costs less, hosting, which AI and the beta.</summary>
    public static CompareQuestion[] HostedQuestions(IStringLocalizer<AppStrings> l, string moveQuestion, string moveAnswer) =>
        [new(moveQuestion, moveAnswer, []), Cheaper(l), Hosting(l), WhichAi(l), AfterBeta(l)];
}
