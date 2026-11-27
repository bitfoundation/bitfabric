using Bit.Fabric.Shared.Features.Diagnostic;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Workbench.Components.Pages.Monitoring.Data;

public static partial class MonitoringSamples
{
    /// <summary>An online shop, down right now: its database stopped answering three minutes after a deployment, and nobody is on it yet.</summary>
    private static SampleProject Brightcart(DateTimeOffset now)
    {
        var project = NewProject(now, "brightcart", "Brightcart", "brightcart.shop", "America/New_York", 140, hasIcon: true, templateVersion: "10.7.0", baseMs: 240);
        // Its pages come from Cloudflare's cache, so visitors wait far less than its server takes.
        project.VisitorBaseMs = 46;
        project.Locations = ["wnam", "enam", "weur"];
        AddParts(project, now, [.. usualParts, "paymentGateway"]);

        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Email, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Webhook, address: "https://hooks.slack.com/services/T0B2C4D6E/B07H8J9K1/brightcart-alerts", format: ProjectWebhookFormat.Slack);
        Route(project, 1, 10, ProjectAlertChannel.Sms, Member("Daniel"));
        Route(project, 1, 10, ProjectAlertChannel.Webhook, address: "https://discord.com/api/webhooks/1187/old-channel", format: ProjectWebhookFormat.Discord);
        Route(project, 2, 30, ProjectAlertChannel.Sms, Member("Sara"));
        Route(project, 2, 30, ProjectAlertChannel.Webhook, address: "https://ops.brightcart.shop/hooks/bitfabric", format: ProjectWebhookFormat.Json);

        var deployedAt = now.AddMinutes(-15);
        Deploy(project, DaysAgo(now, 23, 14, 10), 3);
        Deploy(project, DaysAgo(now, 9, 16, 40), 2);
        Restart(project, DaysAgo(now, 6, 4, 12), 2);
        Deploy(project, deployedAt, 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;

        Fail(project, "AppDbContext", HealthCheckStatus.Unhealthy, now.AddMinutes(-12), null, durationMs: 15000);
        project.HomePageMs = null;
        AutoIncident(project, "database", ProjectHealth.Down, now.AddMinutes(-11), null, ["AppDbContext"], afterDeployAt: deployedAt, now: now);

        AutoIncident(project, "not-answering", ProjectHealth.Down, DaysAgo(now, 23, 14, 31), DaysAgo(now, 23, 14, 49), [], afterDeployAt: DaysAgo(now, 23, 14, 10), acknowledgedBy: Member("Maya"), acknowledgedAfter: TimeSpan.FromMinutes(3));
        Period(project, "paymentGateway", HealthCheckStatus.Unhealthy, DaysAgo(now, 41, 18, 2), DaysAgo(now, 41, 18, 54), "The gateway declined the test authorization.");
        AutoIncident(project, "payments", ProjectHealth.Degraded, DaysAgo(now, 41, 18, 3), DaysAgo(now, 41, 18, 54), ["paymentGateway"], acknowledgedBy: Member("Daniel"), acknowledgedAfter: TimeSpan.FromMinutes(7));
        AddBlips(project, now, 6, "userProfileImages", "cloudflare", "paymentGateway");

        AddReadings(project, now, "wnam", "enam", "weur");

        SetStatusPage(project, published: true, followers: 1284, support: "help@brightcart.shop", hostname: "status.brightcart.shop", hostnameState: "active",
        [
            ("Store and checkout", "فروشگاه و پرداخت", ["binStorage", "AppDbContext", "paymentGateway"]),
            ("Order emails", "ایمیل‌های سفارش", ["smtp", "emailDelivery"]),
            ("App notifications", "اعلان‌های اپ", ["firebase", "apns"]),
            ("Shopping assistant", "دستیار خرید", ["aiChat", "aiEmbedding"]),
            ("Product photos", "عکس‌های محصول", ["userProfileImages"])
        ]);

        project.Sla = new() { MonitoringId = project.Id, TargetPercent = 99.95, PartialWeight = 0.3, DegradedWeight = 0.1, Credits = [new() { BelowPercent = 99.95, CreditPercent = 10 }, new() { BelowPercent = 99.5, CreditPercent = 25 }, new() { BelowPercent = 99, CreditPercent = 50 }] };

        AddScans(project, now, new("A+", 112, 58, 95, 72, 6));

        return Finish(project, now);
    }

    /// <summary>A notes app whose email stopped working; Daniel is on it and has posted what he found.</summary>
    private static SampleProject Fieldnote(DateTimeOffset now)
    {
        var project = NewProject(now, "fieldnote", "Fieldnote", "app.fieldnote.io", "Europe/London", 210, serverHost: "api.fieldnote.io", hasIcon: true, templateVersion: "10.7.0", baseMs: 150);
        project.HasStaticFrontEnd = true;
        project.Locations = ["enam", "weur", "apac-se"];
        AddParts(project, now, [.. usualParts, "reCaptcha"]);

        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Email, Member("Maya"));
        Route(project, 1, 10, ProjectAlertChannel.Push, Member("Daniel"));
        Route(project, 1, 10, ProjectAlertChannel.Webhook, address: "https://default7f3c.0e.environment.api.powerplatform.com/powerautomate/automations/direct/workflows/fieldnote-ops/triggers/manual/paths/invoke", format: ProjectWebhookFormat.Teams);

        Deploy(project, DaysAgo(now, 3, 21, 4), 2);
        Deploy(project, DaysAgo(now, 1, 10, 15), 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;

        Fail(project, "smtp", HealthCheckStatus.Unhealthy, now.AddMinutes(-47), "The SMTP server refused the sign-in.", durationMs: 1840);
        var email = AutoIncident(project, "email", ProjectHealth.Degraded, now.AddMinutes(-46), null, ["smtp"], acknowledgedBy: Member("Daniel"), acknowledgedAfter: TimeSpan.FromMinutes(5), now: now);
        email.Updates.Insert(0, new()
        {
            At = now.AddMinutes(-38),
            Phase = ProjectIncidentPhase.Identified,
            Author = "Daniel Ortiz",
            Text = MonitoringTexts.OfTeam("Our email provider rotated the password last night. We're putting the new one in the app's settings.",
                                          "ارائه‌دهنده ایمیل ما دیشب رمز را عوض کرد. داریم رمز تازه را در تنظیمات اپ می‌گذاریم.")
        });
        email.Updates.Insert(0, new()
        {
            At = now.AddMinutes(-9),
            Phase = ProjectIncidentPhase.Monitoring,
            Author = "Daniel Ortiz",
            Text = MonitoringTexts.OfTeam("The new password is deployed. Watching the next checks before we call it fixed.",
                                          "رمز تازه Deploy شد. پیش از اینکه بگوییم درست شد، بررسی‌های بعدی را زیر نظر داریم.")
        });
        email.Version = 3;

        Period(project, "smtp", HealthCheckStatus.Unhealthy, DaysAgo(now, 3, 21, 8), DaysAgo(now, 3, 21, 47), "The SMTP server refused the sign-in.");
        AutoIncident(project, "email-before", ProjectHealth.Degraded, DaysAgo(now, 3, 21, 9), DaysAgo(now, 3, 21, 47), ["smtp"], afterDeployAt: DaysAgo(now, 3, 21, 4), acknowledgedBy: Member("Maya"), acknowledgedAfter: TimeSpan.FromMinutes(6));
        Period(project, "emailDelivery", HealthCheckStatus.Degraded, DaysAgo(now, 12, 8, 0), DaysAgo(now, 12, 11, 20), "The sender's domain has no DMARC record.");
        AutoIncident(project, "dmarc", ProjectHealth.Degraded, DaysAgo(now, 12, 8, 1), DaysAgo(now, 12, 11, 20), ["emailDelivery"], acknowledgedBy: Member("Sara"), acknowledgedAfter: TimeSpan.FromMinutes(22));
        AddBlips(project, now, 5, "aiChat", "userProfileImages", "reCaptcha");

        AddReadings(project, now);

        SetStatusPage(project, published: true, followers: 96, support: "https://fieldnote.io/help", hostname: "status.fieldnote.io", hostnameState: "pending",
        [
            ("Notes and sync", "یادداشت‌ها و همگام‌سازی", ["binStorage", "AppDbContext", "StackExchange.Redis_redis-persistent"]),
            ("Email", "ایمیل", ["smtp", "emailDelivery"]),
            ("Attachments", "پیوست‌ها", ["userProfileImages"]),
            ("Smart search", "جست‌وجوی هوشمند", ["aiEmbedding", "aiChat"])
        ]);

        AddScans(project, now, new("B", 78, 71, null, 84, 21));

        return Finish(project, now);
    }

    /// <summary>An invoicing SaaS in a planned maintenance right now, with a weekly window as well.</summary>
    private static SampleProject Ledgerline(DateTimeOffset now)
    {
        var project = NewProject(now, "ledgerline", "Ledgerline", "ledgerline.com", "Europe/Amsterdam", 320, serverHost: "api.ledgerline.com", baseMs: 210);
        project.Locations = ["enam", "weur", "eeur"];
        AddParts(project, now, [.. usualParts]);

        Route(project, 0, 0, ProjectAlertChannel.Email, Member("Sara"));
        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Sara"));
        Route(project, 1, 15, ProjectAlertChannel.Sms, Member("Maya"));

        Window(project, "Moving to a faster database server", now.AddMinutes(-25), TimeSpan.FromHours(1));
        var weekly = Window(project, "Weekly backup check", Next(now, DayOfWeek.Sunday, 3, 0).AddDays(-7 * 9), TimeSpan.FromMinutes(20), weekly: true);

        Fail(project, "hangfire", HealthCheckStatus.Degraded, now.AddMinutes(-24), "No Hangfire server is running.");
        AutoIncident(project, "jobs", ProjectHealth.Degraded, now.AddMinutes(-23), null, ["hangfire"], duringMaintenance: true, now: now);

        var pastWindow = weekly.StartsAt.AddDays(7 * 4);
        Period(project, "AppDbContext", HealthCheckStatus.Unhealthy, pastWindow.AddMinutes(3), pastWindow.AddMinutes(11));
        AutoIncident(project, "backup-check", ProjectHealth.Down, pastWindow.AddMinutes(4), pastWindow.AddMinutes(11), ["AppDbContext"], duringMaintenance: true);
        Deploy(project, DaysAgo(now, 2, 13, 20), 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;
        AddBlips(project, now, 4, "cloudflare", "backup");

        AddReadings(project, now);

        SetStatusPage(project, published: true, followers: 431, support: "support@ledgerline.com", hostname: "status.ledgerline.com", hostnameState: "moved",
        [
            ("Invoices and payments", "صورت‌حساب‌ها و پرداخت‌ها", ["binStorage", "AppDbContext"]),
            ("Reports", "گزارش‌ها", ["hangfire", "StackExchange.Redis_redis-persistent"]),
            ("Email", "ایمیل", ["smtp", "emailDelivery"])
        ]);

        project.Sla = new() { MonitoringId = project.Id, Credits = [new() { BelowPercent = 99.9, CreditPercent = 5 }, new() { BelowPercent = 99, CreditPercent = 15 }] };

        AddScans(project, now, new("A", 96, 94, 91, 98, 120));

        return Finish(project, now);
    }

    /// <summary>A team's tasks app whose data was reset three hours ago, which took bit Fabric's approval with it; its /health is still read.</summary>
    private static SampleProject Crewboard(DateTimeOffset now)
    {
        var project = NewProject(now, "crewboard", "Crewboard", "crewboard.app", "Asia/Dubai", 75, hasIcon: true, baseMs: 130);
        project.Locations = ["weur", "apac", "eeur"];
        AddParts(project, now, [.. usualParts]);

        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Webhook, address: "https://hooks.slack.com/services/T0C/B0C/crewboard", format: ProjectWebhookFormat.Slack);

        project.ConnectionStatus = ProjectConnectionStatus.ReconnectNeeded;
        // As the server stores it (ProjectProber): a code, worded in the reader's language when read.
        project.ConnectionProblem = ProjectConnectionProblems.Of(ProjectConnectionProblems.ApprovalWithdrawn);
        project.CheckedAt = now.AddHours(-3).AddMinutes(-12);
        Deploy(project, DaysAgo(now, 5, 9, 30), 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;

        Period(project, "aiChat", HealthCheckStatus.Unhealthy, DaysAgo(now, 8, 15, 0), DaysAgo(now, 8, 16, 5), "The chat model answered 429 Too Many Requests.");
        AutoIncident(project, "ai", ProjectHealth.Degraded, DaysAgo(now, 8, 15, 1), DaysAgo(now, 8, 16, 5), ["aiChat"], acknowledgedBy: Member("Maya"), acknowledgedAfter: TimeSpan.FromMinutes(12));
        AddBlips(project, now, 3, "aiEmbedding", "cloudflare");

        AddReadings(project, now);

        SetStatusPage(project, published: true, followers: 58, support: "hello@crewboard.app", hostname: null, hostnameState: null,
        [
            ("Boards and tasks", "بردها و کارها", ["binStorage", "AppDbContext"]),
            ("Notifications", "اعلان‌ها", ["firebase", "apns", "smtp"]),
            ("AI suggestions", "پیشنهادهای AI", ["aiChat"])
        ]);

        AddScans(project, now, new("C", 64, 87, 82, 91, 44), daysAgo: 9);

        return Finish(project, now);
    }

    /// <summary>A deliveries app connected three minutes ago: Germany read its report, the Network's readings are still coming in.</summary>
    private static SampleProject Parcelpoint(DateTimeOffset now)
    {
        var project = NewProject(now, "parcelpoint", "Parcelpoint", "parcelpoint.io", "Australia/Sydney", 0, hasIcon: true, baseMs: 200);
        AddParts(project, now, [.. usualParts.Where(key => key is not "backup")]);
        AddNotConfigured(project, "googleSignIn");

        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));

        project.ConnectionStatus = ProjectConnectionStatus.FirstChecks;
        project.CheckedAt = now.AddMinutes(-2);
        AddReadings(project, now);
        project.ReadingsIn = 1;

        SetStatusPage(project, published: false, followers: 0, support: null, hostname: null, hostnameState: null,
        [
            ("Website", "وب‌سایت", ["binStorage", "AppDbContext", "userProfileImages"]),
            ("Messaging", "پیام‌رسانی", ["smtp", "sms", "firebase", "apns"]),
            ("AI", "AI", ["aiChat", "aiEmbedding"])
        ]);
        // bit Fabric whose own host names aren't set up yet: the page says so instead of asking for one.
        project.StatusPage.CnameTarget = null;

        AddPendingScans(project, now, now.AddMinutes(-2), ProjectScanTrigger.Connected, running: 2);
        project.ScanProfile = new("A", 95, 89, 92, 94, 87);

        return Finish(project, now);
    }

    /// <summary>Your own app, healthy: the one that shows everything a project page has.</summary>
    private static SampleProject YourApp(DateTimeOffset now)
    {
        var project = NewProject(now, "yourapp", "Your app", "yourapp.com", "Europe/Berlin", 400, hasIcon: true, baseMs: 160);
        project.VisitorBaseMs = 38;
        project.Locations = ["enam", "weur", "oc"];
        AddParts(project, now, [.. usualParts, "reCaptcha", "websites", "aiRealtime"]);
        AddNotConfigured(project, "googleSignIn");

        // A part added to the report 20 days ago, and one taken out 40 days ago that still shows for the stretch it failed.
        project.PartSpans["aiRealtime"] = (now.AddDays(-20).AddHours(-5), null);
        project.PartSpans["StackExchange.Redis_redis-cache"] = (null, now.AddDays(-40).AddHours(-2));
        Period(project, "StackExchange.Redis_redis-cache", HealthCheckStatus.Degraded, DaysAgo(now, 52, 7, 10), DaysAgo(now, 52, 9, 40), "The cache didn't answer; memory served instead.");

        Route(project, 0, 0, ProjectAlertChannel.Push, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Email, Member("Maya"));
        Route(project, 0, 0, ProjectAlertChannel.Webhook, address: "https://hooks.slack.com/services/T024BE7LD/B067ZK8R1/yourapp-ops", format: ProjectWebhookFormat.Slack);
        Route(project, 1, 10, ProjectAlertChannel.Sms, Member("Daniel"));
        Route(project, 1, 10, ProjectAlertChannel.Email, Member("Leo"));
        Route(project, 1, 10, ProjectAlertChannel.Webhook, address: "https://yourteam.webhook.office.com/webhookb2/old-channel/IncomingWebhook/ops", format: ProjectWebhookFormat.Teams);
        Route(project, 2, 30, ProjectAlertChannel.Sms, Member("Sara"));
        Route(project, 2, 30, ProjectAlertChannel.Push, Member("Sara"));
        Route(project, 2, 30, ProjectAlertChannel.Webhook, address: "https://discord.com/api/webhooks/1190/yourapp", format: ProjectWebhookFormat.Discord);
        Route(project, 2, 30, ProjectAlertChannel.Webhook, address: "https://ops.yourapp.com/timeout/bitfabric", format: ProjectWebhookFormat.Json);

        Deploy(project, DaysAgo(now, 33, 11, 0), 5);
        Restart(project, DaysAgo(now, 17, 3, 26), 5);
        Deploy(project, DaysAgo(now, 9, 1, 50), 4);
        Deploy(project, DaysAgo(now, 3, 9, 58), 3);
        Restart(project, DaysAgo(now, 1, 4, 2), 3);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;

        Period(project, "smtp", HealthCheckStatus.Unhealthy, DaysAgo(now, 3, 10, 1), DaysAgo(now, 3, 10, 41), "The SMTP server didn't answer within 10 seconds.");
        AutoIncident(project, "email", ProjectHealth.Degraded, DaysAgo(now, 3, 10, 2), DaysAgo(now, 3, 10, 41), ["smtp"], afterDeployAt: DaysAgo(now, 3, 9, 58), acknowledgedBy: Member("Maya"), acknowledgedAfter: TimeSpan.FromMinutes(3));

        var upgrade = Window(project, "Database upgrade", DaysAgo(now, 9, 2, 0), TimeSpan.FromMinutes(45));
        Period(project, "AppDbContext", HealthCheckStatus.Unhealthy, upgrade.StartsAt.AddMinutes(5), upgrade.StartsAt.AddMinutes(31));
        AutoIncident(project, "upgrade", ProjectHealth.Down, upgrade.StartsAt.AddMinutes(6), upgrade.StartsAt.AddMinutes(31), ["AppDbContext"], duringMaintenance: true);
        Window(project, "Weekly server updates", Next(now, DayOfWeek.Sunday, 2, 0).AddDays(-7 * 3), TimeSpan.FromMinutes(30), weekly: true);

        // The last day has a slow stretch and a restart too, so Performance's first view shows them.
        Restart(project, now.AddHours(-6).AddMinutes(-17), 3);
        Period(project, "userProfileImages", HealthCheckStatus.Degraded, now.AddHours(-5).AddMinutes(-40), now.AddHours(-5).AddMinutes(-28), "The storage took 4.8 seconds to answer.");

        AutoIncident(project, "down", ProjectHealth.Down, DaysAgo(now, 17, 3, 20), DaysAgo(now, 17, 3, 26), [], acknowledgedBy: Member("Sara"), acknowledgedAfter: TimeSpan.FromMinutes(2));
        Period(project, "aiChat", HealthCheckStatus.Degraded, DaysAgo(now, 33, 13, 0), DaysAgo(now, 33, 14, 20), "The chat model took 14 seconds to answer.");
        AutoIncident(project, "ai", ProjectHealth.Degraded, DaysAgo(now, 33, 13, 1), DaysAgo(now, 33, 14, 20), ["aiChat"], afterDeployAt: DaysAgo(now, 33, 11, 0), acknowledgedBy: Member("Daniel"), acknowledgedAfter: TimeSpan.FromMinutes(9));
        AddBlips(project, now, 8, "userProfileImages", "cloudflare", "aiEmbedding", "websites");

        project.Incidents.Add(new()
        {
            Id = MonitoringNoise.IdOf("yourapp:announcement:passkeys"),
            Kind = ProjectIncidentKind.Announcement,
            Impact = ProjectHealth.Operational,
            StartedAt = DaysAgo(now, 5, 15, 0),
            EndedAt = DaysAgo(now, 5, 15, 0),
            Title = MonitoringTexts.OfTeam("New: sign in with a passkey", "تازه: ورود با Passkey"),
            Updates =
            {
                new()
                {
                    At = DaysAgo(now, 5, 15, 0),
                    Phase = ProjectIncidentPhase.Resolved,
                    Author = "Maya Chen",
                    Text = MonitoringTexts.OfTeam("You can now sign in with your fingerprint or face instead of a password. Turn it on in Settings, Account.",
                                                  "حالا می‌توانید به‌جای رمز، با اثر انگشت یا چهره وارد شوید. آن را در تنظیمات، حساب کاربری روشن کنید.")
                }
            }
        });

        AddReadings(project, now);

        SetStatusPage(project, published: true, followers: 412, support: "support@yourapp.com", hostname: "status.yourapp.com", hostnameState: "active",
        [
            ("Website and sign-in", "وب‌سایت و ورود", ["binStorage", "AppDbContext", "appCertificate", "reCaptcha"]),
            ("Emails", "ایمیل‌ها", ["smtp", "emailDelivery"]),
            ("Text messages", "پیامک‌ها", ["sms"]),
            ("App notifications", "اعلان‌های اپ", ["firebase", "apns"]),
            ("AI assistant", "دستیار AI", ["aiChat", "aiEmbedding", "aiRealtime"]),
            ("File uploads", "بارگذاری فایل", ["userProfileImages"])
        ], languages: ["en-US", "fa-IR"]);

        project.Sla = new() { MonitoringId = project.Id, Credits = [new() { BelowPercent = 99.9, CreditPercent = 10 }, new() { BelowPercent = 99, CreditPercent = 25 }, new() { BelowPercent = 95, CreditPercent = 50 }] };
        project.ScanSettings = new() { MonitoringId = project.Id, ScanAfterDeployments = true };

        AddScans(project, now, new("A", 96, 92, 88, 96, 64), history: true);

        return Finish(project, now);
    }

    /// <summary>A writing app watched for 12 days: a cache answers its home page, several parts aren't configured, nobody gets alerts and no scan has run.</summary>
    private static SampleProject Inkpad(DateTimeOffset now)
    {
        var project = NewProject(now, "inkpad", "Inkpad", "inkpad.so", "America/Los_Angeles", 12, hasIcon: true, templateVersion: "10.7.0", baseMs: 110);
        project.HasCacheWarning = true;
        AddParts(project, now, "binStorage", "AppDbContext", "hangfire", "appCertificate", "StackExchange.Redis_redis-persistent", "userProfileImages", "smtp", "emailDelivery", "aiChat");
        AddNotConfigured(project, "sms");
        AddNotConfigured(project, "cloudflare");
        AddNotConfigured(project, "googleSignIn");
        AddNotConfigured(project, "gitHubSignIn");
        AddBlips(project, now, 2, "aiChat", "userProfileImages");
        Deploy(project, DaysAgo(now, 4, 18, 30), 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;

        AddReadings(project, now);

        SetStatusPage(project, published: false, followers: 0, support: null, hostname: null, hostnameState: null,
        [
            ("Writing", "نوشتن", ["binStorage", "AppDbContext"]),
            ("Email", "ایمیل", ["smtp", "emailDelivery"]),
            ("AI writing help", "کمک AI در نوشتن", ["aiChat"])
        ]);

        return Finish(project, now);
    }

    /// <summary>An inventory app the team disconnected five days ago: its history is kept, nothing is read.</summary>
    private static SampleProject Stockroom(DateTimeOffset now)
    {
        var project = NewProject(now, "stockroom", "Stockroom", "stockroom.tools", "Europe/Berlin", 190, templateVersion: "10.4.2", baseMs: 260);
        AddParts(project, now, "binStorage", "AppDbContext", "hangfire", "appCertificate", "userProfileImages", "smtp", "emailDelivery");

        Route(project, 0, 0, ProjectAlertChannel.Email, Member("Maya"));

        project.ConnectionStatus = ProjectConnectionStatus.Disconnected;
        project.CheckedAt = now.AddDays(-5).AddHours(-2);
        Deploy(project, DaysAgo(now, 30, 10, 0), 1);
        project.ApplicationVersion = project.Starts[^1].ApplicationVersion;
        Period(project, "AppDbContext", HealthCheckStatus.Unhealthy, DaysAgo(now, 14, 22, 10), DaysAgo(now, 14, 22, 34));
        AutoIncident(project, "database", ProjectHealth.Down, DaysAgo(now, 14, 22, 11), DaysAgo(now, 14, 22, 34), ["AppDbContext"], acknowledgedBy: Member("Maya"), acknowledgedAfter: TimeSpan.FromMinutes(8));
        AddBlips(project, now, 3, "userProfileImages", "smtp");

        SetStatusPage(project, published: false, followers: 0, support: null, hostname: null, hostnameState: null,
        [
            ("Inventory", "انبار", ["binStorage", "AppDbContext", "hangfire"]),
            ("Email", "ایمیل", ["smtp", "emailDelivery"])
        ]);

        AddScans(project, now, new("B", 80, 76, 79, 88, 33), daysAgo: 12);

        return Finish(project, now);
    }

    /// <summary>The projects only the agency's view has.</summary>
    public static readonly string[] AgencyKeys = ["tablewise", "quoteflow", "fleetline", "hiveroom", "learnly", "formcraft"];

    /// <summary>Six more apps an agency watches for its customers, mostly fine, one with its SMS failing.</summary>
    private static IEnumerable<SampleProject> Agency(DateTimeOffset now)
    {
        (string Key, string Name, string Host, string Zone, bool Icon)[] apps =
        [
            ("tablewise", "Tablewise", "tablewise.app", "Europe/Paris", true),
            ("quoteflow", "Quoteflow", "quoteflow.io", "America/Chicago", false),
            ("fleetline", "Fleetline", "app.fleetline.eu", "Europe/Warsaw", true),
            ("hiveroom", "Hiveroom", "hiveroom.community", "America/Toronto", false),
            ("learnly", "Learnly", "learnly.academy", "Asia/Singapore", true),
            ("formcraft", "Formcraft", "formcraft.app", "Europe/Madrid", false)
        ];

        foreach (var (key, name, host, zone, icon) in apps)
        {
            var n = MonitoringNoise.Of(key, 0);
            var project = NewProject(now, key, name, host, zone, 60 + (int)(n * 300), hasIcon: icon, templateVersion: n > 0.5 ? "11.0.0" : "10.7.0", baseMs: 110 + (int)(n * 160));
            AddParts(project, now, [.. usualParts.Where((_, index) => index < 6 || MonitoringNoise.Of(key, index + 10) > 0.3)]);
            Route(project, 0, 0, ProjectAlertChannel.Email, Member("Maya"));
            Route(project, 1, 15, ProjectAlertChannel.Webhook, address: $"https://hooks.slack.com/services/T0A/B0A/{key}", format: ProjectWebhookFormat.Slack);
            Deploy(project, DaysAgo(now, 1 + (int)(n * 9), 10, 30), 1);
            project.ApplicationVersion = project.Starts[^1].ApplicationVersion;
            AddBlips(project, now, 2 + (int)(n * 8), "userProfileImages", "cloudflare", "smtp");

            if (key is "quoteflow")
            {
                project.Checks.RemoveAll(check => check.Key is "sms");
                AddParts(project, now, "sms");
                Fail(project, "sms", HealthCheckStatus.Degraded, now.AddMinutes(-23), "Twilio account status: suspended.");
                AutoIncident(project, "sms", ProjectHealth.Degraded, now.AddMinutes(-22), null, ["sms"], now: now);
            }

            if (key is "hiveroom")
            {
                AutoIncident(project, "down", ProjectHealth.Down, DaysAgo(now, 6, 20, 0), DaysAgo(now, 6, 20, 35), [], acknowledgedBy: Member("Daniel"), acknowledgedAfter: TimeSpan.FromMinutes(4));
            }

            AddReadings(project, now);

            SetStatusPage(project, published: n > 0.3, followers: (int)(n * 900), support: $"support@{host}", hostname: n > 0.6 ? $"status.{host}" : null, hostnameState: n > 0.6 ? "active" : null,
            [
                ("Website", "وب‌سایت", ["binStorage", "AppDbContext"]),
                ("Email", "ایمیل", ["smtp"])
            ]);

            AddScans(project, now, new(n > 0.3 ? "A" : "B", n > 0.3 ? 95 : 80, 80 + (int)(n * 18), n > 0.6 ? 92 : null, 85 + (int)(n * 12), 30 + (int)(n * 60)), daysAgo: 1 + (int)(n * 5));

            yield return Finish(project, now);
        }
    }

    /// <summary>
    /// What every project gets at the end: each entry's last rounds, from the stretches it had, and its home page's timing
    /// in the round that read the report.
    /// </summary>
    private static SampleProject Finish(SampleProject project, DateTimeOffset now)
    {
        var lastRound = project.CheckedAt ?? now;
        var rounds = project.ConnectionStatus is ProjectConnectionStatus.FirstChecks ? 2 : 20;

        // A report read hours ago reused results measured before it, not before now.
        foreach (var check in project.Checks.Where(check => check.VerifiedAt is not null && lastRound < now))
        {
            check.VerifiedAt -= now - lastRound;
        }

        foreach (var check in project.Checks.Where(check => check.IsNotConfigured is false))
        {
            var part = MonitoringParts.All.FirstOrDefault(part => part.Key == check.Key);

            check.Recent = [.. Enumerable.Range(0, rounds).Select(index =>
            {
                var at = lastRound.AddMinutes(index - rounds + 1);
                var status = check.Since is { } since && at >= since ? check.Status : HealthCheckStatus.Healthy;
                var isReused = part?.CacheMinutes is { } minutes && (int)(at.ToUnixTimeSeconds() / 60) % minutes is not 0;
                var ms = status is HealthCheckStatus.Healthy
                    ? (int)Math.Round(Math.Max(1, check.DurationMs) * (0.75 + 0.5 * MonitoringNoise.Of($"{project.Key}:{check.Key}:round", at.ToUnixTimeSeconds() / 60)))
                    : check.DurationMs;

                return new ProjectCheckRoundDto { At = at, Status = status, DurationMs = isReused ? null : ms };
            })];
        }

        project.HomePageMs ??= project.Checks.Any(check => check.Status is HealthCheckStatus.Unhealthy && MonitoringParts.Critical.Contains(check.Key))
            ? null
            : (int)Math.Round(project.BaseMs * (0.85 + 0.3 * MonitoringNoise.Of($"{project.Key}:home", 0)));

        return project;
    }
}
