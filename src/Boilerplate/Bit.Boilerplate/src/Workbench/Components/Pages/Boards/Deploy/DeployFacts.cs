//+:cnd:noEmit
namespace Boilerplate.Workbench.Components.Pages.Boards.Deploy;

/// <summary>Where a stop of the pipeline runs, which colors it and groups it under one bracket with its neighbors.</summary>
public enum DeployPlace
{
    You,
    GitHub,
    Server,
    Online
}

/// <summary>A stop on the way from a change to the users: its icon, a few everyday words and a line under them (resource keys).</summary>
public sealed record DeployStop(string IconName, string NameKey, string NoteKey, DeployPlace Place);

/// <summary>A step of a deployment on the server, in the order the deploy job runs them (a resource key).</summary>
public sealed record DeployStep(string IconName, string NameKey);

/// <summary>
/// A container on the server: one of the app's, which a deployment swaps for a new one, or a service it keeps. Its name is a
/// resource key, or a product's own name, which reads the same in every language.
/// </summary>
public sealed record DeployBox(string Name, string IconName, bool IsApp);

/// <summary>A variable or a secret each GitHub environment, Test and Production, holds its own value of.</summary>
public sealed record DeploySetting(string Name, bool IsSecret);

/// <summary>
/// What the deploy board shows, read from .github/workflows, .docs/16 and the deploy skill, in words someone who doesn't
/// build software follows; the technical names stay in the small print. Each part is in the template's conditions of what
/// it describes, so a project sees the board for the options it was created with: Cloudflare or Caddy in front of the
/// server, its database, Redis, a standalone api, and no server deployment without .NET Aspire.
/// </summary>
public static class DeployFacts
{
    public static DeployStop[] Stops { get; } =
    [
        new(BitIconName.BranchFork2, nameof(WorkbenchStrings.DeployStopChanges), nameof(WorkbenchStrings.DeployStopChangesNote), DeployPlace.You),
        new(BitIconName.TestBeaker, nameof(WorkbenchStrings.DeployStopTested), nameof(WorkbenchStrings.DeployStopTestedNote), DeployPlace.GitHub),
        //#if (aspire == true)
        new(BitIconName.Package, nameof(WorkbenchStrings.DeployStopPackaged), nameof(WorkbenchStrings.DeployStopPackagedNote), DeployPlace.GitHub),
        new(BitIconName.Server, nameof(WorkbenchStrings.DeployStopLive), nameof(WorkbenchStrings.DeployStopLiveNote), DeployPlace.Server),
        //#if (cloudflare == true)
        new(BitIconName.Shield, nameof(WorkbenchStrings.DeployStopSafe), nameof(WorkbenchStrings.DeployStopSafeNote), DeployPlace.Online),
        //#else
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new(BitIconName.Lock, nameof(WorkbenchStrings.DeployStopSafe), nameof(WorkbenchStrings.DeployStopSafeCaddyNote), DeployPlace.Online),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        new(BitIconName.People, nameof(WorkbenchStrings.DeployStopUsers), nameof(WorkbenchStrings.DeployStopUsersNote), DeployPlace.Online)
        //#else
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new(BitIconName.Devices3, nameof(WorkbenchStrings.DeployStopApps), nameof(WorkbenchStrings.DeployStopAppsNote), DeployPlace.GitHub)
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
    ];

    // Each label is a resource key.
    public static BoardNumber[] Numbers { get; } =
    [
        //#if (aspire == true)
        new("0", nameof(WorkbenchStrings.DeployNumberBuilds)),
        //#if (cloudflare == true)
        new("0", nameof(WorkbenchStrings.DeployNumberDoors)),
        //#else
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("2", nameof(WorkbenchStrings.DeployNumberDoorsCaddy)),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#endif
        new("100%", nameof(WorkbenchStrings.DeployNumberTested)),
        new("2", nameof(WorkbenchStrings.DeployNumberCopies))
    ];

    //#if (aspire == true)
    public static DeployStep[] Steps { get; } =
    [
        new(BitIconName.CloudDownload, nameof(WorkbenchStrings.DeployStepDownload)),
        new(BitIconName.Settings, nameof(WorkbenchStrings.DeployStepSettings)),
        new(BitIconName.Database, nameof(WorkbenchStrings.DeployStepData)),
        new(BitIconName.Play, nameof(WorkbenchStrings.DeployStepStart)),
        new(BitIconName.Sync, nameof(WorkbenchStrings.DeployStepSwitch)),
        new(BitIconName.Globe, nameof(WorkbenchStrings.DeployStepCheck)),
        //#if (cloudflare == true)
        new(BitIconName.Cloud, nameof(WorkbenchStrings.DeployStepRefresh))
        //#endif
    ];

    public static DeployBox[] Boxes { get; } =
    [
        new(nameof(WorkbenchStrings.DeployBoxWebApp), BitIconName.Website, IsApp: true),
        //#if (api == "Standalone")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("API", BitIconName.Code, IsApp: true),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        new(nameof(WorkbenchStrings.DeployBoxLandingPage), BitIconName.Globe, IsApp: true),
        //#if (database == "SqlServer")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("SQL Server", BitIconName.Database, IsApp: false),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (database == "PostgreSQL")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("PostgreSQL", BitIconName.Database, IsApp: false),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (database == "MySql")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("MySQL", BitIconName.Database, IsApp: false),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (database == "Sqlite")
        new("SQLite", BitIconName.Database, IsApp: false),
        //#endif
        //#if (redis == true)
        new(nameof(WorkbenchStrings.DeployBoxCache), BitIconName.SpeedHigh, IsApp: false),
        new(nameof(WorkbenchStrings.DeployBoxStorage), BitIconName.HardDrive, IsApp: false),
        //#endif
        //#if (filesStorage == "Local")
        new(nameof(WorkbenchStrings.DeployBoxFiles), BitIconName.FabricFolder, IsApp: false),
        //#endif
    ];
    //#endif

    public static DeploySetting[] Settings { get; } =
    [
        new("SERVER_ADDRESS", IsSecret: false),
        //#if (api == "Standalone")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("SERVER_API_ADDRESS", IsSecret: false),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        new("APP_TITLE", IsSecret: false),
        new("APP_ID", IsSecret: false),
        //#if (aspire == true)
        new("LANDING_PAGE_ADDRESS", IsSecret: false),
        new("SMTP_CONNECTION_STRING", IsSecret: true),
        new("APP_CERTIFICATE", IsSecret: true),
        //#if (database == "SqlServer" || database == "PostgreSQL" || database == "MySql")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("DATABASE_PASSWORD", IsSecret: true),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (database == "Other")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("DATABASE_CONNECTION_STRING", IsSecret: true),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#if (filesStorage == "S3")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("S3_CONNECTION_STRING", IsSecret: true),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#elif (filesStorage == "AzureBlobStorage")
        //#if (IsInsideProjectTemplate == true)
        /*
        //#endif
        new("AZURE_BLOB_STORAGE_CONNECTION_STRING", IsSecret: true),
        //#if (IsInsideProjectTemplate == true)
        */
        //#endif
        //#endif
        //#if (redis == true)
        new("REDIS_CACHE_PASSWORD", IsSecret: true),
        new("REDIS_PERSISTENT_PASSWORD", IsSecret: true),
        //#endif
        //#if (cloudflare == true)
        new("CLOUDFLARE_TUNNEL_TOKEN", IsSecret: true),
        new("CLOUDFLARE_ZONE", IsSecret: true),
        new("CLOUDFLARE_TOKEN", IsSecret: true),
        //#endif
        //#if (notification == true)
        new("PUBLIC_VAPIDKEY", IsSecret: true),
        new("PRIVATE_VAPIDKEY", IsSecret: true),
        new("VAPID_SUBJECT", IsSecret: true),
        //#endif
        //#endif
        new("ANDROID_RELEASE_*", IsSecret: true),
        new("APPSTORE_*", IsSecret: true)
    ];

    public static BoardSource[] Sources { get; } =
    [
        new(".github/workflows/cd-template.yml"),
        new(".github/workflows/cd-test.yml"),
        new(".github/workflows/cd-production.yml"),
        new(".github/workflows/ci.yml"),
        new(".docs/16- CI-CD Pipeline and Environments.md"),
        new(".github/agents/deploy.agent.md")
    ];
}
