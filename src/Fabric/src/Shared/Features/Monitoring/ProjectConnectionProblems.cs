namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// Why bit Fabric can't read a project anymore (MonitoringDto.ConnectionProblem). The probe stores a stable code, with what
/// the app said when it said something, and every reader gets it in their own language at read time: the card, the
/// project's page, an alert and an AI agent alike. A reason stored before the codes, an English sentence, reads as it was
/// written until the next round stores its code.
/// </summary>
public static class ProjectConnectionProblems
{
    /// <summary>The project has no approval at all.</summary>
    public const string NoApproval = nameof(NoApproval);

    /// <summary>The approval ran out with nothing to renew it with.</summary>
    public const string ApprovalExpired = nameof(ApprovalExpired);

    /// <summary>The app answered 401 to a fresh token.</summary>
    public const string Refused = nameof(Refused);

    /// <summary>The app answered 403: whoever approved bit Fabric lost the feature behind its health report.</summary>
    public const string ApproverLostAccess = nameof(ApproverLostAccess);

    /// <summary>The app refused to renew the approval; the detail is its OAuth error code.</summary>
    public const string RenewRefused = nameof(RenewRefused);

    /// <summary>The app no longer accepts the approval; the detail is the reason it gave, in its own words.</summary>
    public const string NoLongerAccepted = nameof(NoLongerAccepted);

    /// <summary>The app answered invalid_grant: it doesn't know the approval anymore, withdrawn or its data reset.</summary>
    public const string ApprovalWithdrawn = nameof(ApprovalWithdrawn);

    /// <summary>
    /// For an hour, the app's token endpoint or its <c>/healthz</c> answered 404: it no longer serves bit Fabric's report,
    /// which a reconnect won't bring back until its code does.
    /// </summary>
    public const string ReportGone = nameof(ReportGone);

    /// <summary>For an hour, renewing the approval met a server error or no answer; the detail is the last one seen.</summary>
    public const string ServerError = nameof(ServerError);

    /// <summary>
    /// For an hour, <c>/healthz/v1</c> failed (a server error, no answer, or something that isn't a report) while the public
    /// <c>/health</c> said the app is up: its report is broken, not the app. The detail is the last failure seen.
    /// </summary>
    public const string ReportFailing = nameof(ReportFailing);

    /// <summary>The code to store, with its detail after a colon.</summary>
    public static string Of(string code, string? detail = null) => detail is null ? code : $"{code}:{detail}";

    /// <summary>The code of a stored reason, without its detail.</summary>
    public static string? CodeOf(string? stored) => stored?.Split(':', 2)[0];

    /// <summary>The stored reason in the localizer's language.</summary>
    public static string? Describe(IStringLocalizer localizer, string? stored) => Describe(stored, (key, arguments) => localizer[key, arguments]);

    /// <summary>The stored reason in the current UI culture, for code that has no localizer, such as a mapper.</summary>
    public static string? Describe(string? stored) => Describe(stored, (key, arguments) =>
        string.Format(CultureInfo.CurrentCulture, AppStrings.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key, arguments));

    /// <summary>The stored reason in <paramref name="culture"/>, such as English for a prompt an AI reads.</summary>
    public static string? Describe(string? stored, CultureInfo culture) => Describe(stored, (key, arguments) =>
        string.Format(culture, AppStrings.ResourceManager.GetString(key, culture) ?? key, arguments));

    private static string? Describe(string? stored, Func<string, object[], string> text)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return stored;

        var separator = stored.IndexOf(':');
        var (code, detail) = separator < 0 ? (stored, "") : (stored[..separator], stored[(separator + 1)..]);

        return code switch
        {
            NoApproval => text(nameof(AppStrings.MonitoringConnectionNoApproval), []),
            ApprovalExpired => text(nameof(AppStrings.MonitoringConnectionApprovalExpired), []),
            Refused => text(nameof(AppStrings.MonitoringConnectionRefused), []),
            ApproverLostAccess => text(nameof(AppStrings.MonitoringConnectionApproverLostAccess), []),
            RenewRefused => text(nameof(AppStrings.MonitoringConnectionRenewRefused), [detail]),
            NoLongerAccepted => text(nameof(AppStrings.MonitoringConnectionNoLongerAccepted), [detail]),
            ApprovalWithdrawn => text(nameof(AppStrings.MonitoringConnectionApprovalWithdrawn), []),
            ReportGone => text(nameof(AppStrings.MonitoringConnectionReportGone), []),
            ServerError => text(nameof(AppStrings.MonitoringConnectionServerError), [detail]),
            ReportFailing => text(nameof(AppStrings.MonitoringConnectionReportFailing), [detail]),
            _ => stored
        };
    }
}
