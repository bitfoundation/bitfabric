using System.ComponentModel;

namespace Bit.Fabric.Shared;

public static partial class PageUrls
{
    [NoIndex]
    public const string NotAuthorized = "/not-authorized";

    [Description("Verify your email address or phone number after sign-up (usually reached automatically).")]
    [NoIndex]
    public const string Confirm = "/confirm";

    [Description("Start the password reset flow by requesting a reset code.")]
    [NoIndex]
    public const string ForgotPassword = "/forgot-password";

    [Description("Set a new password using a reset code.")]
    [NoIndex]
    public const string ResetPassword = "/reset-password";

    [Description("Sign in to your account.")]
    [NotInSitemap]
    public const string SignIn = "/sign-in";

    [Description("Create a new account.")]
    [NotInSitemap]
    public const string SignUp = "/sign-up";
}
