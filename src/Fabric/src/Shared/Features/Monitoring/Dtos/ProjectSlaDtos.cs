namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

/// <summary>A tier of the owner's contract with their customers: a month below <see cref="BelowPercent"/> gives <see cref="CreditPercent"/> back.</summary>
public partial class ProjectSlaCreditDto
{
    public double BelowPercent { get; set; }

    public double CreditPercent { get; set; }
}

/// <summary>
/// What the owner promises and how the SLA counts (bitfabric-internal/specs/monitoring.md, SLA reports): the target, the
/// weights of a partial outage and of a slow part, and the credit tiers of their contract. Defaults are 99.9%, 30% and
/// 0%, with no credit tiers until the owner enters theirs.
/// </summary>
public partial class ProjectSlaSettingsDto
{
    public const double DefaultTargetPercent = 99.9;

    public const double DefaultPartialWeight = 0.3;

    public const double DefaultDegradedWeight = 0;

    public Guid MonitoringId { get; set; }

    /// <summary>The availability promised a month, such as 99.9.</summary>
    public double TargetPercent { get; set; } = DefaultTargetPercent;

    /// <summary>How much of a minute of partial outage counts as down, 0 to 1.</summary>
    public double PartialWeight { get; set; } = DefaultPartialWeight;

    /// <summary>How much of a minute a part is slow but works counts as down, 0 to 1.</summary>
    public double DegradedWeight { get; set; } = DefaultDegradedWeight;

    /// <summary>Highest threshold first.</summary>
    public ProjectSlaCreditDto[] Credits { get; set; } = [];

    public long Version { get; set; }
}

public partial class ProjectSlaRequestDto
{
    public Guid MonitoringId { get; set; }

    /// <summary>The month's year in the project's time zone; null with <see cref="Month"/> for this month.</summary>
    public int? Year { get; set; }

    /// <summary>1 to 12.</summary>
    public int? Month { get; set; }
}

/// <summary>A month of the app, or of one of its parts, in minutes, and the availability they make with the report's weights.</summary>
public partial class ProjectSlaRowDto
{
    /// <summary>The part's report key; null for the app as a whole.</summary>
    public string? Key { get; set; }

    /// <summary>What the app calls the part, or the project's name.</summary>
    public string? Title { get; set; }

    /// <summary>A percentage; null when no minute of the month counted.</summary>
    public double? Availability { get; set; }

    /// <summary>The minutes the formula counts: watched, read, and outside maintenance.</summary>
    public double CountedMinutes { get; set; }

    public double DownMinutes { get; set; }

    public double PartialMinutes { get; set; }

    public double DegradedMinutes { get; set; }

    /// <summary>Down, plus partial and degraded each times its weight: what the error budget is spent on.</summary>
    public double WeightedMinutes { get; set; }
}

public partial class ProjectSlaMonthDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>The app's availability that month; null when it wasn't watched.</summary>
    public double? Availability { get; set; }

    public bool IsCurrent { get; set; }
}

/// <summary>A maintenance window's occurrence in the month, left out of the count.</summary>
public partial class ProjectSlaMaintenanceDto
{
    public string? Title { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool RepeatsWeekly { get; set; }

    /// <summary>The minutes of it the count left out: those of the month that bit Fabric watched and that have passed.</summary>
    public double Minutes { get; set; }
}

/// <summary>A month's SLA report (bitfabric-internal/specs/monitoring.md, SLA reports), on the SLA tab and in its CSV and PDF.</summary>
public partial class ProjectSlaReportDto
{
    public Guid MonitoringId { get; set; }

    public string? ProjectName { get; set; }

    public string? Address { get; set; }

    /// <summary>The IANA time zone the month runs in (bitfabric-internal/specs/monitoring.md, Times follow the viewer's time zone).</summary>
    public string? TimeZoneId { get; set; }

    public int Year { get; set; }

    public int Month { get; set; }

    /// <summary>Midnight of the month's first day in the project's time zone, as an instant.</summary>
    public DateTimeOffset From { get; set; }

    /// <summary>Midnight of the next month's first day in the project's time zone, as an instant.</summary>
    public DateTimeOffset To { get; set; }

    /// <summary>The month is still running, so its figures are so far.</summary>
    public bool IsCurrent { get; set; }

    public DateTimeOffset GeneratedAt { get; set; }

    /// <summary>The target, weights and credits the figures were counted with.</summary>
    public ProjectSlaSettingsDto Settings { get; set; } = new();

    /// <summary>The app as a whole, from its incidents: the month's figure.</summary>
    public ProjectSlaRowDto App { get; set; } = new();

    /// <summary>Each part of the app, from its own periods and the app's outages.</summary>
    public ProjectSlaRowDto[] Parts { get; set; } = [];

    /// <summary>The weighted minutes of outage the target allows in the month.</summary>
    public double BudgetMinutes { get; set; }

    /// <summary>What is left of <see cref="BudgetMinutes"/> after the app's weighted minutes; never below zero.</summary>
    public double LeftMinutes { get; set; }

    /// <summary>The credit tier the month falls into, if any.</summary>
    public ProjectSlaCreditDto? Credit { get; set; }

    /// <summary>The last 12 months, oldest first, this one last.</summary>
    public ProjectSlaMonthDto[] Months { get; set; } = [];

    public ProjectSlaMaintenanceDto[] Maintenance { get; set; } = [];

    /// <summary>Maintenance left out of the count, the incidents that began in it included.</summary>
    public double MaintenanceMinutes { get; set; }

    /// <summary>Time bit Fabric couldn't read the app's report, such as while it needed reconnecting.</summary>
    public double NoDataMinutes { get; set; }

    /// <summary>Time of the month before bit Fabric watched the app.</summary>
    public double NotWatchedMinutes { get; set; }
}
