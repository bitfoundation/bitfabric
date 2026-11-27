using System.Globalization;

namespace Bit.Fabric.Workbench.Components.Pages.ProjectTelemetry.Data;

/// <summary>How a number reads against what the app should do: fine, worth watching, needing a look, just a number, or not measured yet.</summary>
public enum DashboardLevel
{
    Good,
    Watch,
    Bad,
    Info,
    NotMeasured
}

/// <summary>What a card draws: one number with its trend, or a short list of rows.</summary>
public enum DashboardCardKind
{
    Number,
    List
}

/// <summary>Whether an alert is quiet, firing now, or paused by the user.</summary>
public enum DashboardAlertState
{
    Quiet,
    Firing,
    Paused
}

/// <summary>A row of a list card: what it names, its number, and how it reads. A name that is no resource key, like a route, shows as it is.</summary>
public sealed record DashboardRow(string Name, string Value, DashboardLevel Level = DashboardLevel.Info);

/// <summary>
/// One card of a project's telemetry dashboard: a ready one every project gets, or one the customer's AI added through
/// MCP, with the request it was asked in. Its texts are resource keys; a value is formatted for the culture it shows in.
/// </summary>
public sealed record DashboardCard(string Key, string GroupKey, DashboardCardKind Kind, string TitleKey, string Value, string Unit, string SentenceKey,
                                   DashboardLevel Level, double? Change = null, bool LowerIsBetter = false, bool IsLive = false, bool IsEstimate = false,
                                   double[]? Series = null, DashboardRow[]? Rows = null, int DefaultSize = 1, string? AskedKey = null)
{
    /// <summary>A card the customer's AI added: it can be deleted, where a ready card can only be hidden.</summary>
    public bool IsMadeByAi => AskedKey is not null;
}

/// <summary>An alert the customer's AI set up: the rule in words, where it is sent, its state and the request it was asked in.</summary>
public sealed record DashboardAlert(string Key, string TitleKey, string ChannelsKey, DashboardAlertState State, string StatusKey, string AskedKey, params object[] StatusArguments);

/// <summary>A group of cards under one heading, which folds away.</summary>
public sealed record DashboardGroup(string Key, string IconName, string TitleKey);

/// <summary>
/// A project's telemetry on sample numbers, the same on every run: an app with a few hundred people a day, an AI chat,
/// background jobs and images, one thing that needs a look and two worth watching, two cards and three alerts its AI
/// added. The page creates it itself while its design moves.
/// </summary>
public static class TelemetryDashboardData
{
    /// <summary>The page switch for what the customer's AI added: two cards and three alerts, or nothing yet.</summary>
    public const string AiSwitch = "ai";
    public const string AiNone = "none";

    /// <summary>The page switch for the alerts: one firing, or all quiet.</summary>
    public const string AlertSwitch = "alert";
    public const string AlertQuiet = "quiet";

    public const string YoursGroup = "yours";

    public static readonly DashboardGroup[] Groups =
    [
        new(YoursGroup, BitIconName.Robot, nameof(WorkbenchStrings.TelemetryGroupYours)),
        new("speed", BitIconName.SpeedHigh, nameof(WorkbenchStrings.TelemetryGroupSpeed)),
        new("people", BitIconName.People, nameof(WorkbenchStrings.TelemetryGroupPeople)),
        new("ai", BitIconName.Lightbulb, nameof(WorkbenchStrings.TelemetryGroupAi)),
        new("jobs", BitIconName.Clock, nameof(WorkbenchStrings.TelemetryGroupJobs)),
        new("files", BitIconName.Photo2, nameof(WorkbenchStrings.TelemetryGroupFiles)),
        new("server", BitIconName.Server, nameof(WorkbenchStrings.TelemetryGroupServer))
    ];

    /// <summary>The prompts the page suggests, each a resource key, to copy to the customer's AI.</summary>
    public static readonly string[] Examples =
    [
        nameof(WorkbenchStrings.TelemetryExampleCard),
        nameof(WorkbenchStrings.TelemetryExampleCostAlert),
        nameof(WorkbenchStrings.TelemetryExampleJobsAlert)
    ];

    /// <summary>The cards in their first order: the AI's own first, then the ready ones. A user's own layout is kept in ProjectTelemetryStore.</summary>
    public static DashboardCard[] Cards(bool withAiCards, bool jobsFailing)
    {
        DashboardCard[] aiCards =
        [
            new("ai-pages", YoursGroup, DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardTopPages), "", "", nameof(WorkbenchStrings.TelemetryCardTopPagesLine), DashboardLevel.Info, DefaultSize: 2,
                Rows: [new("/", N(4210)), new("/products", N(2875)), new("/dashboard", N(1940)), new("/settings", N(655)), new("/about", N(410))],
                AskedKey: nameof(WorkbenchStrings.TelemetryExampleCard)),
            new("ai-cost-user", YoursGroup, DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardCostPerUser), $"${N(0.021, "0.000")}", "", nameof(WorkbenchStrings.TelemetryCardCostPerUserLine), DashboardLevel.Info, 0.06, LowerIsBetter: true,
                Series: Wave(23, 0.02, 0.006), DefaultSize: 2, AskedKey: nameof(WorkbenchStrings.TelemetryAskedCostPerUser))
        ];

        DashboardCard[] readyCards =
        [
            new("p95", "speed", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardAnswerTime), N(420), "ms", nameof(WorkbenchStrings.TelemetryCardAnswerTimeLine), DashboardLevel.Watch, 0.11, LowerIsBetter: true, Series: Wave(1, 380, 60), DefaultSize: 2),
            new("median", "speed", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardTypicalTime), N(120), "ms", nameof(WorkbenchStrings.TelemetryCardTypicalTimeLine), DashboardLevel.Good, -0.04, LowerIsBetter: true, Series: Wave(2, 120, 20)),
            new("errors", "speed", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardFailedRequests), N(0.3, "0.0"), "%", nameof(WorkbenchStrings.TelemetryCardFailedRequestsLine), DashboardLevel.Good, -0.2, LowerIsBetter: true, Series: Wave(3, 0.4, 0.3)),
            new("rpm", "speed", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardRequests), N(1240), nameof(WorkbenchStrings.TelemetryUnitPerMinute), nameof(WorkbenchStrings.TelemetryCardRequestsLine), DashboardLevel.Info, 0.08, Series: Wave(4, 1200, 300)),
            new("slowest", "speed", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardSlowestPages), "", "", nameof(WorkbenchStrings.TelemetryCardSlowestPagesLine), DashboardLevel.Info, DefaultSize: 2,
                Rows: [new("/projects/{id}", $"{N(1.8, "0.0")} s", DashboardLevel.Bad), new("/settings", $"{N(760)} ms", DashboardLevel.Watch), new("/api/Chatbot", $"{N(690)} ms", DashboardLevel.Watch), new("/dashboard", $"{N(310)} ms", DashboardLevel.Good), new("/", $"{N(140)} ms", DashboardLevel.Good)]),
            new("vitals", "speed", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardPageLoad), "", "", nameof(WorkbenchStrings.TelemetryCardPageLoadLine), DashboardLevel.Good,
                Rows: [new("LCP", $"{N(2.1, "0.0")} s", DashboardLevel.Good), new("INP", $"{N(180)} ms", DashboardLevel.Good), new("CLS", N(0.04, "0.00"), DashboardLevel.Good)]),
            new("outbound", "speed", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardOutside), "", "", nameof(WorkbenchStrings.TelemetryCardOutsideLine), DashboardLevel.Info,
                Rows: [new("api.openai.com", $"{N(1.2, "0.0")} s", DashboardLevel.Watch), new("api.twilio.com", $"{N(340)} ms", DashboardLevel.Good), new("s3.amazonaws.com", $"{N(95)} ms", DashboardLevel.Good)]),
            new("db", "speed", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardSlowQueries), "", "", nameof(WorkbenchStrings.TelemetryCardSlowQueriesLine), DashboardLevel.Info, IsEstimate: true,
                Rows: [new("Products · search", $"{N(820)} ms", DashboardLevel.Watch), new("Orders · by user", $"{N(310)} ms", DashboardLevel.Good), new("Users · by email", $"{N(40)} ms", DashboardLevel.Good)]),
            new("cache", "speed", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardCache), N(87), "%", nameof(WorkbenchStrings.TelemetryCardCacheLine), DashboardLevel.Good, 0.02, Series: Wave(5, 86, 3)),

            new("online", "people", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardOnline), N(34), "", nameof(WorkbenchStrings.TelemetryCardOnlineLine), DashboardLevel.Info, IsLive: true, Series: Wave(6, 30, 12)),
            new("signins", "people", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardSignIns), N(212), "", nameof(WorkbenchStrings.TelemetryCardSignInsLine), DashboardLevel.Info, 0.05, Series: Wave(7, 18, 9)),
            new("failed-signins", "people", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardFailedSignIns), N(18), "", nameof(WorkbenchStrings.TelemetryCardFailedSignInsLine), DashboardLevel.Watch, 0.64, LowerIsBetter: true, Series: Wave(8, 1, 2)),
            new("signups", "people", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardSignUps), N(9), "", nameof(WorkbenchStrings.TelemetryCardSignUpsLine), DashboardLevel.Info, 0.29, Series: Wave(10, 1, 1)),
            new("denied", "people", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardDenied), N(7), "", nameof(WorkbenchStrings.TelemetryCardDeniedLine), DashboardLevel.Good, -0.3, LowerIsBetter: true, Series: Wave(9, 1, 1)),

            new("cost", "ai", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardAiCost), $"${N(3.4, "0.00")}", "", nameof(WorkbenchStrings.TelemetryCardAiCostLine), DashboardLevel.Info, 0.17, LowerIsBetter: true, Series: Wave(12, 0.15, 0.1), DefaultSize: 2),
            new("tokens", "ai", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardTokens), "", "", nameof(WorkbenchStrings.TelemetryCardTokensLine), DashboardLevel.Info,
                Rows: [new("gpt-5-mini", $"{N(910)} K"), new("claude-sonnet-5-5", $"{N(260)} K"), new("whisper-1", $"{N(38)} min")]),
            new("ai-time", "ai", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardAiTime), N(4.2, "0.0"), "s", nameof(WorkbenchStrings.TelemetryCardAiTimeLine), DashboardLevel.Good, -0.06, LowerIsBetter: true, Series: Wave(13, 4, 1)),
            new("chats", "ai", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardChats), N(3), "", nameof(WorkbenchStrings.TelemetryCardChatsLine), DashboardLevel.Info, IsLive: true, Series: Wave(11, 2, 2)),

            new("failed-jobs", "jobs", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardFailedJobs), N(jobsFailing ? 7 : 1), "", nameof(WorkbenchStrings.TelemetryCardFailedJobsLine), jobsFailing ? DashboardLevel.Bad : DashboardLevel.Good, jobsFailing ? 2.5 : -0.5, LowerIsBetter: true, IsEstimate: true, Series: Wave(14, jobsFailing ? 0.6 : 0.1, 0.5)),
            new("jobs-done", "jobs", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardJobsDone), N(1820), "", nameof(WorkbenchStrings.TelemetryCardJobsDoneLine), DashboardLevel.Info, 0.04, IsEstimate: true, Series: Wave(15, 75, 20)),
            new("jobs-time", "jobs", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardJobTimes), "", "", nameof(WorkbenchStrings.TelemetryCardJobTimesLine), DashboardLevel.Info, IsEstimate: true, DefaultSize: 2,
                Rows: [new("EmailJob.Send", $"{N(1.1, "0.0")} s · {N(9)} s", jobsFailing ? DashboardLevel.Bad : DashboardLevel.Good), new("ImageJob.Resize", $"{N(640)} ms · {N(2.4, "0.0")} s", DashboardLevel.Good), new("CleanupJob.Run", $"{N(14)} s · {N(31)} s", DashboardLevel.Good)]),

            new("resize", "files", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardResize), N(640), "ms", nameof(WorkbenchStrings.TelemetryCardResizeLine), DashboardLevel.Good, -0.1, LowerIsBetter: true, Series: Wave(16, 600, 150)),
            new("uploads", "files", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardUploads), N(58), "", nameof(WorkbenchStrings.TelemetryCardUploadsLine), DashboardLevel.Info, 0.12, Series: Wave(17, 3, 3)),

            new("cpu", "server", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardCpu), N(23), "%", nameof(WorkbenchStrings.TelemetryCardCpuLine), DashboardLevel.Good, IsLive: true, Series: Wave(20, 22, 12)),
            new("memory", "server", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardMemory), N(612), "MB", nameof(WorkbenchStrings.TelemetryCardMemoryLine), DashboardLevel.Good, IsLive: true, Series: Wave(21, 600, 40)),
            new("exceptions", "server", DashboardCardKind.Number, nameof(WorkbenchStrings.TelemetryCardErrors), N(3), "", nameof(WorkbenchStrings.TelemetryCardErrorsLine), DashboardLevel.Watch, 0.5, LowerIsBetter: true, Series: Wave(22, 0.3, 0.6)),
            new("instances", "server", DashboardCardKind.List, nameof(WorkbenchStrings.TelemetryCardInstances), "", "", nameof(WorkbenchStrings.TelemetryCardInstancesLine), DashboardLevel.Info,
                Rows: [new("web-1", "2.4.0", DashboardLevel.Good), new("web-2", "2.4.0", DashboardLevel.Good), new("api-1", "2.4.0", DashboardLevel.Good)])
        ];

        return withAiCards ? [.. aiCards, .. readyCards] : readyCards;
    }

    /// <summary>The alerts the customer's AI set up: the two the founder named, and one on the business itself.</summary>
    public static DashboardAlert[] Alerts(bool jobsFailing) =>
    [
        new("alert-jobs", nameof(WorkbenchStrings.TelemetryAlertJobs), nameof(WorkbenchStrings.TelemetryChannelsEmailPush),
            jobsFailing ? DashboardAlertState.Firing : DashboardAlertState.Quiet,
            jobsFailing ? nameof(WorkbenchStrings.TelemetryAlertFiringFor) : nameof(WorkbenchStrings.TelemetryAlertLastFired),
            nameof(WorkbenchStrings.TelemetryExampleJobsAlert), jobsFailing ? new object[] { 12, 7 } : new object[] { 5 }),
        new("alert-ai-cost", nameof(WorkbenchStrings.TelemetryAlertCost), nameof(WorkbenchStrings.TelemetryChannelsEmailPush), DashboardAlertState.Quiet,
            nameof(WorkbenchStrings.TelemetryAlertLastFired), nameof(WorkbenchStrings.TelemetryExampleCostAlert), 3),
        new("alert-signups", nameof(WorkbenchStrings.TelemetryAlertSignUps), nameof(WorkbenchStrings.TelemetryChannelsEmail), DashboardAlertState.Quiet,
            nameof(WorkbenchStrings.TelemetryAlertNeverFired), nameof(WorkbenchStrings.TelemetryAskedSignUps))
    ];

    /// <summary>A number as the page's culture writes it.</summary>
    private static string N(double value, string format = "N0") => value.ToString(format, CultureInfo.CurrentCulture);

    /// <summary>A day of a number: a smooth wave around its level, the same on every run.</summary>
    private static double[] Wave(int seed, double level, double spread)
    {
        var random = new Random(seed);
        var phase = random.NextDouble() * Math.PI * 2;

        return [.. Enumerable.Range(0, 24).Select(i => Math.Max(0, level + spread * (0.6 * Math.Sin(phase + i / 3.0) + 0.4 * (random.NextDouble() - 0.5))))];
    }
}
