
using Bit.Fabric.Server.Api.Features.Attachments;
using Bit.Fabric.Server.Api.Features.Identity;
using Bit.Fabric.Server.Api.Features.Identity.OAuth;
using Bit.Fabric.Server.Api.Features.Identity.OAuth.Services;
using Bit.Fabric.Server.Api.Features.Monitoring;
using Bit.Fabric.Server.Api.Features.PushNotification;
using Hangfire.Storage;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

namespace Bit.Fabric.Server.Api;

public static partial class Program
{
    /// <summary>
    /// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0#middleware-order
    /// </summary>
    public static void ConfigureMiddlewares(this WebApplication app)
    {
        var configuration = app.Configuration;
        var env = app.Environment;

        ServerApiSettings settings = new();
        configuration.Bind(settings);

        app.UseAppForwardedHeaders();

        app.UseLocalization();

        app.UseRequestIdHeader();

        app.UseExceptionHandler();

        if (env.IsDevelopment() is false)
        {
            app.UseHttpsRedirection();
            app.UseResponseCompression();

            app.UseSecurityHeaders();
        }

        if (env.IsDevelopment())
        {
            app.UseDirectoryBrowser();
        }

        app.UseStaticFiles();

        app.UseCors();

        app.UseMiddleware<ForceUpdateMiddleware>();

        app.UseAuthentication();
        app.UseRateLimiter(); // After UseAuthentication, so rate limit partitions can use HttpContext.User.
        app.UseAuthorization();

        app.UseOutputCache();

        app.UseAntiforgery();

        app.MapAppHealthChecks(OAuthEndpoints.AuthorizationFor(OAuthResources.HealthzPath));

        // Production doesn't publish the api's description (bitfabric-internal/specs/build-and-ship.md).
        if (app.Environment.IsProduction() is false)
        {
            app.MapOpenApi().CacheOutput("AppResponseCachePolicy");
            app.MapScalarApiReference().CacheOutput("AppResponseCachePolicy");
            app.MapGet("/", () => Results.Redirect("/scalar")).ExcludeFromDescription();
            app.MapGet("/swagger", () => Results.Redirect("/scalar")).ExcludeFromDescription();
        }

        if (WritesOpenApiDocument is false)
        {
            app.UseHangfireDashboard(options: new()
            {
                DarkModeEnabled = true,
                Authorization = [new HangfireDashboardAuthorizationFilter()]
            });

            app.ScheduleAppRecurringJobs();
        }

        app.MapGet("/api/minimal-api-sample/{routeParameter}", [AppResponseCache(MaxAge = 3600 * 24)] (string routeParameter, [FromQuery] string queryStringParameter) => new
        {
            RouteParameter = routeParameter,
            QueryStringParameter = queryStringParameter
        }).WithTags("Test").CacheOutput("AppResponseCachePolicy").ExcludeFromDescription();

        app.MapHub<Infrastructure.SignalR.AppHub>("/app-hub", Infrastructure.SignalR.AppHubOptions.Configure);

        // Chatbot tools. Isolated from /dev-mcp. Served under the api version the controllers carry as well.
        foreach (var path in new[] { OAuthResources.McpPath, $"{OAuthResources.McpPath}/v1" })
            app.MapMcp(path).RequireAuthorization(OAuthEndpoints.AuthorizationFor(OAuthResources.McpPath));

        // The feature AND two factor, for the app's own bearer scheme or a token issued for this resource; every
        // requirement is read off OAuthResources (OAuthEndpoints.AuthorizationFor).
        app.MapMcp(OAuthResources.DevMcpPath).RequireAuthorization(OAuthEndpoints.AuthorizationFor(OAuthResources.DevMcpPath));

        app.MapOpenIdConfiguration();

        app.MapOAuthEndpoints();

        app.MapControllers()
           .RequireAuthorization()
           .CacheOutput("AppResponseCachePolicy");
    }


    /// <summary>
    /// Recurring hangfire jobs. AddOrUpdate is idempotent and keyed by job id, so re-running it on every start (and
    /// on every replica) simply re-applies the current schedule.
    /// </summary>
    public static WebApplication ScheduleAppRecurringJobs(this WebApplication app)
    {
        var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();

        List<string> scheduled = [];

        recurringJobManager.AddOrUpdate<UserSessionsRetentionJobRunner>(UserSessionsRetentionJobRunner.RecurringJobId,
                                                                       runner => runner.EnforceRetention(CancellationToken.None),
                                                                       Cron.Daily);
        scheduled.Add(UserSessionsRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<UnconfirmedUsersRetentionJobRunner>(UnconfirmedUsersRetentionJobRunner.RecurringJobId,
                                                                           runner => runner.EnforceRetention(CancellationToken.None),
                                                                           Cron.Daily);
        scheduled.Add(UnconfirmedUsersRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<OAuthRetentionJobRunner>(OAuthRetentionJobRunner.RecurringJobId,
                                                                runner => runner.EnforceRetention(CancellationToken.None),
                                                                Cron.Daily);
        scheduled.Add(OAuthRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<PushSubscriptionsRetentionJobRunner>(PushSubscriptionsRetentionJobRunner.RecurringJobId,
                                                                            runner => runner.EnforceRetention(CancellationToken.None),
                                                                            Cron.Daily);
        scheduled.Add(PushSubscriptionsRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<AiChatImagesRetentionJobRunner>(AiChatImagesRetentionJobRunner.RecurringJobId,
                                                                       runner => runner.EnforceRetention(CancellationToken.None),
                                                                       Cron.Hourly);
        scheduled.Add(AiChatImagesRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<ProjectProbeJobRunner>(ProjectProbeJobRunner.RecurringJobId,
                                                              runner => runner.ProbeAll(CancellationToken.None),
                                                              Cron.Minutely);
        scheduled.Add(ProjectProbeJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<ProjectEscalationJobRunner>(ProjectEscalationJobRunner.RecurringJobId,
                                                                   runner => runner.Escalate(CancellationToken.None),
                                                                   Cron.Minutely);
        scheduled.Add(ProjectEscalationJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<ProjectProbesRetentionJobRunner>(ProjectProbesRetentionJobRunner.RecurringJobId,
                                                                        runner => runner.EnforceRetention(CancellationToken.None),
                                                                        Cron.Daily);
        scheduled.Add(ProjectProbesRetentionJobRunner.RecurringJobId);

        recurringJobManager.AddOrUpdate<ProjectScanWeeklyJobRunner>(ProjectScanWeeklyJobRunner.RecurringJobId,
                                                                   runner => runner.ScheduleDue(CancellationToken.None),
                                                                   Cron.Hourly);
        scheduled.Add(ProjectScanWeeklyJobRunner.RecurringJobId);

        // Monday morning in Europe, once a week; ProjectScansEmailJobRunner itself sends a member one a week however often it runs.
        recurringJobManager.AddOrUpdate<ProjectScansEmailJobRunner>(ProjectScansEmailJobRunner.RecurringJobId,
                                                                   runner => runner.SendWeekly(CancellationToken.None),
                                                                   Cron.Weekly(DayOfWeek.Monday, 7));
        scheduled.Add(ProjectScansEmailJobRunner.RecurringJobId);

        app.RemoveUnscheduledRecurringJobs(scheduled);

        return app;
    }

    /// <summary>
    /// A schedule outlives the class it names. Every job id here is nameof(TheRunner), and AddOrUpdate only ever adds,
    /// so renaming a runner - or switching off the feature that registered it - leaves the old id in storage, where
    /// Hangfire retries it until it gives up and reports a job it can no longer load.
    /// </summary>
    private static void RemoveUnscheduledRecurringJobs(this WebApplication app, IReadOnlyCollection<string> scheduled)
    {
        try
        {
            var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
            using var connection = app.Services.GetRequiredService<JobStorage>().GetConnection();

            foreach (var orphan in connection.GetRecurringJobs().Select(job => job.Id).Except(scheduled).ToArray())
            {
                recurringJobManager.RemoveIfExists(orphan);
                app.Logger.LogWarning("Removed recurring job {RecurringJobId}: it is no longer registered by ScheduleAppRecurringJobs.", orphan);
            }
        }
        catch (Exception exception)
        {
            // Tidying old schedules must never be what stops the app from starting.
            app.Logger.LogError(exception, "Could not prune unregistered recurring jobs.");
        }
    }

    /// <summary>
    /// This allows other backends to retrieve the OpenID Connect configuration and the public key for validating JWT tokens issued by this server.
    /// Checkout AppCertificate.md for more information.
    /// </summary>
    public static WebApplication MapOpenIdConfiguration(this WebApplication app)
    {
        var jwks = AppCertificateService.GetPublicSecurityKeys(app.Configuration)
            .Select(publicKey =>
            {
                var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(publicKey);
                jwk.Use = "sig";
                jwk.Alg = SecurityAlgorithms.RsaSha256;
                return jwk;
            })
            .ToArray();

        app.MapGet("/.well-known/openid-configuration", (HttpRequest request) =>
        {
            var baseUrl = request.GetBaseUrl();
            return new
            {
                // Must be derived exactly as AppJwtSecureDataFormat derives it when minting, or a consumer that trusts
                // this document rejects every token it was published to validate.
                issuer = request.GetIssuer(),
                jwks_uri = new Uri(baseUrl, ".well-known/jwks"),
                id_token_signing_alg_values_supported = new[] { SecurityAlgorithms.RsaSha256 },
            };
        });

        app.MapGet("/.well-known/jwks", () =>
        {
            return new
            {
                keys = jwks
            };
        });

        return app;
    }
}
