using Bit.Fabric.Shared.Features.Monitoring;

namespace Microsoft.AspNetCore.Builder;

public static class StatusPageHostNamesExtensions
{
    private const string RewrittenKey = "StatusPageHostName";

    /// <summary>The key WebApplication keeps its endpoints under for UseRouting (See ASP.NET Core's RerouteHelper).</summary>
    private const string GlobalEndpointRouteBuilderKey = "__GlobalEndpointRouteBuilder";

    extension(WebApplication app)
    {
        /// <summary>
        /// A project's status page on the customer's own host name: that host's home page, in each of the app's
        /// languages, is the page. It comes after <c>UseCultureUrlRedirection</c>, so a bare "/" has
        /// already become "/{culture}/", and routes the request again, because its endpoint was matched for the home page
        /// before any middleware ran. Every other host, bit Fabric's own included, goes on as it came.
        /// </summary>
        public WebApplication UseStatusPageHostNames()
        {
            app.Use(async (context, next) =>
            {
                if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                {
                    string? culture = (context.Request.Path.Value ?? "/").Split('/', StringSplitOptions.RemoveEmptyEntries) switch
                    {
                        [] => "",
                        [var first] when CultureInfoManager.GetCultureInfo(first) is not null => first,
                        _ => null
                    };

                    if (culture is not null && await SlugOf(context) is { Length: > 0 } slug)
                    {
                        context.Request.Path = culture is "" ? $"/status/{slug}" : $"/{culture}/status/{slug}";
                        context.SetEndpoint(null);
                        context.Request.RouteValues.Clear();
                        context.Items[RewrittenKey] = true;
                    }
                }

                await next(context);
            });

            // Routing again, over the app's own endpoints, for the status page's path. A branch has no routes of its own
            // (WebApplication.New drops them), so they are handed back the way ASP.NET Core's RerouteHelper does for
            // UseExceptionHandler and UseStatusCodePagesWithReExecute.
            app.UseWhen(context => context.Items.ContainsKey(RewrittenKey), rerouted =>
            {
                rerouted.Properties[GlobalEndpointRouteBuilderKey] = app;
                rerouted.UseRouting();
            });

            return app;
        }
    }

    /// <summary>
    /// The page a host serves (See PublicStatusController.SlugOf), remembered for a minute per host. The api answers none
    /// with an empty body, which reads as an empty string.
    /// </summary>
    private static async Task<string?> SlugOf(HttpContext context)
    {
        var host = context.Request.Host.Host.ToLowerInvariant();
        var cache = context.RequestServices.GetRequiredService<IMemoryCache>();

        return await cache.GetOrCreateAsync($"status-page-host:{host}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(1);
            try
            {
                return await context.RequestServices.GetRequiredService<IPublicStatusController>().SlugOf(host, context.RequestAborted);
            }
            catch (Exception exp) when (exp is not OperationCanceledException)
            {
                return null; // The api not answering is no reason to fail bit Fabric's own pages.
            }
        });
    }
}
