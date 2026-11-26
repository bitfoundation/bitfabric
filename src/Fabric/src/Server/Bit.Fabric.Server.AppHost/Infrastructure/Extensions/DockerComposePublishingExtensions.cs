using Aspire.Hosting.Docker;
using Aspire.Hosting.Docker.Resources.ComposeNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes;
using Aspire.Hosting.Docker.Resources.ServiceNodes.Swarm;

namespace Aspire.Hosting;

/// <summary>
/// Shapes the Docker Compose project that <c>aspire publish</c> writes and the CD deploys to the server
/// (See bitfabric-internal/.github/workflows/cd-template.yml). Nothing here changes run mode.
/// </summary>
public static class DockerComposePublishingExtensions
{
    extension(IDistributedApplicationBuilder builder)
    {
        /// <summary>
        /// Publish mode only. Sends the images to the registry the CD names, fixes the apps on port 8080 with no host
        /// port, runs them in the environment they are published for, gives every long-running service a restart policy
        /// and a health check, adds the one-shot <c>migrations</c> service, keeps the Aspire dashboard and DbGate on the
        /// server's loopback, puts Caddy on ports 80 and 443 when there is no Cloudflare Tunnel, and wires in the settings
        /// the deploy job supplies: the connection strings and parameters of
        /// the generated <c>.env</c>, the <c>serverweb.env</c> (and <c>serverapi.env</c>) files, and the environment's own
        /// <c>AppCertificate</c>. The Lighthouse service's image goes to the registry with the apps', and the service runs
        /// locked down on a network of its own that only the api joins. The api keeps the source code index of its /docs-mcp
        /// on a <c>docs-mcp</c> volume.
        /// </summary>
        public void ConfigureDockerComposePublishing(
            IResourceBuilder<DockerComposeEnvironmentResource> compose,
            IResourceBuilder<ProjectResource> serverWebProject
            , IResourceBuilder<ProjectResource> serverApiProject
            , IResourceBuilder<ContainerResource> lighthouse
            , IResourceBuilder<ProjectResource>? workbench = null
            )
        {
            if (builder.ExecutionContext.IsPublishMode is false)
                return;

            // The images go to <REGISTRY_ENDPOINT>/<REGISTRY_REPOSITORY>/<resource>:<IMAGE_TAG>, which the CD sets to
            // ghcr.io/<owner>/<repo>/serverweb:<environment>-<commit sha>.
#pragma warning disable ASPIRECOMPUTE003 // AddContainerRegistry is experimental.
            builder.AddContainerRegistry("container-registry",
                builder.AddParameterFromConfiguration("registry-endpoint", "REGISTRY_ENDPOINT"),
                builder.AddParameterFromConfiguration("registry-repository", "REGISTRY_REPOSITORY"));
#pragma warning restore ASPIRECOMPUTE003

            var imageTag = builder.Configuration["IMAGE_TAG"] is { Length: > 0 } tag ? tag : "latest";

            // The project that hosts the api: Server.Web itself, unless the api is standalone.
            var apiProject = serverWebProject;
            apiProject = serverApiProject;

            List<IResourceBuilder<ProjectResource>> projects = [serverWebProject];
            projects.Add(serverApiProject);
            // The Workbench, on Test only: served like the apps, with no settings of its own and no part in their swap.
            if (workbench is not null)
            {
                projects.Add(workbench);
            }

#pragma warning disable ASPIREPIPELINES003 // WithRemoteImageTag is experimental.
            foreach (var project in projects)
            {
                var hasSettings = project != workbench;
                project
                    // A fixed port, because cloudflared reaches the apps at http://serverweb:8080 (and http://serverapi:8080).
                    .WithEndpoint("http", endpoint => endpoint.TargetPort = 8080)
                    // The environment `aspire publish --environment` names (Test or Production). Without it the apps run as
                    // Production everywhere, and AppEnvironment takes its value from it on the server.
                    .WithEnvironment("ASPNETCORE_ENVIRONMENT", builder.Environment.EnvironmentName)
                    .WithRemoteImageTag(imageTag)
                    .PublishAsDockerComposeService((_, service) =>
                    {
                        // No host port: the server keeps every inbound port closed, and cloudflared dials out instead.
                        service.Ports.Clear();
                        service.Restart = "unless-stopped";
                        // The app's own settings that no parameter carries (AI keys, Sentry, external sign-in...), which the
                        // deploy job writes next to docker-compose.yaml.
                        if (hasSettings)
                        {
                            service.EnvFile.Add($"{service.Name}.env");
                        }
                        service.Healthcheck = new()
                        {
                            // The aspnet base image has bash, but neither curl nor wget.
                            Test = ["CMD", "bash", "-c", "exec 3<>/dev/tcp/127.0.0.1/8080 && printf 'GET /alive HTTP/1.1\\r\\nHost: localhost\\r\\nConnection: close\\r\\n\\r\\n' >&3 && head -n 1 <&3 | grep -q ' 200 '"],
                            Interval = "10s",
                            Timeout = "5s",
                            Retries = 6,
                            StartPeriod = "60s"
                        };
                    });
            }

            // bit Fabric's Lighthouse service: its image is built from its Dockerfile and pushed as
            // <registry>/<repository>/lighthouse:<IMAGE_TAG>, the name the deploy job writes as LIGHTHOUSE_IMAGE. Chrome
            // loads customers' pages in it, so it runs as the image's unprivileged user with every capability dropped, no way
            // to gain one, a read-only file system but for /tmp, a memory limit, and no host port.
            lighthouse
                .WithRemoteImageTag(imageTag)
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.Ports.Clear();
                    service.Restart = "unless-stopped";
                    service.CapDrop = ["ALL"];
                    service.SecurityOpt = ["no-new-privileges:true"];
                    service.ReadOnly = true;
                    // Chrome's profiles, one a run, and its shared memory (--disable-dev-shm-usage).
                    service.Tmpfs = ["/tmp:size=1g"];
                    service.Deploy = new() { Resources = new() { Limits = new() { Memory = "2g" } } };
                    service.Healthcheck = new()
                    {
                        Test = ["CMD", "node", "-e", "fetch('http://127.0.0.1:3000/alive').then(r => process.exit(r.ok ? 0 : 1), () => process.exit(1))"],
                        Interval = "10s",
                        Timeout = "5s",
                        Retries = 6,
                        StartPeriod = "20s"
                    };
                });
#pragma warning restore ASPIREPIPELINES003

            // Mailpit is development only. A deployed server sends its mail through a real SMTP server.
            var smtp = builder.AddConnectionString("smtp");
            apiProject.WithReference(smtp);
            // A key that can only read the locked backups bucket, for the health report's entry on them. Only Production is
            // backed up: Test's data is there to try things on.
            if (builder.Environment.EnvironmentName is "Production")
            {
                apiProject.WithReference(builder.AddConnectionString("backups"));
            }
            // ServerApiSettings refuses the template's sample VAPID private key and subject outside Development.
            apiProject.WithEnvironment("AdsPushVapid__PrivateKey", builder.AddParameter("vapid-private-key", secret: true))
                .WithEnvironment("AdsPushVapid__Subject", builder.AddParameter("vapid-subject"));
            // Pre-rendering calls the api over the Docker Compose network rather than through its public address.
            serverWebProject.WithEnvironment("ServerSideHttpClientBaseAddress", serverApiProject.GetEndpoint("http"));

            // The image only carries the development AppCertificate, which the api refuses outside Development. The deploy job
            // writes the environment's own pair, and the retired one while there is one, to the app-certificate folder next to
            // docker-compose.yaml (See AppCertificate.md).
            apiProject.WithEnvironment("AppCertificateDirectory", "/app-certificate")
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.AddVolume(new Volume { Name = "app-certificate", Type = "bind", Source = "./app-certificate", Target = "/app-certificate", ReadOnly = true });
                });

            // The source code index of the api's /docs-mcp (See DocsMcpCodeIndexer): the codebase-memory command line it
            // installs, the sources of the newest bit platform tag and their index - minutes of work a tag - kept across
            // deployments on a volume of their own. It is mounted over /home/app, the home of the aspnet image's app user,
            // which nothing else of the app uses: Docker fills a new volume with what the image holds at its target,
            // ownership included, so the app, which runs as that user, can write to it. Nothing else is needed on the server
            // or in the image: no git (a tag's sources are read from GitHub's archive of it) and no Node.js.
            apiProject.WithEnvironment("DocsMcp__CodeIndex__DataDirectoryPath", "/home/app/docs-mcp")
                .PublishAsDockerComposeService((_, service) =>
                {
                    service.AddVolume(new Volume { Name = "docs-mcp", Type = "volume", Source = "docs-mcp", Target = "/home/app" });
                });

            // The Aspire dashboard and DbGate listen on the server's loopback only, where SSH reaches them: for Production,
            // ssh -L 18888:127.0.0.1:18888 -L 3000:127.0.0.1:3000. Each environment has an address of its own, so Test and
            // Production can share a server.
            var loopback = builder.Environment.EnvironmentName switch { "Production" => "127.0.0.1", "Test" => "127.0.0.2", _ => "127.0.0.3" };

            var dashboardApiKey = builder.AddParameter("dashboard-api-key", secret: true);
            var dashboardBrowserToken = builder.AddParameter("dashboard-browser-token", secret: true);

            // The dashboard keeps its telemetry in SQLite on a volume, so a restart or a deployment doesn't lose it. Aspire's
            // default image comes from the nightly feed, so the release one is named here.
            compose.WithDashboard(dashboard =>
            {
                dashboard
                    .WithImage("aspire/dashboard", "13.6")
                    .WithImageRegistry("mcr.microsoft.com")
                    .WithVolume(VolumeNameGenerator.Generate(dashboard, "data"), "/home/app")
                    .WithEnvironment("ASPIRE_DASHBOARD_APPLICATION_NAME", "Bit.Fabric")
                    .WithEnvironment("ASPIRE_DASHBOARD_PERSISTENCE_MODE", "Resume")
                    .WithEnvironment("ASPIRE_DASHBOARD_DATA_DIRECTORY", "/home/app/dashboard-data")
                    .WithEnvironment("Dashboard__Api__PrimaryApiKey", dashboardApiKey)
                    .WithEnvironment("Dashboard__Frontend__BrowserToken", dashboardBrowserToken);

                apiProject.WithEnvironment("AspireDashboard__Url", dashboard.Resource.PrimaryEndpoint)
                    .WithEnvironment("AspireDashboard__ApiKey", dashboardApiKey);
            });

            // DbGate's integration leaves it out of a publish, so it is put back next to the dashboard.
            // It has no sign-in of its own: a route on the tunnel to it needs Cloudflare Access in front.
            if (builder.Resources.OfType<DbGateContainerResource>().SingleOrDefault() is { } dbGate)
            {
                foreach (var exclusion in dbGate.Annotations.OfType<ManifestPublishingCallbackAnnotation>().ToArray())
                    dbGate.Annotations.Remove(exclusion);

                builder.CreateResourceBuilder(dbGate).PublishAsDockerComposeService((_, service) =>
                {
                    service.Ports.Clear();
                    service.Ports.Add($"{loopback}:3000:3000");
                    service.Restart = "unless-stopped";
                });
            }

            compose.ConfigureComposeFile(composeFile =>
            {
                var api = composeFile.Services[apiProject.Resource.Name];

                var dashboardService = composeFile.Services[$"{compose.Resource.Name}-dashboard"];
                dashboardService.Ports.Clear();
                dashboardService.Ports.Add($"{loopback}:18888:18888");

                // DbGate's integration always writes rediss:// for Redis, which is right in run mode, where Aspire serves Redis
                // over TLS. The published Redis has no TLS.
                if (composeFile.Services.TryGetValue("dbgate", out var dbGateService))
                {
                    foreach (var (name, value) in dbGateService.Environment.Where(variable => variable.Key.StartsWith("URL_")).ToArray())
                        dbGateService.Environment[name] = value.Replace("rediss://", "redis://");
                }


                // The database migrations: a one-shot `migrations` service that runs the api's own image - the same build,
                // with the same settings - with MigrateDatabaseAndExit=true. The deploy job runs it on its own before
                // `docker compose up`, so a failed migration stops the deployment while the running version is left
                // untouched, and the api starts only once it has exited with 0.
                var migrations = new Service
                {
                    Name = "migrations",
                    Image = api.Image,
                    Environment = new(api.Environment) { ["MigrateDatabaseAndExit"] = "true" },
                    EnvFile = [.. api.EnvFile],
                    Volumes = [.. api.Volumes.Select(volume => new Volume { Name = volume.Name, Type = volume.Type, Source = volume.Source, Target = volume.Target, ReadOnly = volume.ReadOnly })],
                    Networks = [.. api.Networks],
                    Restart = "no"
                };
                foreach (var (name, dependency) in api.DependsOn)
                {
                    migrations.DependsOn[name] = new() { Condition = dependency.Condition };
                }
                composeFile.AddService(migrations);
                api.DependsOn["migrations"] = new() { Condition = "service_completed_successfully" };

                composeFile.Services[serverWebProject.Resource.Name].DependsOn[serverApiProject.Resource.Name] = new() { Condition = "service_started" };

                // A new major of PostgreSQL, in two one-shot services that postgresserver waits for. Compose has stopped the
                // server of the older major by the time they run, as it replaces a container before it starts what the new
                // one waits for. `postgresserver-export`, in the older major's image, runs it on a copy of its read-only
                // volume and dumps the databases; `postgresserver-upgrade`, in postgresserver's own image, restores them into
                // this major's volume and checks every table's rows. Every deployment runs both, and once the copy is done
                // they do nothing, so they stay for the next major. See deploy/database.
                var postgres = composeFile.Services["postgresserver"];
                var dataVolume = postgres.Volumes.Single(volume => volume.Target is "/var/lib/postgresql").Source!;
                var previousDataVolume = dataVolume[..^PostgresMajor.DataVolumeSuffix.Length] + PostgresMajor.PreviousDataVolumeSuffix;
                const string dumpsVolume = "postgresserver-upgrade";
                // Compose reads a $ as the start of one of its own variables, and $$ as a $.
                string Script(string name) => File.ReadAllText(Path.Combine(builder.AppHostDirectory, "..", "..", "..", "deploy", "database", name))
                    .ReplaceLineEndings("\n").Replace("$", "$$");
                Volume Mount(string source, string target, bool readOnly = false) => new() { Name = source, Type = "volume", Source = source, Target = target, ReadOnly = readOnly };
                var environment = postgres.Environment.Where(variable => variable.Key.StartsWith("OTEL_") is false).ToDictionary();

                composeFile.AddService(new Service
                {
                    Name = "postgresserver-export",
                    Image = postgres.Image!.Replace($":{PostgresMajor.Tag}", $":{PostgresMajor.PreviousTag}"),
                    Entrypoint = ["bash", "-c", Script("export.sh")],
                    Environment = new(environment),
                    Volumes =
                    [
                        Mount(previousDataVolume, "/var/lib/postgresql-previous", readOnly: true),
                        Mount(dataVolume, "/var/lib/postgresql-new", readOnly: true),
                        Mount(dumpsVolume, "/upgrade")
                    ],
                    NetworkMode = "none",
                    Restart = "no"
                });
                composeFile.AddService(new Service
                {
                    Name = "postgresserver-upgrade",
                    Image = postgres.Image,
                    Entrypoint = ["bash", "-c", Script("upgrade.sh")],
                    Environment = new(environment),
                    Volumes =
                    [
                        Mount(dataVolume, "/var/lib/postgresql"),
                        Mount(previousDataVolume, "/var/lib/postgresql-previous", readOnly: true),
                        Mount(dumpsVolume, "/upgrade")
                    ],
                    NetworkMode = "none",
                    Restart = "no"
                });
                composeFile.Services["postgresserver-upgrade"].DependsOn["postgresserver-export"] = new() { Condition = "service_completed_successfully" };
                composeFile.AddVolume(new Volume { Name = previousDataVolume, Driver = "local" });
                composeFile.AddVolume(new Volume { Name = dumpsVolume, Driver = "local" });
                postgres.DependsOn["postgresserver-upgrade"] = new() { Condition = "service_completed_successfully" };

                // The Lighthouse service is on a network of its own, which the api joins to call it: from there it reaches the
                // internet, and none of the database, Redis, the dashboard or the other services, which are on Aspire's network
                // alone. The migrations, made from the api's service above, stay off it.
                composeFile.Networks["lighthouse"] = new Network { Name = "lighthouse", Driver = "bridge" };

                // The api's /docs-mcp volume, which it is given above.
                composeFile.AddVolume(new Volume { Name = "docs-mcp", Driver = "local" });
                composeFile.Services[lighthouse.Resource.Name].Networks = ["lighthouse"];
                api.Networks.Add("lighthouse");

                // Aspire writes WaitFor as `service_started`: wait for health instead wherever the dependency reports it.
                foreach (var service in composeFile.Services.Values)
                {
                    foreach (var (name, dependency) in service.DependsOn)
                    {
                        if (dependency.Condition is "service_started" && composeFile.Services.TryGetValue(name, out var target) && target.Healthcheck is not null)
                        {
                            dependency.Condition = "service_healthy";
                        }
                    }
                }
            });
        }
    }

    extension<T>(IResourceBuilder<T> resource) where T : IComputeResource
    {
        /// <summary>
        /// Publish mode only: the service comes back with the server, and reports its health to <c>docker compose ps</c>
        /// and to the dependents that wait for it.
        /// </summary>
        public IResourceBuilder<T> PublishAsHealthyComposeService(string healthCheck)
        {
            return resource.PublishAsDockerComposeService((_, service) =>
            {
                service.Restart = "unless-stopped";
                service.Healthcheck = new() { Test = ["CMD-SHELL", healthCheck], Interval = "5s", Timeout = "5s", Retries = 12, StartPeriod = "30s" };
            });
        }
    }

    extension(IResourceBuilder<RedisResource> redis)
    {
        /// <summary>
        /// Aspire publishes Redis as <c>/bin/sh -c "redis-server --requirepass $REDIS_PASSWORD ..."</c> and writes every
        /// <c>WithArgs</c> value after that string, where the shell takes them as its own positional parameters and
        /// redis-server never sees them - published, redis-persistent ran with <c>appendonly no</c>. Run mode folds them
        /// in, so this does the same for the published service, and adds its restart policy and health check.
        /// </summary>
        public IResourceBuilder<RedisResource> PublishAsComposeRedis()
        {
            return redis.PublishAsHealthyComposeService("redis-cli -a \"$$REDIS_PASSWORD\" --no-auth-warning ping | grep -q PONG")
                .PublishAsDockerComposeService((_, service) =>
                {
                    if (service.Command is ["-c", ..] && service.Command.Count > 2)
                    {
                        service.Command = ["-c", string.Join(' ', service.Command.Skip(1).Select(argument => argument.Length is 0 ? "''" : argument))];
                    }
                });
        }
    }
}
