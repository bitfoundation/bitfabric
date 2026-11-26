using Bit.Fabric.Shared.Features.Diagnostic;

namespace Bit.Fabric.Shared.Features.Monitoring.Dtos;

/// <summary>
/// A project's application map (bitfabric-internal/specs/monitoring.md, Application map): everything its last health report
/// says the app talks to, each part with how it's doing now and over the last 24 hours.
/// </summary>
public partial class ProjectMapDto
{
    /// <summary>When the newest report was read; null before the first one.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>How long the app's own page took in that round.</summary>
    public int? HomePageMs { get; set; }

    /// <summary>The parts of the app, by the report's categories, as its last report lists them.</summary>
    public ProjectMapPartDto[] Parts { get; set; } = [];
}

/// <summary>One thing the app depends on, such as its database or its email service, as its report names it.</summary>
public partial class ProjectMapPartDto
{
    /// <summary>The entry's name in the report, such as <c>smtp</c>.</summary>
    public string? Key { get; set; }

    /// <summary>What the app calls it, such as "Database", in its own words.</summary>
    public string? Title { get; set; }

    /// <summary>The kind of dependency, from the report: Platform, Messaging, Identity, AI, Integrations or the app's own.</summary>
    public string? Category { get; set; }

    /// <summary>The part's <c>BitIconName</c>, from the report.</summary>
    public string? IconName { get; set; }

    /// <summary>What a pass proves, from the report.</summary>
    public string? About { get; set; }

    /// <summary>
    /// What lies behind the part, such as "Twilio" behind "SMS": the outside service the app's report says the check talks
    /// to. Null when the report names none; bit Fabric never guesses it.
    /// </summary>
    public string? Provider { get; set; }

    /// <summary>The host the check reaches, from the report.</summary>
    public string? Host { get; set; }

    /// <summary>The model an AI check uses, from the report.</summary>
    public string? Model { get; set; }

    public HealthCheckStatus Status { get; set; }

    /// <summary>Its settings are missing: a setup finding, not an outage.</summary>
    public bool IsNotConfigured { get; set; }

    /// <summary>Since when it has been in this status.</summary>
    public DateTimeOffset? Since { get; set; }

    /// <summary>When the app last really ran it: the round's time, or earlier for a result the app reuses.</summary>
    public DateTimeOffset? CheckedAt { get; set; }

    /// <summary>How long its last real run took.</summary>
    public int? LastMs { get; set; }

    /// <summary>The average of its timed runs over the last 24 hours.</summary>
    public int? TypicalMs { get; set; }

    /// <summary>Its slowest run over the last 24 hours.</summary>
    public int? SlowestMs { get; set; }

    /// <summary>The rounds that looked at it over the last 24 hours.</summary>
    public int Rounds { get; set; }

    /// <summary>The rounds of the last 24 hours it failed or was degraded in.</summary>
    public int ProblemRounds { get; set; }

    /// <summary>The ongoing incident about it, if there is one.</summary>
    public Guid? OngoingIncidentId { get; set; }
}
