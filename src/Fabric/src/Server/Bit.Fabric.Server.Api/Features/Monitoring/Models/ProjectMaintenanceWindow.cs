using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// A time the customer declared for work on their app (bitfabric-internal/specs/monitoring.md, Maintenance windows): bit
/// Fabric keeps checking, but alerts nobody, and the time doesn't count against the SLA. One that repeats weekly recurs at
/// the same time of the week as its first occurrence, in UTC.
/// </summary>
public partial class ProjectMaintenanceWindow : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string? Title { get; set; }

    public DateTimeOffset StartsAt { get; set; }

    public DateTimeOffset EndsAt { get; set; }

    public bool RepeatsWeekly { get; set; }

    public long Version { get; set; }

    /// <summary>Whether <paramref name="at"/> falls in this window, or, for a weekly one, in any week's occurrence of it.</summary>
    public bool Covers(DateTimeOffset at)
    {
        if (at >= StartsAt && at < EndsAt)
            return true;

        if (RepeatsWeekly is false || at < StartsAt)
            return false;

        var week = TimeSpan.FromDays(7);
        var sinceStart = (at - StartsAt).Ticks % week.Ticks;

        return sinceStart < (EndsAt - StartsAt).Ticks;
    }
}
