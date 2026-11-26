namespace Boilerplate.Client.Core.Components.Pages.Settings;

public partial class TwoFactorSection
{
    private string? qrCode;
    private bool isWaiting;
    private bool isLoading = true;
    private string? sharedKey;
    private int recoveryCodesLeft;
    private bool isKeyCopiedShown;
    private bool isCodesCopiedShown;
    private string[]? recoveryCodes;
    private string? authenticatorUri;
    private string? verificationCode;
    private bool isTwoFactorAuthEnabled;


    [AutoInject] private Clipboard clipboard = default!;
    [AutoInject] private IUserController userController = default!;

    /// <summary>
    /// Where the user came from to turn two-factor authentication on, such as an OAuth consent that needs it
    /// (See ConsentPage). Honoured only when it is a url of this app.
    /// </summary>
    [Parameter, SupplyParameterFromQuery(Name = "return-url")]
    public string? ReturnUrl { get; set; }

    /// <summary>
    /// Whether this session signed in with two-factor authentication (<see cref="AuthPolicies.TFA_ENABLED"/>): turning it
    /// on does not add that to the session, which only a new sign-in with the second factor does.
    /// </summary>
    private bool isTwoFactorSession;


    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        isTwoFactorSession = await AuthorizationService.IsAuthorized((await AuthenticationStateTask).User, AuthPolicies.TFA_ENABLED);

        await SendTwoFactorAuthRequest(new());
    }

    private bool HasReturnUrl => Uri.IsAppRelativeUrl(ReturnUrl, requireLeadingSlash: false);

    private void GoBack() => NavigationManager.NavigateTo(ReturnUrl!);

    /// <summary>
    /// Signs out and back in, so the new session carries the second factor, landing where <see cref="ReturnUrl"/> says. The
    /// session would be signed out anyway on its next refresh: turning two-factor on signs out every session.
    /// </summary>
    private async Task SignInAgain()
    {
        await AuthManager.SignOut(CurrentCancellationToken);

        NavigationManager.NavigateTo($"{PageUrls.SignIn}?return-url={Uri.EscapeDataString(ReturnUrl!)}");
    }

    private async Task EnableTwoFactorAuth()
    {
        if (string.IsNullOrWhiteSpace(verificationCode))
        {
            SnackBarService.Error(Localizer[nameof(AppStrings.TfaVerificationCodeRequiredMessage)]);
            return;
        }

        // Strip spaces and hyphens
        var twoFactorCode = verificationCode.Replace(" ", string.Empty).Replace("-", string.Empty);

        var request = new TwoFactorAuthRequestDto { Enable = true, TwoFactorCode = twoFactorCode };
        var response = await SendTwoFactorAuthRequest(request);

        if (response is null) return; // The request failed and the error was already reported to the user.

        recoveryCodes = response.RecoveryCodes;
        SnackBarService.Success(Localizer[nameof(AppStrings.TwoFactorAuthenticationEnabled)]);
        // Enabling 2fa regenerates the security stamp on the server, so every active session gets signed out on its next token refresh.
        SnackBarService.Warning(Localizer[nameof(AppStrings.SignOutOfAllDevicesWarningMessage)]);
    }

    private async Task DisableTwoFactorAuth()
    {
        // Anything that weakens the second factor needs elevated access on the server (See UserController.TwoFactorAuth);
        // enabling does not, because it already requires a valid code from the authenticator.
        if (await AuthManager.TryEnterElevatedAccessMode(CurrentCancellationToken) is false) return;

        var request = new TwoFactorAuthRequestDto { Enable = false };
        var response = await SendTwoFactorAuthRequest(request);

        if (response is null) return; // The request failed and the error was already reported to the user.

        SnackBarService.Success(Localizer[nameof(AppStrings.TwoFactorAuthenticationDisabled)]);
        // Disabling 2fa regenerates the security stamp on the server, so every active session gets signed out on its next token refresh.
        SnackBarService.Warning(Localizer[nameof(AppStrings.SignOutOfAllDevicesWarningMessage)]);

        recoveryCodes = [];
    }

    private async Task GenerateRecoveryCode()
    {
        // Same elevated-access rule as DisableTwoFactorAuth.
        if (await AuthManager.TryEnterElevatedAccessMode(CurrentCancellationToken) is false) return;

        var request = new TwoFactorAuthRequestDto { ResetRecoveryCodes = true };
        var response = await SendTwoFactorAuthRequest(request);

        recoveryCodes = response?.RecoveryCodes;
    }

    private async Task ResetAuthenticatorKey()
    {
        // Same elevated-access rule as DisableTwoFactorAuth - resetting the shared key also turns 2fa off.
        if (await AuthManager.TryEnterElevatedAccessMode(CurrentCancellationToken) is false) return;

        var request = new TwoFactorAuthRequestDto { ResetSharedKey = true };
        var response = await SendTwoFactorAuthRequest(request);

        if (response is null) return; // The request failed and the error was already reported to the user.

        // Resetting the authenticator key (and disabling 2fa) regenerates the security stamp on the server,
        // so every active session gets signed out on its next token refresh.
        SnackBarService.Warning(Localizer[nameof(AppStrings.SignOutOfAllDevicesWarningMessage)]);
    }

    //private async Task ForgetMachine()
    //{
    //    var request = new TwoFactorAuthRequestDto { ForgetMachine = true };
    //    await SendTwoFactorAuthRequest(request);
    //}

    private async Task<TwoFactorAuthResponseDto?> SendTwoFactorAuthRequest(TwoFactorAuthRequestDto request)
    {
        if (isWaiting) return null;

        isWaiting = true;

        try
        {
            var response = await userController.TwoFactorAuth(request, CurrentCancellationToken);

            qrCode = response.QrCode;
            sharedKey = response.SharedKey;
            authenticatorUri = response.AuthenticatorUri;
            recoveryCodesLeft = response.RecoveryCodesLeft;
            isTwoFactorAuthEnabled = response.IsTwoFactorEnabled;
            isLoading = false;

            return response;
        }
        catch (KnownException e)
        {
            SnackBarService.Error(e.Message);
            return null;
        }
        finally
        {
            isWaiting = false;
        }
    }

    private async Task CopySharedKeyToClipboard()
    {
        if (isKeyCopiedShown) return;

        await clipboard.WriteText(sharedKey!);

        isKeyCopiedShown = true;
        StateHasChanged();

        await Task.Delay(1000);

        isKeyCopiedShown = false;
        StateHasChanged();
    }

    private async Task CopyRecoveryCodesToClipboard()
    {
        if (isCodesCopiedShown) return;

        await clipboard.WriteText(string.Join(Environment.NewLine, recoveryCodes ?? []));

        isCodesCopiedShown = true;
        StateHasChanged();

        await Task.Delay(1000);

        isCodesCopiedShown = false;
        StateHasChanged();
    }
}
