using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// `aspire publish` turns the application model into the Docker Compose project the CD deploys to the server, the Aspire
// dashboard included; run mode leaves this resource out of the model. See `ConfigureDockerComposePublishing` below.
var compose = builder.AddDockerComposeEnvironment("compose");

// Check out appsettings.Development.json for credentials/passwords settings.

// Redis cache for FusionCache hybrid caching (L2 cache) and SignalR backplane - no persistence needed
var redisCache = builder.AddRedisCache();

// Redis for Hangfire background jobs, and distributed locking - persistent with AOF for durability
var redisPersistent = builder.AddRedisPersistent();

var postgresDatabase = builder.AddPostgreSQL();

var s3Storage = builder.AddS3Storage();

// Development only: a deployed server takes its embeddings from the OpenAI-compatible provider its
// settings name.
var embedding = builder.ExecutionContext.IsRunMode ? builder.AddTextEmbeddingsInference() : null;

// The performance and accessibility scans: Lighthouse and Chrome in a container of bit Fabric's own, which the api calls.
var lighthouse = builder.AddLighthouse();

var serverWebProject = builder.AddProject("serverweb", "../Bit.Fabric.Server.Web/Bit.Fabric.Server.Web.csproj")
    .WithExternalHttpEndpoints();

// Adding health checks endpoints to applications in non-development environments has security implications.
// See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
if (builder.Environment.IsDevelopment())
{
    serverWebProject.WithHttpHealthCheck("/alive");
}

var serverApiProject = builder.AddProject("serverapi", "../Bit.Fabric.Server.Api/Bit.Fabric.Server.Api.csproj")
    .WithExternalHttpEndpoints();

// Adding health checks endpoints to applications in non-development environments has security implications.
// See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
if (builder.Environment.IsDevelopment())
{
    serverApiProject.WithHttpHealthCheck("/alive");
}

if (builder.ExecutionContext.IsRunMode)
{
    // An OAuth client arrives at /oauth/authorize without an origin, so without WebAppUrl the api sends it to a consent
    // page on its own host, which has none (404). A deployment sets it in cd-template.yml.
    serverApiProject.WithEnvironment("WebAppUrl", serverWebProject.GetEndpoint("http"));
}

serverWebProject.WithReference(serverApiProject);
serverApiProject.WithReference(postgresDatabase).WaitFor(postgresDatabase);
serverApiProject.WithReference(s3Storage);
if (embedding is not null)
{
    serverApiProject.WithEnvironment("AI__HuggingFace__EmbeddingEndpoint", embedding.GetEndpoint("http"));
}
serverApiProject.WithEnvironment("Monitoring__LighthouseUrl", lighthouse.GetEndpoint("http"));
serverApiProject.WithReference(redisCache).WaitFor(redisCache);
serverWebProject.WithReference(redisCache).WaitFor(redisCache);
serverWebProject.WithReference(redisPersistent).WaitFor(redisPersistent); // Its Data Protection keys
serverApiProject.WithReference(redisPersistent).WaitFor(redisPersistent);

// cloudflared connects straight to the projects (no reverse proxy) - possible now that RemoveWildcardEndpoints drops http2.
builder.AddCloudflareTunnels(serverWebProject
    , serverApiProject
    );

// DbGate, the one web UI for the database and Redis. In run mode it starts from the Aspire dashboard.
builder.ConfigureDbGate();

// The Workbench is deployed to Test only, for the team and the people they invite, behind Cloudflare Access (See the deploy
// skill). In run mode it isn't a resource: it runs with dotnet watch in its own folder (See the workbench skill).
var deployedWorkbench = builder.ExecutionContext.IsPublishMode && builder.Environment.EnvironmentName is "Test"
    ? builder.AddProject("workbench", "../../Workbench/Bit.Fabric.Workbench.csproj").WithExternalHttpEndpoints()
    : null;

// Publish mode only: the registry the images go to, the migrations service, health checks, restart policies, the
// dashboards on the server's loopback and the settings the deployed server reads from the deploy job.
builder.ConfigureDockerComposePublishing(compose, serverWebProject
    , serverApiProject
    , lighthouse
    , deployedWorkbench
    );

if (builder.ExecutionContext.IsRunMode) // The following project is only added for testing purposes.
{
    // Blazor WebAssembly Standalone project. Its dev server listens on its launch profile's address itself, not on a port
    // Aspire gives it, so that endpoint has no proxy in front of it, which would hold the same port.
    builder.AddProject("clientwebwasm", "../../Client/Bit.Fabric.Client.Web/Bit.Fabric.Client.Web.csproj")
        .WithEndpoint("http", endpoint => endpoint.IsProxied = false)
        .WithExplicitStart();

    var mailpit = builder.AddMailPit("smtp") // For testing purposes only, in production, you would use a real SMTP server.
        .WithOtlpExporter()
        .WithDataVolume("mailpit");

    serverApiProject.WithReference(mailpit);

    if (OperatingSystem.IsWindows())
    {
        // Blazor Hybrid Windows project.
        builder.AddProject("clientwindows", "../../Client/Bit.Fabric.Client.Windows/Bit.Fabric.Client.Windows.csproj")
            .WithExplicitStart();
    }

    // Without Docker or Podman the projects still run, only the containers are missing. See `.docs/20- .NET Aspire.md`.
    builder.RunWithoutContainerRuntime();

    // Keeps the containers alive between runs and stops them once the app host hasn't run for 15 minutes. See `.docs/20- .NET Aspire.md`.
    builder.UsePersistentContainers();

    builder.RemoveWildcardEndpoints();
}

await builder
    .Build()
    .RunAsync();
