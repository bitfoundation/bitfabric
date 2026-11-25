//+:cnd:noEmit
namespace Boilerplate.Workbench.Components.Pages.Explainers.CiCd.Data;

/// <summary>
/// What the CI/CD explainer says, read from .github/workflows and .docs/16: each part is in the template's conditions of
/// the workflows it describes, so a project gets the page for the options it was created with, such as Cloudflare or
/// Caddy in front of the server, and no server deployment without .NET Aspire.
/// </summary>
public static class CiCdPipeline
{
    public static CiCdHighlight[] Highlights { get; } =
    [
        new(BitIconName.Cloud, "Builds run on GitHub", "GitHub's own runners check, build and package everything, so nothing is ever built on your server."),
        //#if (aspire == true)
        new(BitIconName.Server, "Your server only runs it", "A runner on your Linux server pulls the finished images and swaps in the new version with no downtime."),
        //#if (cloudflare == true)
        new(BitIconName.Lock, "No inbound port open", "The runner and the Cloudflare Tunnel both dial out, so every inbound port of the server stays closed."),
        //#else
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new(BitIconName.Lock, "Only 80 and 443 open", "Caddy takes those two ports and gets each public address its HTTPS certificate; the runner dials out."),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#endif
        new(BitIconName.Money, "CI inside every CD", "A private repository's Actions minutes are limited, so the checks run inside each deployment, which deploys nothing until they pass, rather than on every push.")
    ];

    public static CiCdStage[] Stages { get; } =
    [
        new("What starts it", BitIconName.Play,
        [
            new("cd-test.yml", "You run it on the branch to try on Test", IsCode: true),
            new("cd-production.yml", "By hand at first, then each merge into main", IsCode: true)
        ]),
        new("GitHub's runners", BitIconName.Cloud,
        [
            new("ci", "Formatting, build, api check and tests", IsCode: true),
            //#if (aspire == true)
            new("build_server", "The server's images, built beside ci and pushed to GitHub Container Registry", IsCode: true),
            //#endif
            new("Windows, Android, iOS", "The apps, built once ci passes; off until you ship them", IsOff: true)
        ]),
        //#if (aspire == true)
        new("Your server", BitIconName.Server,
        [
            new("deploy_server", "Migrates the database and swaps in the new version", IsCode: true)
        ], IsYourServer: true),
        //#endif
        new("Live", BitIconName.Globe,
        [
            //#if (aspire == true)
            new("SERVER_ADDRESS", "Checked to answer before the run ends", IsCode: true),
            //#if (cloudflare == true)
            new("Cloudflare", "Its cache is purged, so users get the new version at once")
            //#else
            //#if (IsInsideProjectTemplate == true)
            /*
            //#endif
            new("Caddy", "Serves each public address over HTTPS")
            //#if (IsInsideProjectTemplate == true)
            */
            //#endif
            //#endif
            //#else
            //#if (IsInsideProjectTemplate == true)
            /*
            //#endif
            new("The run's artifacts", "The apps wait there for a day; without .NET Aspire the server isn't deployed")
            //#if (IsInsideProjectTemplate == true)
            */
            //#endif
            //#endif
        ])
    ];

    public static CiCdWorkflow[] Workflows { get; } =
    [
        new("ci.yml", "Inside every CD, before anything deploys",
            "Checks the formatting, builds the whole solution with the code style enforced, checks that the api still works for the app versions it supports, and runs the tests. Nothing deploys until it passes.",
            BitIconName.TestBeaker),
        new("cd-test.yml", "By hand, on the branch to try",
            "Deploys that branch to Test, which keeps whichever branch was deployed to it last. Try a branch there before you merge it.",
            BitIconName.Rocket, "gh workflow run cd-test.yml --ref <branch>"),
        new("cd-production.yml", "By hand at first, then each merge into main",
            "Deploys main to Production. Its push trigger stays off until the first deployment works, and a change to the docs, the tests or the agent files never deploys.",
            BitIconName.Deploy),
        new("cd-template.yml", "Called by both CDs",
            "The jobs both deployments share, with ENV_NAME naming the environment: CI, the server's build and deployment, and the apps' builds.",
            BitIconName.Flow)
    ];

    public static CiCdEnvironment[] Environments { get; } =
    [
        new("Test", BitIconName.TestBeaker,
        [
            new("Deploys", "Whichever branch you run it on"),
            new("Starts", "gh workflow run cd-test.yml --ref <branch>", IsCode: true),
            //#if (aspire == true)
            new("Server", "The self-hosted runner labeled Test"),
            //#endif
            new("App versions", "The run's number is the build number, so every Test build installs as an update")
        ]),
        new("Production", BitIconName.Rocket,
        [
            new("Deploys", "main"),
            new("Starts", "By hand until the first deployment works, then on each merge into main that changes the app"),
            //#if (aspire == true)
            new("Server", "The self-hosted runner labeled Production"),
            new("Extra check", "bit api check --release", IsCode: true),
            //#endif
            new("App versions", "The Version in src/Directory.Build.props, raised for each release")
        ])
    ];

    public static CiCdJob[] Jobs { get; } =
    [
        new("ci", "GitHub, ubuntu-26.04", false, "ci.yml's checks. Nothing deploys or publishes until they pass."),
        //#if (aspire == true)
        new("build_server", "GitHub, ubuntu-26.04", false, "Fills in missing translations, builds the server's images with .NET Aspire and pushes them to GitHub Container Registry."),
        new("deploy_server", "Your server, labeled Test or Production", true, "Writes the settings, migrates the database and replaces the apps. The server holds Docker and the runner, no SDK and no source code."),
        //#endif
        new("build_blazor_hybrid_windows", "GitHub, windows-2025", false, "A Windows installer with automatic updates, signed once the CODE_SIGNING settings are there.", IsOff: true),
        new("build_blazor_hybrid_android", "GitHub, ubuntu-26.04", false, "An .aab for Google Play, signed with your keystore.", IsOff: true),
        new("build_blazor_hybrid_iOS", "GitHub, macOS (xcode-27)", false, "An .ipa for the App Store, signed without a Mac of your own.", IsOff: true)
    ];

    public static CiCdStep[] DeploySteps { get; } =
    [
        //#if (aspire == true)
        new("Pull the images", "The ones build_server pushed, tagged with the environment and the commit.", BitIconName.CloudDownload),
        new("Write the settings", "The Docker Compose project and its settings, from the environment's own secrets and variables.", BitIconName.Settings),
        new("Migrate the database", "On its own, while the running version keeps serving. A failure here leaves that version running.", BitIconName.Database),
        new("Start the services", "What the apps depend on comes up first, and has to be healthy.", BitIconName.ServerProcesses),
        new("Replace the apps", "The new containers start beside the old ones, which stop only once every new one is healthy.", BitIconName.Sync),
        new("Check the public address", "The run waits until the site answers where its users reach it.", BitIconName.Globe),
        //#if (cloudflare == true)
        new("Purge Cloudflare's cache", "So users get the new version at once.", BitIconName.Cloud),
        //#endif
        //#endif
    ];

    public static CiCdHighlight[] Cautions { get; } =
    [
        //#if (aspire == true)
        new(BitIconName.Database, "Migrations run while the old version still serves", "So each one has to keep that version working: add a column in one release and drop the old one in a later release."),
        new(BitIconName.Undo, "To roll back, deploy an earlier commit", "Its images are still in the registry, and the deployment undoes the migrations that commit doesn't have."),
        new(BitIconName.HardDrive, "The data lives in Docker volumes on the server", "Back them up somewhere other than the server."),
        //#endif
    ];

    public static CiCdSetting[] Settings { get; } =
    [
        new("SERVER_ADDRESS", CiCdSettingKind.Variable, "The web app's public address, built into the apps and trusted by the server"),
        //#if (api == "Standalone")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("SERVER_API_ADDRESS", CiCdSettingKind.Variable, "The api's public address, which every app is built with"),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        new("APP_TITLE, APP_ID", CiCdSettingKind.Variable, "The app's name and id, different per environment so the Test and Production apps install side by side"),
        new("OPENAI_APIKEY", CiCdSettingKind.Secret, "The key bit translate fills in missing translations with", IsOptional: true),
        //#if (aspire == true)
        new("LANDING_PAGE_ADDRESS", CiCdSettingKind.Variable, "The landing page's public address, which the deployment checks"),
        new("SMTP_CONNECTION_STRING", CiCdSettingKind.Secret, "The mail server the app and the landing page send through"),
        new("APP_CERTIFICATE", CiCdSettingKind.Secret, "The environment's own certificate, which signs its tokens"),
        new("APP_CERTIFICATE_RETIRED", CiCdSettingKind.Secret, "The certificate it replaced, while tokens it signed are still alive", IsOptional: true),
        //#if (database == "SqlServer" || database == "PostgreSQL" || database == "MySql")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("DATABASE_PASSWORD", CiCdSettingKind.Secret, "The database server's password"),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (database == "Other")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("DATABASE_CONNECTION_STRING", CiCdSettingKind.Secret, "The connection string of the database"),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#if (filesStorage == "S3")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("S3_CONNECTION_STRING", CiCdSettingKind.Secret, "The S3-compatible storage the files go to"),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (filesStorage == "AzureBlobStorage")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("AZURE_BLOB_STORAGE_CONNECTION_STRING", CiCdSettingKind.Secret, "The Azure Storage account the files go to"),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#if (redis == true)
        new("REDIS_CACHE_PASSWORD, REDIS_PERSISTENT_PASSWORD", CiCdSettingKind.Secret, "The passwords of the two Redis containers"),
        //#endif
        //#if (cloudflare == true)
        new("CLOUDFLARE_TUNNEL_TOKEN", CiCdSettingKind.Secret, "The connector token of the environment's Cloudflare Tunnel"),
        new("CLOUDFLARE_ZONE, CLOUDFLARE_TOKEN", CiCdSettingKind.Secret, "The zone, and a token that may purge its cache"),
        //#endif
        //#if (notification == true)
        new("PUBLIC_VAPIDKEY, PRIVATE_VAPIDKEY, VAPID_SUBJECT", CiCdSettingKind.Secret, "The environment's web push keys and the contact push services see"),
        //#endif
        new("<SERVICE>__<Section>__<Key>", CiCdSettingKind.Secret, "Any other setting of a service, one each, such as SERVERWEB__SMS__PROVIDERS__TWILIO__AUTHTOKEN; a variable for what isn't secret", IsOptional: true),
        //#endif
        //#if (notification == true)
        new("GOOGLE_SERVICES_JSON_FILE_BASE64", CiCdSettingKind.Secret, "The Android app's google-services.json, for push notifications", IsOptional: true),
        //#endif
        new("ANDROID_RELEASE_*", CiCdSettingKind.Secret, "The Android app's keystore and its passwords"),
        new("APPSTORE_*", CiCdSettingKind.Secret, "Signing the iOS app and uploading it to App Store Connect"),
        new("IOS_CODE_SIGN_PROVISION", CiCdSettingKind.Variable, "The iOS app's provisioning profile"),
        new("CODE_SIGNING_CLIENT_ID, CODE_SIGNING_TENANT_ID, CODE_SIGNING_SUBSCRIPTION_ID", CiCdSettingKind.Secret, "Signing the Windows app with Artifact Signing", IsOptional: true),
        new("CODE_SIGNING_ENDPOINT, CODE_SIGNING_ACCOUNT, CODE_SIGNING_CERTIFICATE_PROFILE, WINDOWS_UPDATE_FILES_URL", CiCdSettingKind.Variable, "Where the Windows app is signed, and where it looks for updates", IsOptional: true)
    ];

    public static ExplainerSource[] Sources { get; } =
    [
        new(".github/workflows/cd-template.yml", Note: "The jobs, and the final word on what each one reads"),
        new(".github/workflows/ci.yml"),
        new(".github/workflows/cd-test.yml"),
        new(".github/workflows/cd-production.yml"),
        new(".docs/16- CI-CD Pipeline and Environments.md", Note: "The same story in words, with every setting"),
        new(".github/agents/deploy.agent.md", Note: "The skill that sets up the server, the runner and the secrets")
    ];
}
