namespace Bit.Fabric.Client.Core.Components.Layout;

/// <summary>
/// Tells the app's users about an incident or a planned maintenance on the app's status page
/// (See <see cref="ClientCoreSettings.StatusPageSummaryUrl"/>): a card in the corner, a sheet at the bottom on a phone.
/// It asks a few seconds after the app opens, off the startup path; again when the app comes back after a while in the
/// background, and when a request to the app's own server fails. It doesn't poll. A closed notice comes back only for a
/// new incident, a new update or a maintenance that begins.
/// </summary>
public partial class StatusPageNotice
{
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromSeconds(5);

    /// <summary>How long the app has to be in the background, or since the last ask, before it asks again.</summary>
    public static readonly TimeSpan AskAgainAfter = TimeSpan.FromMinutes(5);

    public const string DismissedStorageKey = "status-notice-dismissed";

    [AutoInject] private Document document = default!;
    [AutoInject] private StatusPageService statusPageService = default!;

    private StatusNotice? notice;
    private DateTimeOffset? hiddenAt;
    private int checking;
    private Action? unsubscribe;
    private ButilSubscription? visibilitySubscription;

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        if (statusPageService.IsEnabled is false)
            return;

        // Not awaited: the first render, and everything the app does as it starts, goes on without it.
        _ = Start(CurrentCancellationToken);
    }

    private async Task Start(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(FirstCheckDelay, TimeProvider, cancellationToken);

            if (statusPageService.LastCheckedAt is { } lastCheckedAt && TimeProvider.GetUtcNow() - lastCheckedAt < AskAgainAfter)
            {
                await InvokeAsync(() => Show(statusPageService.LastNotice));
            }
            else
            {
                await InvokeAsync(() => Check(cancellationToken));
            }

            unsubscribe = PubSubService.Subscribe(ClientAppMessages.SERVER_REQUEST_FAILED, async _ => await InvokeAsync(() => CheckAgain(cancellationToken)));

            visibilitySubscription = await document.SubscribeVisibilityChange(state =>
            {
                var now = TimeProvider.GetUtcNow();

                if (state is VisibilityState.Hidden)
                {
                    hiddenAt = now;
                    return;
                }

                var awayLongEnough = hiddenAt is { } at && now - at >= AskAgainAfter;
                hiddenAt = null;

                if (awayLongEnough)
                {
                    _ = InvokeAsync(() => CheckAgain(cancellationToken));
                }
            });
        }
        catch (OperationCanceledException) { }
        catch (Exception exp)
        {
            ExceptionHandler.Handle(exp, displayKind: ExceptionDisplayKind.None);
        }
    }

    private async Task CheckAgain(CancellationToken cancellationToken)
    {
        if (statusPageService.LastCheckedAt is { } lastCheckedAt && TimeProvider.GetUtcNow() - lastCheckedAt < AskAgainAfter)
            return;

        await Check(cancellationToken);
    }

    private async Task Check(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref checking, 1) is 1)
            return;

        try
        {
            await Show(await statusPageService.GetNotice(cancellationToken));
        }
        catch (OperationCanceledException) { }
        catch (Exception exp)
        {
            ExceptionHandler.Handle(exp, displayKind: ExceptionDisplayKind.None);
        }
        finally
        {
            checking = 0;
        }
    }

    private async Task Show(StatusNotice? found)
    {
        if (found is not null && await StorageService.GetItem(DismissedStorageKey) == found.Key)
        {
            found = null;
        }

        notice = found;
        StateHasChanged();
    }

    private async Task Close()
    {
        if (notice is null)
            return;

        var key = notice.Key;
        notice = null;
        await StorageService.SetItem(DismissedStorageKey, key);
    }

    private string KindClass => notice?.Kind switch
    {
        StatusNoticeKind.Outage => "outage",
        StatusNoticeKind.PartialOutage => "partial",
        _ => "maintenance"
    };

    private string IconName => notice?.Kind switch
    {
        StatusNoticeKind.Outage => BitIconName.ErrorBadge,
        StatusNoticeKind.PartialOutage => BitIconName.Warning,
        _ => BitIconName.Repair
    };

    private BitColor IconColor => notice?.Kind switch
    {
        StatusNoticeKind.Outage => BitColor.Error,
        StatusNoticeKind.PartialOutage => BitColor.Warning,
        _ => BitColor.Primary
    };

    private string Title => notice?.Title ?? (notice?.Kind is StatusNoticeKind.Maintenance
        ? Localizer[nameof(AppStrings.StatusNoticeMaintenanceTitle)]
        : Localizer[nameof(AppStrings.StatusNoticeIncidentTitle)]);

    private string[] Phases =>
    [
        Localizer[nameof(AppStrings.StatusNoticeInvestigating)],
        Localizer[nameof(AppStrings.StatusNoticeIdentified)],
        Localizer[nameof(AppStrings.StatusNoticeMonitoring)],
        Localizer[nameof(AppStrings.StatusNoticeResolved)]
    ];

    private string? When
    {
        get
        {
            if (notice is null)
                return null;

            if (notice.Kind is StatusNoticeKind.Maintenance)
            {
                return (notice.InProgress, notice.StartsAt, notice.EndsAt) switch
                {
                    (true, _, { } endsAt) => Localizer[nameof(AppStrings.StatusNoticeMaintenanceUntil), Time(endsAt)],
                    (false, { } startsAt, { } endsAt) => Localizer[nameof(AppStrings.StatusNoticeMaintenanceFromTo), Time(startsAt), Time(endsAt, sameDayAs: startsAt)],
                    (false, { } startsAt, null) => Time(startsAt),
                    _ => null
                };
            }

            return (notice.StartsAt, notice.UpdatedAt) switch
            {
                ({ } startsAt, { } updatedAt) when updatedAt - startsAt >= TimeSpan.FromMinutes(1) => Localizer[nameof(AppStrings.StatusNoticeStartedAndUpdated), Ago(startsAt), Ago(updatedAt)],
                ({ } startsAt, _) => Localizer[nameof(AppStrings.StatusNoticeStarted), Ago(startsAt)],
                _ => (string?)null
            };
        }
    }

    private string Ago(DateTimeOffset at)
    {
        var elapsed = TimeProvider.GetUtcNow() - at;

        if (elapsed < TimeSpan.FromMinutes(1))
            return Localizer[nameof(AppStrings.StatusNoticeJustNow)];

        if (elapsed < TimeSpan.FromHours(1))
            return Localizer[nameof(AppStrings.StatusNoticeMinutesAgo), (int)elapsed.TotalMinutes];

        if (elapsed < TimeSpan.FromDays(1))
            return Localizer[nameof(AppStrings.StatusNoticeHoursAgo), (int)elapsed.TotalHours];

        return Time(at);
    }

    /// <summary>
    /// The time alone today - or on the day of <paramref name="sameDayAs"/> - and with its date on another day, in the
    /// user's time zone.
    /// </summary>
    private string Time(DateTimeOffset at, DateTimeOffset? sameDayAs = null)
    {
        var local = TimeZoneService.ToLocalTime(at);
        var day = TimeZoneService.ToLocalTime(sameDayAs ?? TimeProvider.GetUtcNow()).Date;

        return local.ToString(local.Date == day ? "t" : "g", CultureInfo.CurrentCulture);
    }

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        unsubscribe?.Invoke();

        if (visibilitySubscription is not null)
        {
            try
            {
                await visibilitySubscription.DisposeAsync();
            }
            catch (JSDisconnectedException) { } // The circuit is gone, and the page's listener with it.
        }

        await base.DisposeAsync(disposing);
    }
}
