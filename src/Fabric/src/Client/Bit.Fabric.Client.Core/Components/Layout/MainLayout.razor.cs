using Bit.BevTools;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Shared.Features.Tenants.Dtos;

namespace Bit.Fabric.Client.Core.Components.Layout;

public partial class MainLayout : IAsyncDisposable
{
    private static readonly BitModalParameters ModalParameters = new() { Classes = new() { Root = "modal" } };

    [CascadingParameter] public Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;


    [AutoInject] private Dom dom = default!;
    [AutoInject] private Document document = default!;
    [AutoInject] private Keyboard keyboard = default!;
    [AutoInject] private BevToolsLoader bevToolsLoader = default!;
    [AutoInject] private AuthManager authManager = default!;
    [AutoInject] private ThemeService themeService = default!;
    [AutoInject] private BitThemeNotifications bitThemeNotifications = default!;
    [AutoInject] private PubSubService pubSubService = default!;
    [AutoInject] private IUserController userController = default!;
    [AutoInject] private ITenantController tenantController = default!;
    [AutoInject] private BitExtraServices bitExtraServices = default!;
    [AutoInject] private ClientExceptionHandlerBase exceptionHandler = default!;
    [AutoInject] private ITelemetryContext telemetryContext = default!;
    [AutoInject] private JsonSerializerOptions jsonSerializerOptions = default!;


    /// <summary>
    /// <inheritdoc cref="Parameters.IsOnline"/>
    /// </summary>
    [PersistentState] public bool? IsOnline { get; set; }

    private BitDir? currentDir;
    private bool? isIdentityPage;
    private UserDto? currentUser;
    private TenantDto? currentTenant;
    private AppThemeType? currentTheme;
    private RouteData? currentRouteData;
    private List<Action> unsubscribers = [];
    private CancellationTokenSource getCurrentUserCts = new();
    /// <summary>What <see cref="SetCurrentUser"/> loads last, which coming back online waits for rather than restarts.</summary>
    private Task currentUserLoads = Task.CompletedTask;
    private bool isReloadingCurrentUser;
    /// <summary>The app came back online again while <see cref="ReloadCurrentUser"/> ran, so it checks again once it's done.</summary>
    private bool isCurrentUserRecheckPending;

    /// <summary>
    /// Bumped by <see cref="ClientAppMessages.PROFILE_UPDATED"/>, whose payload can be the same <see cref="currentUser"/>
    /// instance changed in place (See ProfileSection), so a reference comparison wouldn't see the change.
    /// </summary>
    private int currentUserVersion;
    private object? renderedState;


    /// <summary>
    /// Compares everything this layout renders or cascades with what it last rendered. Each render of the layout offers new
    /// parameters to AppShell, AppMenu and AppAiChatPanel, so a render that would only repeat what is on screen is skipped.
    /// </summary>
    protected override bool ShouldRender()
    {
        object state = (Body, currentTenant, currentDir, currentUser, currentUserVersion, currentTheme, currentRouteData, IsOnline, isIdentityPage, navPanelItems);

        if (Equals(state, renderedState))
            return false;

        renderedState = state;
        return true;
    }


    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        SetCurrentDir();

        try
        {
            var inPrerenderSession = RendererInfo.IsInteractive is false;

            // During pre-rendering, if any API calls are made, the `isOnline` value will be set 
            // using PubSub's `ClientAppMessages.IS_ONLINE_CHANGED`, depending on the success 
            // or failure of the API call. However, if a pre-rendered page has no HTTP API call 
            // dependencies, its value remains null. 
            // Even though Server.Web and Server.Api may be deployed on different servers, 
            // we can still assume that if the client is displaying a pre-rendered result, it is online.
            if (inPrerenderSession)
            {
                IsOnline ??= true;
            }

            authManager.AuthenticationStateChanged += AuthManager_AuthenticationStateChanged;
            bitThemeNotifications.ThemeChanged += BitThemeNotifications_ThemeChanged;

            unsubscribers.Add(pubSubService.Subscribe(ClientAppMessages.THEME_CHANGED, async payload =>
            {
                if (payload is null) return;
                currentTheme = (AppThemeType)payload;
                StateHasChanged();
            }));

            unsubscribers.Add(pubSubService.Subscribe(ClientAppMessages.ROUTE_DATA_UPDATED, async payload =>
            {
                currentRouteData = (RouteData?)payload;
                SetRouteData();
                StateHasChanged();
            }));

            unsubscribers.Add(pubSubService.Subscribe(ClientAppMessages.IS_ONLINE_CHANGED, async payload =>
            {
                if (IsOnline == (bool?)payload)
                    return;
                var wasOffline = IsOnline is false;
                telemetryContext.IsOnline = IsOnline = (bool?)payload;
                await InvokeAsync(StateHasChanged);

                // Back online after the server couldn't be reached: what failed to load then, such as the signed-in
                // user's profile when the app was opened offline, would stay missing until the app is reloaded.
                // From offline only, as the first answer at start comes while the profile is still on its way.
                if (wasOffline && IsOnline is true)
                {
                    await InvokeAsync(ReloadCurrentUser);
                }
            }));

            unsubscribers.Add(pubSubService.Subscribe(ClientAppMessages.PROFILE_UPDATED, async payload =>
            {
                if (payload is null) return;

                var updatedUser = payload is JsonElement jsonDocument
                    ? jsonDocument.Deserialize(jsonSerializerOptions.GetTypeInfo<UserDto>())! /* Message gets published from server through SignalR */
                    : (UserDto)payload;

                if (updatedUser.Id != currentUser?.Id) return;

                currentUser = updatedUser;
                currentUserVersion++;
                await authManager.StoreActiveAccount(updatedUser, currentTenant);

                await InvokeAsync(StateHasChanged);
            }));

            await Task.WhenAll(SetCurrentUser(AuthenticationStateTask), SetCurrentThemeAndRootCssClasses());
        }
        catch (Exception exp)
        {
            exceptionHandler.Handle(exp);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        await base.OnAfterRenderAsync(firstRender);

        if (firstRender)
        {
            await keyboard.Add(ButilKeyCodes.KeyD, OpenDevTools, ButilModifiers.Ctrl | ButilModifiers.Shift);

            // Stamps the booted culture onto <html> for the static hosts, whose index.html carries no lang/dir;
            // a no-op re-stamp on Server.Web, where App.razor already rendered the same values.
            await ApplyCultureToDocument();
        }
    }


    private async void AuthManager_AuthenticationStateChanged(Task<AuthenticationState> task)
    {
        try
        {
            await SetCurrentUser(task);
        }
        catch (Exception ex)
        {
            exceptionHandler.Handle(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async void BitThemeNotifications_ThemeChanged(object? sender, BitThemeChangedEventArgs e)
    {
        try
        {
            if (e.HasNewTheme is false) return;

            await InvokeAsync(() => themeService.ApplyTheme(e.NewTheme));
        }
        catch (Exception ex)
        {
            exceptionHandler.Handle(ex);
        }
    }

    private async Task SetCurrentThemeAndRootCssClasses()
    {
        currentTheme = await themeService.GetCurrentTheme();

        await bitExtraServices.AddRootCssClasses();
    }

    /// <summary>
    /// Loads again what <see cref="SetCurrentUser"/> couldn't load while the server was out of reach; it loads only what's
    /// missing. A load still on its way is waited for rather than restarted, as cancelling it would tell the app it's offline
    /// again. Coming back online once more while this runs, as when the profile's call fails and the tenant's then goes
    /// through, has it check again once it's done.
    /// </summary>
    private async Task ReloadCurrentUser()
    {
        if (isReloadingCurrentUser)
        {
            isCurrentUserRecheckPending = true;
            return;
        }

        isReloadingCurrentUser = true;
        try
        {
            do
            {
                isCurrentUserRecheckPending = false;

                try
                {
                    await currentUserLoads;
                }
                catch (Exception)
                {
                    // Handled where it was started; what it couldn't load is loaded below
                }

                try
                {
                    await SetCurrentUser(authManager.GetAuthenticationStateAsync());
                }
                catch (Exception exp)
                {
                    exceptionHandler.Handle(exp);
                }
            }
            while (isCurrentUserRecheckPending && IsOnline is true);
        }
        finally
        {
            isReloadingCurrentUser = false;
            isCurrentUserRecheckPending = false;
        }

        StateHasChanged();
    }

    private ClaimsPrincipal? lastAuthUser;
    private async Task SetCurrentUser(Task<AuthenticationState> task)
    {
        var authUser = (await task).User;

        await SetNavPanelItems(authUser);

        using var currentCts = getCurrentUserCts;
        getCurrentUserCts = new();
        await currentCts.TryCancel();

        if (authUser.IsAuthenticated() is false)
        {
            currentUser = null;
            currentTenant = null;
        }
        else
        {
            var showsStoredAccount = false;

            // What this device kept of the account is shown until the server answers below, rather than nobody, and the
            // server's answer always replaces it.
            if (currentUser is null && await authManager.GetStoredActiveAccount(authUser) is { User: not null } stored)
            {
                (currentUser, currentTenant) = stored;
                showsStoredAccount = true;
                await InvokeAsync(StateHasChanged);
            }

            Task[] loads =
            [
                LoadCurrentUser(authUser, showsStoredAccount, getCurrentUserCts.Token),
                SetCurrentTenantIfNeeded(authUser.GetTenantId(), showsStoredAccount, getCurrentUserCts.Token),
            ];

            await (currentUserLoads = Task.WhenAll(loads));

            await authManager.StoreActiveAccount(currentUser!, currentTenant);
        }

        lastAuthUser = authUser;
    }

    private async Task LoadCurrentUser(ClaimsPrincipal authUser, bool showsStoredAccount, CancellationToken cancellationToken)
    {
        if (currentUser is null || showsStoredAccount || authUser.IsTheSame(lastAuthUser) is false)
        {
            currentUser = await userController.GetCurrentUser(cancellationToken);
            authManager.SetActiveAccountProfile(currentUser);
        }
    }

    /// <summary>
    /// Resolves the tenant the user is currently signed into (shown next to the app version in AppShell).
    /// Switching, signing in/out and leaving a tenant all change the tenant claim, so re-resolving here keeps the display in sync.
    /// </summary>
    private async Task SetCurrentTenantIfNeeded(Guid? tenantId, bool showsStoredAccount, CancellationToken cancellationToken)
    {
        if (tenantId is null)
        {
            currentTenant = null;
            return;
        }

        if (currentTenant?.Id == tenantId && showsStoredAccount is false) return;

        currentTenant = await tenantController.GetCurrentTenant(cancellationToken);
    }

    private void SetCurrentDir()
    {
        currentDir = CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitDir.Rtl : null;
    }

    /// <summary>
    /// Stamps the current culture's name and directionality onto the &lt;html&gt; element, so assistive technologies,
    /// css <c>:lang()</c> rules and the document's layout direction follow the rendered language.
    /// </summary>
    private async Task ApplyCultureToDocument()
    {
        if (CultureInfoManager.InvariantGlobalization || RendererInfo.IsInteractive is false) return;

        var culture = CultureInfo.CurrentUICulture;

        await using var html = await dom.DocumentElement();
        if (html is not null)
        {
            await html.SetAttribute("lang", culture.Name);
        }

        await document.SetDir(culture.TextInfo.IsRightToLeft ? DocumentDir.Rtl : DocumentDir.Ltr);
    }

    private void SetRouteData()
    {
        if (currentRouteData is null)
        {
            isIdentityPage = false;
            return;
        }

        var type = currentRouteData.PageType;

        if (type.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any())
        {
            isIdentityPage = false;
            return;
        }

        if (type.Namespace?.Contains("Client.Core.Components.Pages.Identity") is true)
        {
            isIdentityPage = true;
            pubSubService.Publish(ClientAppMessages.CLOSE_NAV_PANEL);
            return;
        }

        isIdentityPage = false;
    }

    /// <summary>
    /// BevTools' own Ctrl+Shift+D, for the time before it is attached: a browser outside Development loads it on its first
    /// open, and until then nothing of it listens. Once it runs, it answers the shortcut itself, toggling the panel.
    /// </summary>
    private void OpenDevTools()
    {
        if (bevToolsLoader.IsActive) return;

        pubSubService.Publish(ClientAppMessages.SHOW_DEV_TOOLS);
    }


    public async ValueTask DisposeAsync()
    {
        if (getCurrentUserCts is not null)
        {
            await getCurrentUserCts.TryCancel();
        }

        authManager.AuthenticationStateChanged -= AuthManager_AuthenticationStateChanged;
        bitThemeNotifications.ThemeChanged -= BitThemeNotifications_ThemeChanged;

        unsubscribers.ForEach(us => us.Invoke());

        if (keyboard is not null)
        {
            await keyboard.DisposeAsync();
        }
    }
}
