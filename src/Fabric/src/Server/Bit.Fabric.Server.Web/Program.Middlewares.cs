using System.Net;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Components.Endpoints;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Yarp.ReverseProxy.Transforms;

namespace Bit.Fabric.Server.Web;

public static partial class Program
{
    extension(WebApplication app)
    {
        /// <summary>
        /// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/middleware/?view=aspnetcore-9.0#middleware-order
        /// </summary>
        public void ConfigureMiddlewares()
        {
            var configuration = app.Configuration;
            var env = app.Environment;

            var settings = app.Services.GetRequiredService<ServerWebSettings>();

            app.UseAppForwardedHeaders();

            app.UseLocalization();


            if (env.IsDevelopment() is false)
            {
                app.UseHttpsRedirection();
                app.UseResponseCompression();

                app.UseSecurityHeaders();
            }

            app.Handle40XStatusCodes();

            if (env.IsDevelopment())
            {
                app.UseDirectoryBrowser();
            }

            app.Use(async (context, next) =>
            {
                context.Response.OnStarting(async () =>
                {
                    if (env.IsDevelopment())
                    {
                        var cacheControl = context.Response.GetTypedHeaders().CacheControl ?? new();
                        cacheControl.NoCache = true;
                        context.Response.GetTypedHeaders().CacheControl = cacheControl;
                    }
                    else
                    {
                        // Caching static files on the Browser and CDN's edge servers.
                        if (context.Request.Query.Any(q => string.Equals(q.Key, "v", StringComparison.InvariantCultureIgnoreCase))
                            && env.WebRootFileProvider.GetFileInfo(context.Request.Path).Exists)
                        {
                            context.Response.GetTypedHeaders().CacheControl = new()
                            {
                                Public = true,
                                NoTransform = true,
                                MaxAge = TimeSpan.FromDays(7)
                            };
                        }
                    }
                });

                await next.Invoke();
            });

            // Where this app's server is, for a monitor given only the app's address. Ahead of the static files, which
            // would serve the copy in Client.Web's wwwroot/.well-known that a statically hosted front end answers with.
            app.Use(async (context, next) =>
            {
                if (context.Request.Path.Equals("/.well-known/bit-app.json", StringComparison.OrdinalIgnoreCase) is false)
                {
                    await next.Invoke();
                    return;
                }

                var origin = new Uri($"{context.Request.Scheme}://{context.Request.Host}{context.Request.PathBase}/");

                await context.Response.WriteAsJsonAsync(new
                {
                    server = new Uri(origin, configuration.GetServerAddress()).ToString(),
                    front_end = "server",
                    manifest = "/manifest.json"
                }, System.Text.Json.JsonSerializerOptions.Web, context.RequestAborted);
            });

            app.UsePrecompressedStaticFiles();

            app.UseStaticFiles();

            // https://yurl.chayev.com/
            app.UseWhen(context => context.Request.Path.StartsWithSegments("/.well-known"), wellKnownApp =>
            {
                // iOS asks for the extension-less path, but the file on disk is the .json one - Azure Static Web Apps
                // decides Content-Type from the extension and would serve an extension-less file as octet-stream,
                // which Apple rejects, so staticwebapp.config.json rewrites the same way. One file, two hosts.
                wellKnownApp.Use(async (context, next) =>
                {
                    if (context.Request.Path.Equals("/.well-known/apple-app-site-association", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Request.Path = "/.well-known/apple-app-site-association.json";
                    }

                    await next.Invoke();
                });

                wellKnownApp.UseStaticFiles(new StaticFileOptions()
                {
                    FileProvider = env.WebRootFileProvider,
                    DefaultContentType = "application/json",
                    ServeUnknownFileTypes = true
                });
            });


            app.UseAuthentication();
            app.UseAuthorization();


            app.UseCultureUrlRedirection();

            app.UseNoIndexHeader();

            app.UseStatusPageHostNames();

            app.UseOutputCache();

            app.UseAntiforgery();

            // A standalone api keeps the feature alone: OAuth lives there, and so does the resource table.
            app.MapAppHealthChecks();

            // Server.Web's own settings, which the standalone api's answer cannot speak for.
            app.MapDeploymentConfiguration();

            // The one-line installers the project wizard gives (See InstallerEndpoints)
            app.MapInstallers();

            // Pre-rendering reads the access_token cookie, so the api calls that write it and the Hangfire dashboard that
            // reads it can go through this host (See RequestHeadersDelegatingHandler).
            var serverApiAddress = string.IsNullOrWhiteSpace(settings.ServerSideHttpClientBaseAddress) is false
                ? settings.ServerSideHttpClientBaseAddress
                : configuration.GetServerAddress();

            if (Uri.TryCreate(serverApiAddress, UriKind.Absolute, out _) is false)
                throw new InvalidOperationException($"'{serverApiAddress}' is not an absolute address. Set ServerAddress (or ServerSideHttpClientBaseAddress) in appsettings.json.");

            // /api is here mostly for the two endpoints that set and remove the pre-rendering http cookie, which the client sends to this host.
            // If you're changing this to forward more, first check whether the client is connecting straight to the server api's address: that is faster.
            foreach (var pattern in new[] { "/api/{**catch-all}", "/hangfire/{**catch-all}" })
            {
                // The api trusts this host's X-Forwarded-* headers: drop whatever the caller sent and set the client's ip alone,
                // as the pre-rendering HttpClient does, so the api keeps seeing its own host.
                app.MapForwarder(pattern, serverApiAddress, transform => transform.AddXForwarded(ForwardedTransformActions.Remove).AddXForwardedFor());
            }

            app.UseSiteMap();

            // Handle the rest of requests with blazor
            var blazorApp = app.MapRazorComponents<Components.App>()
                .CacheOutput("AppResponseCachePolicy")
                .AddInteractiveServerRenderMode()
                .AddInteractiveWebAssemblyRenderMode()
                .AddAdditionalAssemblies(AssemblyLoadContext.Default.Assemblies.Where(asm => asm.GetName().Name?.Contains("Bit.Fabric.Client") is true).ToArray());

            if (settings.WebAppRender.RenderMode is not null && settings.WebAppRender.PrerenderEnabled is false)
            {
                // In the interactive modes with pre-rendering off, nothing of the page is produced on the server -
                // blazor emits only a marker comment and the client renders everything - so endpoint authorization has
                // nothing to protect here and the client handles it.
                blazorApp.AllowAnonymous();
            }
        }

        /// <summary>
        /// Prior to the introduction of .NET 8, the Blazor router effectively managed NotFound and NotAuthorized components during pre-rendering.
        /// However, the current behavior has changed, and it now exclusively returns 401, 403, and 404 status codes with an empty body response!
        /// To address this, we've implemented the UseStatusCodePages middleware to handle responses featuring 401, 403, and 404 status codes that lack a body.
        /// This middleware facilitates redirection to the appropriate not-found and not-authorized pages. Consequently, the status code for these responses becomes 302 (Found).
        /// To mitigate the challenges posed by this situation, our only recourse is to repurpose the 401, 403, and 404 status codes for
        /// not-found and not-authorized responses, at the very least.
        /// </summary>
        private void Handle40XStatusCodes()
        {
            app.Use(async (context, next) =>
            {
                int? statusCode = null;

                if (context.Request.Path.HasValue)
                {
                    if (context.Request.Path.Value.Contains(PageUrls.NotFound, StringComparison.InvariantCultureIgnoreCase))
                    {
                        statusCode = (int)HttpStatusCode.NotFound;
                    }
                    if (context.Request.Path.Value.Contains(PageUrls.NotAuthorized, StringComparison.InvariantCultureIgnoreCase))
                    {
                        statusCode = context.Request.Query["isForbidden"].FirstOrDefault() is "true" ? (int)HttpStatusCode.Forbidden : (int)HttpStatusCode.Unauthorized;
                    }
                }

                if (statusCode is not null)
                {
                    // Applied as the response starts, not before the endpoint runs: a 404 already set by then makes
                    // RazorComponentEndpointInvoker drop the rendered page and leave an empty body for status code pages.
                    // Only over a 200 - the page itself; a redirect (the culture one, for instance) must stay a redirect.
                    context.Response.OnStarting(() =>
                    {
                        if (context.Response.StatusCode is StatusCodes.Status200OK)
                        {
                            context.Response.StatusCode = statusCode.Value;
                        }
                        return Task.CompletedTask;
                    });
                }

                await next.Invoke(context);
            });

            app.UseStatusCodePages(options: new()
            {
                HandleAsync = async (statusCodeContext) =>
                {
                    var httpContext = statusCodeContext.HttpContext;

                    if (httpContext.Response.StatusCode is 401 or 403 &&
                        httpContext.GetEndpoint()?.Metadata.OfType<ComponentTypeMetadata>().Any() is true /* The generation of a 401 or 403 status code is attributed to Blazor. */)
                    {
                        bool is403 = httpContext.Response.StatusCode is 403;

                        var qs = AppQueryStringCollection.Parse(httpContext.Request.QueryString.Value ?? string.Empty);
                        qs.Remove("try_refreshing_token");
                        var returnUrl = UriHelper.BuildRelative(httpContext.Request.PathBase, httpContext.Request.Path,
                                                                QueryString.Create(qs.Select(kv => KeyValuePair.Create(kv.Key, kv.Value?.ToString()))));
                        // return-url has to be encoded as a single value: interpolating it raw would let its inner '&'
                        // separators split into extra outer parameters and truncate the url SignIn navigates back to.
                        var redirectQuery = QueryString.Create(new KeyValuePair<string, string?>[]
                        {
                            new("return-url", returnUrl),
                            new("isForbidden", is403 ? "true" : "false")
                        });
                        httpContext.Response.Redirect($"{PageUrls.NotAuthorized}{redirectQuery}");
                    }
                    else if (httpContext.Response.StatusCode is 404 &&
                        httpContext.GetEndpoint() is null /* Please be aware that certain endpoints, particularly those associated with web API actions, may intentionally return a 404 error. */ &&
                        httpContext.Request.Path.StartsWithSegments("/_framework") is false &&
                        httpContext.Request.Path.StartsWithSegments("/_content") is false)
                    {
                        httpContext.Response.Redirect($"{PageUrls.NotFound}{QueryString.Create("url", httpContext.Request.GetEncodedPathAndQuery())}");
                    }
                }
            });
        }
    }
}
