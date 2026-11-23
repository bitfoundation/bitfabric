using Bit.Fabric.Server.Api.Features.PushNotification;
using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

/// <summary>
/// A project's public status page (bitfabric-internal/specs/monitoring.md, Status page): at
/// <c>bitfabric.co/status/{slug}</c>, and at the customer's own host name once Cloudflare for SaaS has issued its
/// certificate. It shows the parts of the app as its users know them (See <see cref="StatusComponent"/>), the incidents,
/// the maintenance and the last 90 days, in the visitor's language.
/// </summary>
public partial class StatusPage : ITenantAware
{
    public Guid Id { get; set; }

    public Guid ProjectId { get; set; }

    public Project? Project { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    /// <summary>Unique across bit Fabric: the page is at <c>/status/{slug}</c>.</summary>
    [Required, MaxLength(60)]
    public string? Slug { get; set; }

    /// <summary>Visitors see it only once the owner publishes it.</summary>
    public bool IsPublished { get; set; }

    public bool ShowsResponseTime { get; set; }

    /// <summary>The languages the owner writes updates in, the main one first; visitors can pick any of bit Fabric's.</summary>
    public string[] Languages { get; set; } = [];

    /// <summary>An email address or a web address where visitors reach the app's support.</summary>
    [MaxLength(200)]
    public string? SupportAddress { get; set; }

    /// <summary>Such as <c>status.myapp.com</c>, which the customer points at bit Fabric with a CNAME.</summary>
    [MaxLength(253)]
    public string? CustomHostname { get; set; }

    /// <summary>Cloudflare's id of the custom host name, to read its state and remove it.</summary>
    [MaxLength(64)]
    public string? CustomHostnameId { get; set; }

    /// <summary>Cloudflare's state of the host name and its certificate, such as "pending" or "active".</summary>
    [MaxLength(64)]
    public string? CustomHostnameState { get; set; }

    public List<StatusComponent> Components { get; set; } = [];

    public long Version { get; set; }
}

/// <summary>
/// A part of the app as its users know it, such as "Sign-in" or "Payments", made of one or more health report entries;
/// its name is the owner's, in each language they write in.
/// </summary>
public partial class StatusComponent : ITenantAware
{
    public Guid Id { get; set; }

    public Guid StatusPageId { get; set; }

    public StatusPage? StatusPage { get; set; }

    public Guid TenantId { get; set; }

    public Tenant? Tenant { get; set; }

    public int Order { get; set; }

    public List<ProjectText> Name { get; set; } = [];

    public List<ProjectText> Description { get; set; } = [];

    /// <summary>The report entries it is made of; an incident about one of them is about this component.</summary>
    public string[] CheckKeys { get; set; } = [];

    public bool IsShown { get; set; } = true;

    public long Version { get; set; }
}

/// <summary>
/// A visitor following a status page by push (bitfabric-internal/specs/monitoring.md, Subscribe): a row of the template's
/// push subscriptions, related to the pages it follows, with the language its pushes are written in.
/// </summary>
public partial class StatusPageFollower
{
    public Guid Id { get; set; }

    public Guid StatusPageId { get; set; }

    public StatusPage? StatusPage { get; set; }

    public int PushNotificationSubscriptionId { get; set; }

    public PushNotificationSubscription? PushNotificationSubscription { get; set; }

    [Required, MaxLength(10)]
    public string? Culture { get; set; }

    /// <summary>The components followed; empty for all of them.</summary>
    public Guid[] ComponentIds { get; set; } = [];

    public DateTimeOffset FollowedAt { get; set; }
}
