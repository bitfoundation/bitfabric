namespace Bit.Fabric.Server.Api.Infrastructure.Emails;

/// <summary>
/// The inline styles every email shares (See EmailLayout). An email client reads no style sheet of the app's and many drop a
/// &lt;style&gt; block, so the light look lives in style attributes; the layout's &lt;style&gt; only adds dark mode and the
/// phone's padding for the clients that read it.
/// </summary>
public static class EmailStyles
{
    /// <summary>The app's own font first, then each system's, which covers Persian too.</summary>
    public const string Font = "Inter,-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,Tahoma,sans-serif";

    public const string Paragraph = "margin:0 0 16px; font-size:16px; line-height:1.6; color:#3A414B;";

    public const string Small = "margin:0; font-size:13px; line-height:1.6; color:#5F6773;";

    public const string Link = "color:#0065EF; text-decoration:underline;";

    /// <summary>The brand's blue, the logo's own.</summary>
    public const string Primary = "#0065EF";

    public static bool IsRightToLeft => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    /// <summary>The reading direction's start, as text-align takes it: Outlook's Word engine reads no "start".</summary>
    public static string Start => IsRightToLeft ? "right" : "left";

    public static string Direction => IsRightToLeft ? "rtl" : "ltr";
}
