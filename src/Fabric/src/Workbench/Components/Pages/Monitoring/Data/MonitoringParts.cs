namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

/// <summary>
/// The entries a bit app's health report has, in the app's own words, as the template describes them
/// (src/Server/Bit.Fabric.Server.Shared/Infrastructure/HealthChecks/HealthCheckDescriptions.cs), plus a payment gateway an
/// app added of its own. The sample projects are made of these.
/// </summary>
public static class MonitoringParts
{
    /// <param name="Key">The entry's name in the report.</param>
    /// <param name="Title">What the app calls it.</param>
    /// <param name="IconName">Its BitIconName.</param>
    /// <param name="About">What a pass proves.</param>
    /// <param name="Category">One of the template's categories, or the app's own.</param>
    /// <param name="TimeoutMs">The entry's limit, when the app sets one.</param>
    /// <param name="TypicalMs">How long it usually takes.</param>
    /// <param name="CacheMinutes">How long the app reuses its result, for a check that costs a sign-in or money.</param>
    public sealed record Part(string Key, string Title, string IconName, string About, string Category, int? TimeoutMs, int TypicalMs, int? CacheMinutes = null);

    public static readonly Part[] All =
    [
        new("binStorage", "Disk space", "HardDrive", "At least 2 GB free on the instance that answered this report. The only liveness check.", "Platform", null, 1),
        new("AppDbContext", "Database", "Database", "Opens a connection to the app's database.", "Platform", null, 4),
        new("hangfire", "Background jobs", "Processing", "At least one Hangfire server is running.", "Platform", null, 3),
        new("appCertificate", "App certificate", "Certificate", "The certificate that signs tokens and protects data is within its validity period.", "Platform", null, 1),
        new("StackExchange.Redis_redis-persistent", "Redis (persistent)", "Server", "Hangfire storage and distributed locks.", "Platform", null, 2),
        new("StackExchange.Redis_redis-cache", "Redis (cache)", "ServerProcesses", "The distributed cache and backplane. Memory keeps serving without it.", "Platform", null, 2),
        new("userProfileImages", "File storage", "Photo2", "The storage container of the uploaded files.", "Platform", 5000, 46),
        new("cloudflare", "Cloudflare", "Globe", "The zones whose edge cache the app purges.", "Platform", 10000, 118, 5),
        new("backup", "Backups", "CloudUpload", "The backup job copied the database and the files within the last seven hours.", "Platform", 10000, 212, 5),
        new("smtp", "Email", "Mail", "Signs in to the SMTP server without sending anything.", "Messaging", 10000, 312, 5),
        new("emailDelivery", "Email sender domain", "MailCheck", "The sender's domain has SPF and DMARC records, so mail can reach inboxes rather than spam.", "Messaging", 10000, 64, 30),
        new("sms", "SMS", "CellPhone", "The Twilio account is active.", "Messaging", 5000, 186),
        new("firebase", "Android push", "Ringer", "Firebase Cloud Messaging accepts the credentials.", "Messaging", 5000, 141, 5),
        new("apns", "Apple push", "Ringer", "APNs accepts the credentials.", "Messaging", 5000, 158, 5),
        new("googleSignIn", "Sign in with Google", "Signin", "Can't be verified without a user; only reported while not configured.", "Identity", null, 0),
        new("gitHubSignIn", "Sign in with GitHub", "Signin", "Can't be verified without a user; only reported while not configured.", "Identity", null, 0),
        new("reCaptcha", "reCAPTCHA", "Shield", "Google's siteverify answers.", "Identity", 5000, 96, 5),
        new("aiChat", "AI chat", "ChatBot", "The chat model gives a short answer.", "AI", 20000, 812, 5),
        new("aiEmbedding", "Embeddings", "DocumentSearch", "One word gets embedded, as semantic search does.", "AI", 10000, 236, 5),
        new("aiRealtime", "Voice calls", "Headset", "The realtime model issues a session for the configured voice.", "AI", 10000, 420, 5),
        new("websites", "Websites", "Globe", "Each site in HealthChecks:Websites answers a GET with a success status. The data says what went wrong with each one that didn't.", "Websites", 12000, 380, 1),
        new("paymentGateway", "Payment gateway", "Money", "Authorizes and voids a one cent payment.", "Integrations", 8000, 540, 5)
    ];

    /// <summary>
    /// What lies behind an entry, as the template's report names it from the app's settings (HealthCheckDependencies in
    /// src/Boilerplate): the outside service, its host and an AI check's model. The payment gateway names its own.
    /// </summary>
    public static readonly Dictionary<string, (string Provider, string? Host, string? Model)> Behind = new()
    {
        ["userProfileImages"] = ("Amazon S3", "s3.eu-central-1.amazonaws.com", null),
        ["cloudflare"] = ("Cloudflare", "api.cloudflare.com", null),
        ["smtp"] = ("SMTP", "smtp.sendgrid.net", null),
        ["sms"] = ("Twilio", "api.twilio.com", null),
        ["firebase"] = ("Firebase Cloud Messaging", "fcm.googleapis.com", null),
        ["apns"] = ("Apple Push Notification service", "api.push.apple.com", null),
        ["reCaptcha"] = ("Google reCAPTCHA", "www.google.com", null),
        ["aiChat"] = ("OpenAI", "api.openai.com", "gpt-4.1-mini"),
        ["aiEmbedding"] = ("OpenAI", "api.openai.com", "text-embedding-3-small"),
        ["aiRealtime"] = ("OpenAI", "api.openai.com", "gpt-realtime"),
        ["paymentGateway"] = ("Stripe", "api.stripe.com", null)
    };

    /// <summary>The settings section a not-configured entry names, as the template's stand-in check reports it.</summary>
    public static readonly Dictionary<string, string> Settings = new()
    {
        ["sms"] = "Sms",
        ["googleSignIn"] = "Authentication:Google",
        ["gitHubSignIn"] = "Authentication:GitHub",
        ["cloudflare"] = "Cloudflare",
        ["backup"] = "ConnectionStrings:backups",
        ["aiRealtime"] = "AI:Realtime"
    };

    /// <summary>The parts without which the app doesn't answer: one of them failing is the app being down.</summary>
    public static readonly string[] Critical = ["binStorage", "AppDbContext"];

    public static Part Find(string key) => All.First(part => part.Key == key);

    public static string TitleOf(string key) => All.FirstOrDefault(part => part.Key == key)?.Title ?? key;
}
