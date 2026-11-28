using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>An address of the add flow's samples and what bit Fabric finds there.</summary>
public sealed record AddressSample(string Host, AddressProbeOutcome Outcome, string? AppTitle = null, string? ServerHost = null, bool HasStaticFrontEnd = false, string? TemplateVersion = "11.0.0");

public static partial class MonitoringSamples
{
    /// <summary>
    /// The addresses the add flow's cases type: a bit app found at its address, one whose pages are hosted statically and
    /// whose report is on its server, one that doesn't say its template version, a site that isn't a bit app, and one that
    /// doesn't answer. Any other address is a bit app named after its host, so typing one works too.
    /// </summary>
    public static readonly AddressSample[] Addresses =
    [
        new("taskbloom.app", AddressProbeOutcome.Found, "Taskbloom"),
        new("shopwave.store", AddressProbeOutcome.Found, "Shopwave", "api.shopwave.store", HasStaticFrontEnd: true, TemplateVersion: "10.7.0"),
        new("menuly.app", AddressProbeOutcome.Found, "Menuly", TemplateVersion: null),
        new("myblog.com", AddressProbeOutcome.NotFound),
        new("staging.taskbloom.app", AddressProbeOutcome.Unreachable)
    ];

    /// <summary>The host of an address as the customer typed it, or null when it isn't one.</summary>
    public static string? HostOf(string? typed)
    {
        var text = typed?.Trim();

        if (string.IsNullOrWhiteSpace(text))
            return null;

        if (text.Contains("://", StringComparison.Ordinal) is false)
        {
            text = $"https://{text}";
        }

        return Uri.TryCreate(text, UriKind.Absolute, out var uri) && uri.Host.Contains('.') ? uri.Authority.ToLowerInvariant() : null;
    }

    /// <summary>What the add flow finds at an address: one of the samples, a project already watched, or a bit app named after its host.</summary>
    public static AddressProbeResultDto Probe(string? typed, IEnumerable<SampleProject> watched)
    {
        if (HostOf(typed) is not { } host)
            return new() { Outcome = AddressProbeOutcome.Unreachable, Address = typed?.Trim() };

        var address = $"https://{host}";

        if (watched.FirstOrDefault(project => string.Equals(new Uri(project.Address).Authority, host, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            return new()
            {
                Outcome = AddressProbeOutcome.Found,
                Address = existing.Address,
                ServerAddress = existing.ServerAddress ?? existing.Address,
                HasStaticFrontEnd = existing.HasStaticFrontEnd,
                TemplateVersion = existing.TemplateVersion,
                AppTitle = existing.Name,
                IsAlreadyAdded = true,
                WatchedProjectName = existing.Name,
                WatchedEnvironment = existing.Environment
            };
        }

        var sample = Addresses.FirstOrDefault(sample => sample.Host == host)
                     ?? new AddressSample(host, AddressProbeOutcome.Found, char.ToUpperInvariant(host[0]) + host.Split('.')[0][1..]);

        return sample.Outcome is AddressProbeOutcome.Found
            ? new()
            {
                Outcome = AddressProbeOutcome.Found,
                Address = address,
                ServerAddress = sample.ServerHost is null ? address : $"https://{sample.ServerHost}",
                HasStaticFrontEnd = sample.HasStaticFrontEnd,
                TemplateVersion = sample.TemplateVersion,
                AppTitle = sample.AppTitle
            }
            : new() { Outcome = sample.Outcome, Address = address };
    }

    /// <summary>
    /// A project the add flow just connected: its report read once from Germany, the Network's readings still to come in, its
    /// first scans waiting, a status page not published yet, and a push to whoever added it as its first alert route.
    /// </summary>
    public static SampleProject NewlyConnected(DateTimeOffset now, AddressProbeResultDto found, string? name)
    {
        var host = new Uri(found.Address!).Authority;
        var key = host.Split('.')[0];
        var serverHost = found.ServerAddress is { } server && server != found.Address ? new Uri(server).Authority : null;

        var project = NewProject(now, key, string.IsNullOrWhiteSpace(name) ? found.AppTitle ?? host : name.Trim(), host, "Europe/Berlin", 0, serverHost, templateVersion: found.TemplateVersion ?? "11.0.0");
        project.HasStaticFrontEnd = found.HasStaticFrontEnd;
        project.ConnectionStatus = ProjectConnectionStatus.FirstChecks;
        project.CheckedAt = now;
        AddParts(project, now, [.. usualParts.Where(part => part is not "backup" and not "cloudflare")]);
        AddNotConfigured(project, "googleSignIn");
        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));
        AddReadings(project, now);
        project.ReadingsIn = 0;

        SetStatusPage(project, published: false, followers: 0, support: null, hostname: null, hostnameState: null,
        [
            ("Website", "وب‌سایت", ["binStorage", "AppDbContext", "userProfileImages"]),
            ("Email", "ایمیل", ["smtp", "emailDelivery"])
        ]);

        AddPendingScans(project, now, now, ProjectScanTrigger.Connected, running: 1);

        return Finish(project, now);
    }
}
