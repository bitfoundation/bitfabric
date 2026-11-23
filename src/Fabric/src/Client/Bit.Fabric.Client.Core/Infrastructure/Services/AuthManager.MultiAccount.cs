using System.Text.Json.Serialization.Metadata;
using Bit.Fabric.Shared.Features.Tenants.Dtos;

namespace Bit.Fabric.Client.Core.Infrastructure.Services;

public partial class AuthManager
{
    public async Task SignOutAll(CancellationToken cancellationToken)
    {
        var otherUserSessions = await GetOtherUserSessions();

        foreach (var userSession in otherUserSessions)
        {
            await RemoveUserSession(userSession.UserId);
        }

        await Task.WhenAll(otherUserSessions.Select(userSession => RevokeQuietly(userSession.RefreshToken)));

        await SignOut(activateNextAccount: false, cancellationToken);
    }

    public async Task<DeviceUserSessionDto[]> GetOtherUserSessions()
    {
        var activeUserId = GetUserId(await storageService.GetItem("access_token"));

        List<DeviceUserSessionDto> userSessions = [];

        foreach (var persistent in (bool[])[true, false])
        {
            userSessions.AddRange(await ReadUserSessions(persistent));
        }

        return [.. userSessions.Where(userSession => userSession.UserId != activeUserId)
                               .OrderBy(userSession => userSession.DisplayName, StringComparer.CurrentCultureIgnoreCase)];
    }

    public async Task<Guid[]> GetDeviceSessionIds()
    {
        var sessionIds = (await GetOtherUserSessions()).Select(userSession => userSession.SessionId).Append(GetSessionId(await tokenProvider.GetAccessToken()));

        return [.. sessionIds.OfType<Guid>()];
    }

    public async Task<bool> SwitchAccount(Guid userId, string? pin = null)
    {
        if (GetUserId(await storageService.GetItem("access_token")) == userId)
            return true;

        var (userSession, persistent) = await FindUserSession(userId);

        if (userSession is null)
            return false;

        var tokens = await OpenTokens(userSession, pin);

        if (tokens is null)
        {
            if (userSession.HasPin && string.IsNullOrEmpty(pin) is false)
            {
                await CountFailedPinAttempt(userSession, persistent);
            }

            return false;
        }

        await KeepActiveUserSession();

        await RemoveUserSession(userId);

        await WriteActiveTokens(tokens.AccessToken, tokens.RefreshToken, persistent);

        await WritePinLock(userSession.PinLock, persistent);

        activeAccountProfile = userSession;

        await NotifyActiveAccountChanged(softRestartOnSwitch: true);

        return true;
    }

    public ValueTask<bool> IsPinSupported() => pinCipher.IsSupported();

    public async Task<bool> HasPin()
    {
        return await ReadPinLock() is not null;
    }

    public async Task SetPin(string pin)
    {
        if (string.IsNullOrWhiteSpace(await storageService.GetItem("refresh_token")))
            throw new UnauthorizedException(localizer[nameof(AppStrings.YouNeedToSignIn)]);

        await WritePinLock(await pinCipher.CreateLock(pin), await storageService.IsPersistent("refresh_token"));
    }

    public async Task Lock()
    {
        if (await ReadPinLock() is null)
            throw new InvalidOperationException("The active account has no PIN to lock it with.");

        await KeepActiveUserSession();

        await ClearTokens(activateNextAccount: false);
    }

    private DeviceUserSessionDto? activeAccountProfile;

    public void SetActiveAccountProfile(UserDto user)
    {
        activeAccountProfile = new()
        {
            UserId = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FullName = user.FullName,
            HasProfileImage = user.HasProfileImage,
            Version = user.Version
        };
    }

    private const string ActiveAccountProfileKey = "active_account_profile";
    private const string ActiveAccountTenantKey = "active_account_tenant";

    /// <summary>
    /// What this device last had of <paramref name="authUser"/>'s profile and tenant, for the app to show at start
    /// until the server answers who the user is (See <see cref="StoreActiveAccount"/>). Nothing that belongs to another
    /// user or another tenant comes back.
    /// </summary>
    public async Task<(UserDto? User, TenantDto? Tenant)> GetStoredActiveAccount(ClaimsPrincipal authUser)
    {
        if (GetUserId(authUser) is not Guid userId)
            return default;

        // A switch to another account of this device has its profile already (See SwitchAccount).
        var user = activeAccountProfile is { } profile && profile.UserId == userId
            ? new UserDto
            {
                Id = profile.UserId,
                UserName = profile.UserName,
                Email = profile.Email,
                PhoneNumber = profile.PhoneNumber,
                FullName = profile.FullName,
                HasProfileImage = profile.HasProfileImage,
                Version = profile.Version
            }
            : await ReadStored(ActiveAccountProfileKey, IdentityJsonContext.Default.UserDto);

        if (user?.Id != userId)
            return default;

        var tenant = await ReadStored(ActiveAccountTenantKey, AppJsonContext.Default.TenantDto);

        return (user, tenant is not null && tenant.Id == authUser.GetTenantId() ? tenant : null);
    }

    /// <summary>
    /// Keeps the active account's profile and tenant next to its tokens, with the persistence the tokens have, so the
    /// next start can show them before the server answers. <see cref="ClearTokens"/> removes them with the tokens.
    /// </summary>
    public async Task StoreActiveAccount(UserDto user, TenantDto? tenant)
    {
        SetActiveAccountProfile(user);

        // An answer that arrives after its account was signed out of, or switched away from, must not bring it back.
        if (GetUserId(await storageService.GetItem("access_token")) != user.Id)
            return;

        var persistent = await storageService.IsPersistent("refresh_token");

        await storageService.SetItem(ActiveAccountProfileKey, JsonSerializer.Serialize(user, IdentityJsonContext.Default.UserDto), persistent);

        if (tenant is null)
        {
            await storageService.RemoveItem(ActiveAccountTenantKey);
        }
        else
        {
            await storageService.SetItem(ActiveAccountTenantKey, JsonSerializer.Serialize(tenant, AppJsonContext.Default.TenantDto), persistent);
        }
    }

    private async Task RemoveStoredActiveAccount()
    {
        await storageService.RemoveItem(ActiveAccountProfileKey);
        await storageService.RemoveItem(ActiveAccountTenantKey);
    }

    private async Task<T?> ReadStored<T>(string key, JsonTypeInfo<T> typeInfo)
        where T : class
    {
        var json = await storageService.GetItem(key);

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, typeInfo);
        }
        catch (JsonException exp)
        {
            // Written by an older version of the app, perhaps. The server's answer replaces it anyway.
            authLogger.LogWarning(exp, "The stored {Key} could not be read.", key);
            return null;
        }
    }

    private static Guid? GetUserId(string? accessToken)
    {
        return GetUserId(IAuthTokenProvider.ParseAccessToken(accessToken, validateExpiry: false));
    }

    private static Guid? GetUserId(ClaimsPrincipal user)
    {
        return Guid.TryParse((user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("nameid"))?.Value, out var userId) ? userId : null;
    }

    // False until the first GetAuthenticationStateAsync records the user the app started with.
    private bool isNotifiedUserKnown;
    private Guid? notifiedUserId;
    private Guid? notifiedTenantId;

    private async Task NotifyActiveAccountChanged(bool softRestartOnSwitch)
    {
        var previousUserId = notifiedUserId;
        var previousTenantId = notifiedTenantId;

        var state = await GetAuthenticationStateAsync();

        if (GetUserId(state.User) is Guid userId)
        {
            notifiedUserId = userId;

            var isSwitch = previousUserId is not null && previousUserId != userId;
            notifiedTenantId = state.User.GetTenantId();
            // A tenant switch keeps the user id, so the check above misses it, yet every page loaded before it shows
            // the previous tenant's data while its next requests already go out with the new tenant's token.
            // The tenant id is compared rather than the token, so a plain refresh in another tab restarts nothing.
            // previousUserId == userId also rules out the first notification, when nothing was known yet.
            isSwitch |= previousUserId == userId && previousTenantId != notifiedTenantId;

            if (softRestartOnSwitch && isSwitch)
            {
                accessTokenTsc = null;
                pubSubService.Publish(ClientAppMessages.SOFT_RESTART);
            }
        }

        NotifyAuthenticationStateChanged(Task.FromResult(state));
    }

    private async Task KeepActiveUserSession()
    {
        var accessToken = await storageService.GetItem("access_token");
        var refreshToken = await storageService.GetItem("refresh_token");
        var user = IAuthTokenProvider.ParseAccessToken(accessToken, validateExpiry: false);

        if (GetUserId(user) is not Guid userId || string.IsNullOrWhiteSpace(refreshToken))
            return;

        var profile = activeAccountProfile?.UserId == userId ? activeAccountProfile : null;

        DeviceUserSessionDto userSession = new()
        {
            UserId = userId,
            UserName = profile is null ? user.Identity?.Name : profile.UserName,
            Email = profile is null ? user.GetEmail() : profile.Email,
            PhoneNumber = profile?.PhoneNumber,
            FullName = profile?.FullName,
            HasProfileImage = profile?.HasProfileImage ?? false,
            Version = profile?.Version
        };

        await SetUserSessionTokens(userSession, accessToken, refreshToken, await ReadPinLock());

        await AddUserSession(userSession, await storageService.IsPersistent("refresh_token"));
    }

    private async Task<bool> StoreRefreshedTokens(string? sentRefreshToken, TokenResponseDto refreshTokenResponse)
    {
        if (await storageService.GetItem("refresh_token") == sentRefreshToken || await IsCurrentSession(refreshTokenResponse.AccessToken))
        {
            await StoreTokens(refreshTokenResponse);
            return true;
        }

        if (GetUserId(refreshTokenResponse.AccessToken) is not Guid userId)
            return false;

        var (userSession, persistent) = await FindUserSession(userId);

        if (userSession?.SessionId is not Guid sessionId || sessionId != GetSessionId(refreshTokenResponse.AccessToken))
            return false;

        await SetUserSessionTokens(userSession, refreshTokenResponse.AccessToken, refreshTokenResponse.RefreshToken, userSession.PinLock);

        await AddUserSession(userSession, persistent);

        return false;
    }

    private async Task SetUserSessionTokens(DeviceUserSessionDto userSession, string? accessToken, string? refreshToken, DevicePinLockDto? pinLock)
    {
        userSession.SessionId = GetSessionId(accessToken);
        userSession.RefreshTokenExpiresOn = GetExpiry(refreshToken);
        userSession.PinLock = pinLock;

        if (pinLock is null)
        {
            userSession.AccessToken = accessToken;
            userSession.RefreshToken = refreshToken;
            userSession.SealedTokens = null;
            return;
        }

        var tokens = new TokenResponseDto { AccessToken = accessToken, RefreshToken = refreshToken };

        userSession.AccessToken = userSession.RefreshToken = null;
        userSession.SealedTokens = await pinCipher.Seal(pinLock, JsonSerializer.SerializeToUtf8Bytes(tokens, IdentityJsonContext.Default.TokenResponseDto));
    }

    private async Task<TokenResponseDto?> OpenTokens(DeviceUserSessionDto userSession, string? pin)
    {
        if (userSession.PinLock is null)
            return new() { AccessToken = userSession.AccessToken, RefreshToken = userSession.RefreshToken };

        if (string.IsNullOrEmpty(pin) || userSession.SealedTokens is null)
            return null;

        var tokens = await pinCipher.Open(userSession.PinLock, pin, userSession.SealedTokens);

        return tokens is null ? null : JsonSerializer.Deserialize(tokens, IdentityJsonContext.Default.TokenResponseDto);
    }

    private async Task CountFailedPinAttempt(DeviceUserSessionDto userSession, bool persistent)
    {
        userSession.FailedPinAttempts++;

        if (userSession.FailedPinAttempts >= AccountPinCipher.MaxFailedAttempts)
        {
            await RemoveUserSession(userSession.UserId);
            return;
        }

        await AddUserSession(userSession, persistent);
    }

    private async Task<DevicePinLockDto?> ReadPinLock()
    {
        var json = await storageService.GetItem("pin_lock");

        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize(json, IdentityJsonContext.Default.DevicePinLockDto);
        }
        catch (JsonException exp)
        {
            authLogger.LogWarning(exp, "Failed to read the PIN of the active account.");
            return null;
        }
    }

    private async Task WritePinLock(DevicePinLockDto? pinLock, bool persistent)
    {
        if (pinLock is null)
        {
            await storageService.RemoveItem("pin_lock");
            return;
        }

        await storageService.SetItem("pin_lock", JsonSerializer.Serialize(pinLock, IdentityJsonContext.Default.DevicePinLockDto), persistent);
    }

    private async Task ForgetUserSession(string? refreshToken)
    {
        if (GetSessionId(refreshToken) is Guid sessionId
            && (await GetOtherUserSessions()).FirstOrDefault(userSession => userSession.SessionId == sessionId) is { } userSession)
        {
            await RemoveUserSession(userSession.UserId);
        }
    }

    private async Task<DeviceUserSessionDto?> TakeUserSession(Guid userId)
    {
        var (userSession, _) = await FindUserSession(userId);

        await RemoveUserSession(userId);

        return userSession;
    }

    private async Task<(DeviceUserSessionDto? UserSession, bool Persistent)> FindUserSession(Guid userId)
    {
        foreach (var persistent in (bool[])[true, false])
        {
            if ((await ReadUserSessions(persistent)).FirstOrDefault(userSession => userSession.UserId == userId) is { } userSession)
                return (userSession, persistent);
        }

        return (null, false);
    }

    private async Task AddUserSession(DeviceUserSessionDto userSession, bool persistent)
    {
        await RemoveUserSession(userSession.UserId);

        await WriteUserSessions(persistent, [.. await ReadUserSessions(persistent), userSession]);
    }

    private async Task RemoveUserSession(Guid userId)
    {
        foreach (var persistent in (bool[])[true, false])
        {
            var userSessions = await ReadUserSessions(persistent);

            if (userSessions.RemoveAll(userSession => userSession.UserId == userId) > 0)
            {
                await WriteUserSessions(persistent, userSessions);
            }
        }
    }

    private async Task<List<DeviceUserSessionDto>> ReadUserSessions(bool persistent)
    {
        var json = await storageService.GetItem(persistent ? "user_sessions" : "temp_user_sessions");

        if (string.IsNullOrWhiteSpace(json))
            return [];

        List<DeviceUserSessionDto> userSessions;

        try
        {
            userSessions = JsonSerializer.Deserialize(json, IdentityJsonContext.Default.ListDeviceUserSessionDto) ?? [];
        }
        catch (JsonException exp)
        {
            authLogger.LogWarning(exp, "Failed to read the user sessions stored on this device.");
            userSessions = [];
        }

        var now = TimeProvider.GetUtcNow();

        if (userSessions.RemoveAll(userSession => userSession.RefreshTokenExpiresOn is not DateTimeOffset expiresOn || expiresOn <= now) > 0)
        {
            await WriteUserSessions(persistent, userSessions);
        }

        return userSessions;
    }

    private async Task WriteUserSessions(bool persistent, List<DeviceUserSessionDto> userSessions)
    {
        var key = persistent ? "user_sessions" : "temp_user_sessions";

        if (userSessions.Count is 0)
        {
            await storageService.RemoveItem(key);
            return;
        }

        await storageService.SetItem(key, JsonSerializer.Serialize(userSessions, IdentityJsonContext.Default.ListDeviceUserSessionDto), persistent);
    }

    private static DateTimeOffset? GetExpiry(string? token)
    {
        return long.TryParse(IAuthTokenProvider.ParseAccessToken(token, validateExpiry: false).FindFirst("exp")?.Value, out var expSeconds)
            ? DateTimeOffset.FromUnixTimeSeconds(expSeconds)
            : null;
    }

    private async Task RevokeQuietly(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            return;

        try
        {
            await identityController.RevokeRefreshToken(new() { RefreshToken = refreshToken }, CancellationToken.None);
        }
        catch (Exception exp)
        {
            authLogger.LogWarning(exp, "Failed to end the session of an account that left the device.");
        }
    }
}
