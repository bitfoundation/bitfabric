using System.Text.Json;
using Bit.Fabric.Shared.Features.Identity;
using Bit.Fabric.Shared.Features.Identity.Dtos;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Shared.Features.Tenants.Dtos;
using Microsoft.AspNetCore.Components.Authorization;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The stand-in for the app's <see cref="ITenantController"/>, for the projects page's first step: creating a team. The new
/// team has no project yet, and the page's host shows it; the app would then switch into it with a new token, which the
/// Workbench has no server to issue, so the call ends when the host replaces the page.
/// </summary>
public sealed class WorkbenchTenantController(WorkbenchScenarioState scenarioState, MonitoringSampleWorld world, AuthenticationStateProvider authenticationStateProvider)
    : MonitoringStandIn(scenarioState, world, authenticationStateProvider), ITenantController
{
    public async Task<TenantDto> Create(TenantDto request, CancellationToken cancellationToken)
    {
        await ScenarioState.Save(cancellationToken);

        World.CreateTeam();

        // The page that asked is replaced by the new team's, which cancels this.
        await Task.Delay(Timeout.Infinite, cancellationToken);

        return request;
    }

    public Task<TenantDto?> GetCurrentTenant(CancellationToken cancellationToken) => throw NotUsedByTheWorkbench();

    public Task<TenantDto> Update(TenantDto request, CancellationToken cancellationToken) => throw NotUsedByTheWorkbench();

    public Task InviteUser(InviteUserToTenantRequestDto request, CancellationToken cancellationToken) => throw NotUsedByTheWorkbench();
}

/// <summary>
/// The stand-in for the app's <see cref="IPushNotificationService"/>: the view's device allows notifications, unless its
/// host says the browser blocks them, and subscribing goes nowhere.
/// </summary>
public sealed class WorkbenchPushNotificationService : IPushNotificationService
{
    /// <summary>The browser blocks bit Fabric's notifications on this device.</summary>
    public bool IsBlocked { get; set; }

    public string? Token { get; set; }

    public Task<bool> IsAvailable(CancellationToken cancellationToken) => Task.FromResult(IsBlocked is false);

    public Task RequestPermission(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<Bit.Fabric.Shared.Features.PushNotification.PushNotificationSubscriptionDto?> GetSubscription(CancellationToken cancellationToken)
        => Task.FromResult<Bit.Fabric.Shared.Features.PushNotification.PushNotificationSubscriptionDto?>(null);

    public Task Subscribe(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task Unsubscribe(CancellationToken cancellationToken) => Task.CompletedTask;
}
