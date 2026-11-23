namespace Bit.Fabric.Server.Api.Features.Monitoring;

/// <summary>
/// What the tools answer, as a model reads it best: names over codes, the app's own names for its parts, times in UTC
/// unless the name says otherwise, and every duration with its unit in its name. Nulls are left out (See Result).
/// </summary>
public partial class ProjectMonitoringMcpTools
{
    /// <summary>Why a request can't be answered as asked, with the projects to pick from when it named none, or several.</summary>
    public sealed record Problem(string Message, ProjectChoice[]? Projects = null);

    public sealed record ProjectChoice(Guid Id, string? Name, string? Address);

    public sealed record ProjectRef(Guid Id, string? Name);

    /// <summary>
    /// <c>Team</c>: The team the request is for: a user in several sees which one answered.
    /// </summary>
    /// <para><c>DaysIn</c>: The time zone each project's days are counted in.</para>
    public sealed record ProjectList(string? Team, string DaysIn, ProjectSummary[] Projects);

    public sealed record ProjectSummary(Guid Id,
        string? Name,
        string? Address,
        string Health,
        string? ConnectionProblem,
        double? Uptime30DaysPercent,
        int? TypicalResponseMs,
        int? Slowest5PercentResponseMs,
        string[] PartsNotWorking,
        OngoingIncident? OngoingIncident,
        string? CacheWarning,
        DateTimeOffset? LastCheckedAt,
        DayWithProblems[] Last30DaysWithProblems);

    /// <summary>
    /// A day of the last 30 when a round found the app down or a part of it failing, as the card's day bars color it; a
    /// minute a round.
    /// </summary>
    public sealed record DayWithProblems(DateOnly Date, string State, int DownMinutes, int DegradedMinutes);

    public sealed record OngoingIncident(Guid Id, string? Title, DateTimeOffset Since);

    public sealed record ProjectDetail(Guid Id,
        string? Name,
        string? Address,
        string? ServerAddress,
        string Health,
        string? ConnectionProblem,
        string? TimeZone,
        OngoingIncident? OngoingIncident,
        string? InMaintenanceNow,
        DateTimeOffset? LastCheckedAt,
        int? ReportMs,
        string? ApplicationVersion,
        DateTimeOffset? LastDeployedAt,
        DateTimeOffset? LastRestartedAt,
        PartGroup[] Parts,
        CheckedFrom[] CheckedFrom);

    public sealed record PartGroup(string Category, Part[] Parts);

    /// <summary>
    /// <c>Message</c>: The app's own words about the part, such as why it fails.<br/>
    /// <c>Proves</c>: What a pass proves, in the app's words.<br/>
    /// <c>MeasuredAt</c>: When a result the app reuses was really measured; left out for one measured in the last round.<br/>
    /// <c>Since</c>: Since when the part has been in this status.
    /// </summary>
    public sealed record Part(string? Key,
        string Title,
        string Status,
        string? Message,
        string? Proves,
        int DurationMs,
        int? LimitMs,
        DateTimeOffset? MeasuredAt,
        DateTimeOffset? Since);

    /// <summary>
    /// <c>Answered</c>: For a location of the bitplatform Network: whether the home page answered its newest reading.
    /// </summary>
    public sealed record CheckedFrom(string Name,
        string? Code,
        string Reads,
        DateTimeOffset? NewestReadingAt,
        string? DataCenter,
        int? ResponseMs,
        int? ReportMs,
        bool? Answered);

    public sealed record IncidentList(Incident[] Incidents);

    /// <summary>
    /// <c>Impact</c>: Down, or Degraded when parts failed while the app worked; Operational for an announcement.<br/>
    /// <c>Parts</c>: What the app calls the parts it is about.<br/>
    /// <c>OpenedByBitFabric</c>: Opened from the checks, so it closes by itself once they pass again.<br/>
    /// <c>AfterDeploymentAt</c>: A deployment within 30 minutes before it began, its likeliest cause.<br/>
    /// <c>DuringMaintenance</c>: It began in a maintenance window, so nobody was alerted and it doesn't count against the SLA.<br/>
    /// <c>Updates</c>: Newest first.<br/>
    /// <c>WhoWasTold</c>: Every alert sent about it, newest first.<br/>
    /// <c>AgentPrompt</c>: Once it is over, a prompt for the team's AI agent to find the cause in the app's own logs.
    /// </summary>
    public sealed record Incident(Guid Id,
        ProjectRef Project,
        string Kind,
        string Impact,
        string? Title,
        bool Ongoing,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        double DurationMinutes,
        string[] Parts,
        bool OpenedByBitFabric,
        DateTimeOffset? AfterDeploymentAt,
        bool DuringMaintenance,
        string? AcknowledgedBy,
        DateTimeOffset? AcknowledgedAt,
        IncidentUpdate[] Updates,
        AlertSent[] WhoWasTold,
        string? AgentPrompt);

    public sealed record IncidentUpdate(DateTimeOffset At, string Phase, string? Text, string Author);

    /// <summary>
    /// <c>To</c>: The team member, or a webhook's host.<br/>
    /// <c>Problem</c>: Why it didn't arrive.
    /// </summary>
    public sealed record AlertSent(DateTimeOffset At, string Kind, string Channel, string? To, bool Delivered, string? Problem);

    /// <summary>
    /// <c>TimeZone</c>: The zone the days run midnight to midnight in.<br/>
    /// <c>WatchedSince</c>: Nothing before this was watched, so it is no data rather than uptime.<br/>
    /// <c>UptimePercent</c>: The app's, the average of its watched days.
    /// </summary>
    public sealed record History(ProjectRef Project,
        string TimeZone,
        DateTimeOffset From,
        DateTimeOffset To,
        DateTimeOffset WatchedSince,
        double? UptimePercent,
        HistoryDay[] Days,
        HistoryPart[] Parts,
        HistoryIncident[] Incidents,
        Start[] DeploymentsAndRestarts,
        Maintenance[] Maintenance);

    /// <summary>
    /// <c>State</c>: Ok, Degraded, Down, Maintenance (planned work, nothing wrong) or NoData (not watched yet, or for a part, not in the app's report that day).<br/>
    /// <c>Maintenance</c>: Maintenance was planned for some of the day.
    /// </summary>
    public sealed record HistoryDay(DateOnly Date, string State, double? UptimePercent, int DownMinutes, int DegradedMinutes, bool Maintenance);

    /// <summary>
    /// <c>ReportedFrom</c>: when the app's report first had this part within the span, so a part added later counts only from
    /// then; null when it had it from the start.<br/>
    /// <c>ReportedUntil</c>: when the report last had it, for a part taken out of the report; null while it still has it.
    /// </summary>
    public sealed record HistoryPart(string Key, string Title, double? UptimePercent, HistoryDay[] DaysWithTrouble, DateTimeOffset? ReportedFrom, DateTimeOffset? ReportedUntil);

    public sealed record HistoryIncident(Guid Id,
        string? Title,
        string Impact,
        DateTimeOffset StartedAt,
        DateTimeOffset? EndedAt,
        double DurationMinutes,
        string[] Parts,
        DateTimeOffset? AfterDeploymentAt,
        bool DuringMaintenance);

    /// <summary>
    /// <c>Kind</c>: Deployment, a new build, or Restart, the same build on a new start.
    /// </summary>
    public sealed record Start(DateTimeOffset At, string Kind, string? Version);

    public sealed record Maintenance(Guid Id, string? Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool RepeatsWeekly);

    /// <summary>
    /// <c>StartsAtInProjectTime</c>: The month's first midnight in the project's time zone, which a month follows.<br/>
    /// <c>AllowedDowntimeMinutes</c>: The weighted minutes of outage the target allows in the whole month.<br/>
    /// <c>DowntimeLeftMinutes</c>: What is left of them: the downtime still affordable this month.<br/>
    /// <c>PartialOutageWeight</c>: How much of a minute of partial outage counts as down, 0 to 1.<br/>
    /// <c>SlowPartWeight</c>: How much of a minute a part is slow but works counts as down, 0 to 1.<br/>
    /// <c>Credit</c>: The tier of the owner's contract the month falls into; left out when it falls into none, or none is set.<br/>
    /// <c>NoReadingMinutes</c>: Time bit Fabric couldn't read the app, such as while it needed reconnecting: not counted.
    /// </summary>
    public sealed record SlaReport(ProjectRef Project,
        string? TimeZone,
        int Year,
        int Month,
        DateTimeOffset StartsAt,
        DateTimeOffset EndsAt,
        DateTimeOffset StartsAtInProjectTime,
        DateTimeOffset EndsAtInProjectTime,
        bool IsCurrentMonth,
        double TargetPercent,
        double? AvailabilityPercent,
        bool? MeetsTarget,
        double AllowedDowntimeMinutes,
        double DowntimeLeftMinutes,
        double WeightedDowntimeMinutes,
        double DownMinutes,
        double PartialOutageMinutes,
        double CountedMinutes,
        double PartialOutageWeight,
        double SlowPartWeight,
        SlaCredit? Credit,
        SlaCredit[] CreditTiers,
        SlaPart[] Parts,
        double MaintenanceMinutes,
        double NoReadingMinutes,
        double NotWatchedMinutes,
        SlaMaintenance[] Maintenance,
        SlaMonth[] LastMonths);

    public sealed record SlaCredit(double BelowPercent, double CreditPercent);

    public sealed record SlaPart(string? Key, string? Title, double? AvailabilityPercent, double DownMinutes, double PartialOutageMinutes, double CountedMinutes);

    public sealed record SlaMaintenance(string? Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool RepeatsWeekly, double LeftOutMinutes);

    public sealed record SlaMonth(int Year, int Month, double? AvailabilityPercent, bool IsCurrent);

    /// <summary>
    /// <c>Address</c>: Where visitors read it, once it is published.<br/>
    /// <c>OwnHostName</c>: The team's own host name for it, such as status.theirapp.com, and whether Cloudflare made it active yet.<br/>
    /// <c>State</c>: Operational, Degraded, Down, or NoData: bit Fabric can't read the app right now, which the page says rather than look operational.
    /// </summary>
    public sealed record StatusPageReport(ProjectRef Project,
        bool Published,
        string? Address,
        string? OwnHostName,
        string? OwnHostNameState,
        string[] Languages,
        string State,
        bool InMaintenance,
        PageComponent[] Components,
        PageIncident[] Ongoing,
        int Followers,
        DateTimeOffset? CheckedAt);

    public sealed record PageComponent(string? Name, string State);

    public sealed record PageIncident(Guid Id, string? Title, string Impact, DateTimeOffset StartedAt);

    /// <summary>What an action did, and the incident or window as it is now.</summary>
    public sealed record Done(string Message, Incident? Incident = null, Maintenance? Maintenance = null);
}
