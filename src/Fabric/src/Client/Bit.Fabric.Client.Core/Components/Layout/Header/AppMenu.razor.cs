using Bit.Fabric.Shared.Features.PushNotification;
using Bit.Fabric.Shared.Features.Tenants.Dtos;

namespace Bit.Fabric.Client.Core.Components.Layout.Header;

public partial class AppMenu
{
    [CascadingParameter] public BitDir? CurrentDir { get; set; }

    [CascadingParameter] public UserDto? CurrentUser { get; set; }

    [CascadingParameter] public AppThemeType? CurrentTheme { get; set; }

    /// <summary>
    /// <inheritdoc cref="Parameters.IsOnline"/>
    /// </summary>
    [CascadingParameter(Name = Parameters.IsOnline)] public bool? IsOnline { get; set; }


    [AutoInject] private ThemeService themeService = default!;
    [AutoInject] private CultureService cultureService = default!;
    [AutoInject] private IUserController userController = default!;
    [AutoInject] private SignInModalService signInModalService = default!;
    [AutoInject] private AccountSwitchService accountSwitchService = default!;
    [AutoInject] private IPushNotificationService pushNotificationService = default!;
    [AutoInject] private IPushNotificationController pushNotificationController = default!;
    [AutoInject] private NotificationPreferenceService notificationPreferenceService = default!;


    private bool isOpen;
    private bool showCultures;
    private bool isPinSupported;
    private bool isSignOutConfirmOpen;
    private bool isSignOutConfirmRendered;
    private DeviceUserSessionDto[] otherUserSessions = [];
    private BitChoiceGroupItem<string>[] cultures = default!;
    private bool notificationsEnabled;
    private bool pushNotificationsBlocked;
    private bool showTenants;
    private string? currentTenantId;
    private BitChoiceGroupItem<string>[] tenants = [];
    private HashSet<string> pendingInvitationTenantIds = [];

    private bool ShowMainMenu =>
        showCultures is false
        && showTenants is false
        ;


    private string? ProfileImageUrl => CurrentUser?.GetProfileImageUrl(AbsoluteServerAddress);

    private string AccountMenuLabel => CurrentUser?.DisplayName ?? Localizer[nameof(AppStrings.AccountMenuTitle)].Value;

    private string ThemeToggleLabel => CurrentTheme == AppThemeType.Light
        ? Localizer[nameof(AppStrings.SwitchToDarkThemeTitle)]
        : Localizer[nameof(AppStrings.SwitchToLightThemeTitle)];


    /// <summary>
    /// MainLayout cascades CurrentUser, so each of its renders would re-render this menu as well.
    /// </summary>
    protected override object? GetParametersRenderKey()
        => (CurrentDir, CurrentTheme, IsOnline, AuthenticationStateTask,
            CurrentUser?.Id, CurrentUser?.DisplayName, CurrentUser?.Email, CurrentUser?.PhoneNumber, CurrentUser?.HasProfileImage, CurrentUser?.Version);

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        NavigationManager.LocationChanged += NavigationManager_LocationChanged;
        AuthManager.AuthenticationStateChanged += AuthManager_AuthenticationStateChanged;

        if (CultureInfoManager.InvariantGlobalization is false)
        {
            cultures = CultureInfoManager.SupportedCultures
                              .Select(sc => new BitChoiceGroupItem<string> { Value = sc.Culture.Name, Text = sc.DisplayName })
                              .ToArray();
        }
    }

    private void NavigationManager_LocationChanged(object? sender, LocationChangedEventArgs e)
    {
        // The sign-in and sign-up buttons href are bound to NavigationManager.GetRelativePath().
        // To ensure the bound values update with each route change, it's necessary to call StateHasChanged on location changes.
        StateHasChanged();
    }

    private async Task OnCultureChanged(string? cultureName)
    {
        await cultureService.ChangeCulture(cultureName);
    }

    private bool ShowNotificationsToggle
    {
        get
        {
            var show = AppPlatform.IsWindows is false; // Push is not implemented on Windows.
            show = true; // In-app messages reach every platform, and a signed out choice applies at the next sign-in.
            return show;
        }
    }

    private string NotificationsToggleLabel => notificationsEnabled
        ? Localizer[nameof(AppStrings.TurnNotificationsOff)].Value
        : Localizer[nameof(AppStrings.TurnNotificationsOn)].Value;

    /// <summary>
    /// The switch shows the device's choice, which covers in-app messages too, so a push the platform refuses is
    /// called out under it rather than turning it off.
    /// </summary>
    private async Task RefreshNotificationsState()
    {
        notificationsEnabled = await notificationPreferenceService.IsEnabled();
        pushNotificationsBlocked = notificationsEnabled
                                   && AppPlatform.IsWindows is false
                                   && await pushNotificationService.IsAvailable(CurrentCancellationToken) is false;

        StateHasChanged();
    }

    private async Task ToggleNotifications()
    {
        var enable = notificationsEnabled is false;

        if (enable)
        {
            // Asked first, so the prompt is still tied to the click that got us here.
            if (AppPlatform.IsWindows is false)
            {
                await pushNotificationService.RequestPermission(CurrentCancellationToken);
            }
        }

        // Stored either way: the permission decides whether a push can be delivered, not whether the user wants
        // notifications, and in-app messages need no permission at all.
        await notificationPreferenceService.SetEnabled(enable);

        if (enable)
        {
            await pushNotificationService.Subscribe(CurrentCancellationToken);
        }
        else
        {
            await pushNotificationService.Unsubscribe(CurrentCancellationToken);
        }

        await RefreshNotificationsState();

        await ConfirmNotifications(enable);
    }

    /// <summary>
    /// Tells the server what the switch now says, and the server answers with the welcome notification that proves
    /// the setup works. A later sign-in on this device gets the same choice through UpdateSession, silently.
    /// </summary>
    private async Task ConfirmNotifications(bool enabled)
    {
        var user = (await AuthenticationStateTask).User;

        if (user.IsAuthenticated())
        {
            await userController.SetNotificationEnabled(enabled, CurrentCancellationToken);
            return;
        }

        // Signed out there is no session to store the choice on, and turning the switch off has just deleted the
        // subscription, so only an enable the platform accepted has anything left to say. Windows has no push to test.
        if (enabled is false || pushNotificationsBlocked || AppPlatform.IsWindows)
            return;

        var subscription = await pushNotificationService.GetSubscription(CurrentCancellationToken);

        if (subscription?.DeviceId is null)
            return;

        await pushNotificationController.TestPushNotificationSetup(subscription, CurrentCancellationToken);
    }

    private async Task OnTimeZoneChanged(string? timeZoneId)
    {
        if (string.IsNullOrEmpty(timeZoneId) || BitTimeZones.Find(timeZoneId) == BitTimeZones.Find(TimeZoneService.CurrentTimeZone)) return;

        await TimeZoneService.ChangeTimeZone(timeZoneId);
    }

    private async Task ShowTenants()
    {
        showTenants = true;

        var user = (await AuthenticationStateTask).User;
        currentTenantId = user.GetTenantId()?.ToString();

        // GetTenants also returns tenants the user has only been INVITED to, and switching into one of those is what
        // accepts the invitation (See IdentityController.Refresh) - so they have to be told apart in the list rather
        // than offered as if they were memberships. A pending invitation is only relevant for regular users; a global
        // admin is listed every active tenant and always just switches (See ManageMyTenantsPage.IsPendingInvitation).
        var isGlobalAdmin = await AuthorizationService.IsAuthorized(user, AppFeatures.Management.Tenants_Manage_Global);

        var userTenants = await userController.GetTenants(CurrentCancellationToken);

        pendingInvitationTenantIds = isGlobalAdmin
            ? []
            : [.. userTenants.Where(t => t.CurrentUserMembershipStatus is TenantMembershipStatus.Invited).Select(t => t.Id.ToString())];

        tenants = [.. userTenants.Select(t => new BitChoiceGroupItem<string> { Value = t.Id.ToString(), Text = t.Title ?? t.Name })];
    }

    private bool IsPendingInvitation(string? tenantId) => tenantId is not null && pendingInvitationTenantIds.Contains(tenantId);

    private async Task OnTenantChanged(string? tenantId)
    {
        if (Guid.TryParse(tenantId, out var newTenantId) is false || tenantId == currentTenantId)
            return;

        CloseMenu(); // A switch that fails leaves the menu closed rather than sitting on the tenant panel behind the error.

        // Switching calls the refresh token api that stores the new tenant id in the token's claims (See IdentityController.Refresh).
        if (await AuthManager.SwitchTenant(newTenantId, CurrentCancellationToken))
        {
            // Rebuilds everything the previous tenant's data reached: the current page, the tenant shown in the
            // nav panel, this menu itself.
            PubSubService.Publish(ClientAppMessages.SOFT_RESTART);
        }
    }

    private async Task ToggleTheme()
    {
        await themeService.ToggleTheme();
    }

    private async Task GoToProfile()
    {
        CloseMenu();
        NavigationManager.NavigateTo($"{PageUrls.Settings}/{PageUrls.SettingsSections.Profile}");
    }

    /// <summary>
    /// Re-reads whatever the menu shows that can go stale while it is closed. The notification permission is the one
    /// that matters: it changes in the browser's or the OS's own settings, and nothing tells the app when. It is
    /// first read here too, rather than after the menu's first render, which falls in the app's start.
    /// </summary>
    private async Task OnDropMenuOpen()
    {
        otherUserSessions = await AuthManager.GetOtherUserSessions();
        isPinSupported = await AuthManager.IsPinSupported();

        await RefreshNotificationsState();
    }

    /// <summary>
    /// Closes the menu and resets its sub panels, so it reopens on the main menu rather than on whichever
    /// sub panel was open when it was closed. BitDropMenu only raises OnDismiss when it closes itself
    /// (a click on the overlay or on the trigger); assigning the bound IsOpen from code does not.
    /// </summary>
    private void CloseMenu()
    {
        isOpen = false;
        OnDropMenuDismiss();
    }

    private void OnDropMenuDismiss()
    {
        showCultures = false;
        showTenants = false;
    }

    private void AuthManager_AuthenticationStateChanged(Task<AuthenticationState> task)
    {
        _ = InvokeAsync(() =>
        {
            showTenants = false; // This would help refreshing the list of tenants, so they would get loaded again the next time user opens the tenant menu.
            StateHasChanged();
        });
    }


    protected override async ValueTask DisposeAsync(bool disposing)
    {
        NavigationManager.LocationChanged -= NavigationManager_LocationChanged;
        AuthManager.AuthenticationStateChanged -= AuthManager_AuthenticationStateChanged;

        await base.DisposeAsync(disposing);
    }

    private async Task ModalSignIn()
    {
        CloseMenu();
        await signInModalService.SignIn();
    }

    private async Task SwitchAccount(DeviceUserSessionDto userSession)
    {
        CloseMenu();
        await accountSwitchService.Switch(userSession);
    }

    private async Task Lock()
    {
        CloseMenu();
        await accountSwitchService.Lock(CurrentCancellationToken);
    }

    private async Task AddAccount()
    {
        CloseMenu();

        if (await signInModalService.SignIn())
        {
            PubSubService.Publish(ClientAppMessages.SOFT_RESTART);
        }
    }
}
