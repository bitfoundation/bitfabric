using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Builder;

public static partial class WebApplicationExtensions
{
    extension(WebApplication app)
    {
        /// <summary>
        /// Makes <c>/{culture}/...</c> the one url a Blazor page is served under: 302s <c>/about</c>,
        /// <c>/about?culture=fa-IR</c> and <c>/FA-ir/about</c> onto the canonical form. With the culture in the path
        /// the url alone identifies the language variant, which is what lets pre-rendered pages be cached on the CDN
        /// edge and in the browser (See <c>AppResponseCachePolicy</c>) while one purge by the bare path still clears
        /// every culture of a page. The redirect target is whatever <c>UseLocalization</c>'s provider chain resolved
        /// (query, route, cookie, Accept-Language) - the user's preference decides where a culture-less url lands,
        /// while a url that names a culture is served in that culture for everyone. The redirect itself is
        /// per-caller: 302 and never cacheable.
        /// </summary>
        public WebApplication UseCultureUrlRedirection()
        {
            if (CultureInfoManager.InvariantGlobalization)
                return app;

            app.Use(async (context, next) =>
            {
                var request = context.Request;

                if ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) is false
                    || context.IsBlazorPageContext() is false
                    // The service worker's app-shell request must be served as requested: a service worker may not
                    // answer a navigation with a `redirected` response - and with `no-prerender` nothing is rendered.
                    || request.Query.ContainsKey("no-prerender"))
                {
                    await next(context);
                    return;
                }

                // What UseLocalization resolved for this request; always one of the supported cultures.
                var culture = CultureInfo.CurrentUICulture.Name;

                var path = request.Path.Value ?? "/";
                var segments = path.Split('/'); // The path always starts with '/', so segments[0] is empty.
                var firstSegment = segments.Length > 1 ? segments[1] : string.Empty;

                var hasCultureQueryParameter = request.Query.ContainsKey("culture");

                if (hasCultureQueryParameter is false && string.Equals(firstSegment, culture, StringComparison.Ordinal))
                {
                    await next(context); // Already canonical.
                    return;
                }

                // A miscased or query-overruled culture segment is replaced rather than prefixed twice.
                var barePath = CultureInfoManager.GetCultureInfo(firstSegment) is not null
                    ? $"/{string.Join('/', segments.Skip(2))}"
                    : path;

                var query = request.QueryString;
                if (hasCultureQueryParameter)
                {
                    var queryParameters = AppQueryStringCollection.Parse(query.Value ?? string.Empty);
                    queryParameters.Remove("culture");
                    query = queryParameters is { Count: > 0 } ? new QueryString($"?{queryParameters}") : QueryString.Empty;
                }

                // 302 and never cacheable: the target depends on the caller's cookie and Accept-Language, and a
                // remembered redirect would pin one user's language onto a url forever.
                context.Response.GetTypedHeaders().CacheControl = new() { NoStore = true, Private = true };
                context.Response.Redirect(UriHelper.BuildRelative(request.PathBase, $"/{culture}{barePath}", query));
            });

            return app;
        }

        /// <summary>
        /// Serves the .br file the publish wrote next to a static file to a browser that accepts Brotli, _framework's
        /// included, instead of leaving UseResponseCompression to compress it again.
        /// </summary>
        public WebApplication UsePrecompressedStaticFiles()
        {
            var contentTypes = new FileExtensionContentTypeProvider();
            contentTypes.Mappings[".br"] = "application/octet-stream";

            app.UseWhen(context => (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                && context.GetEndpoint()?.RequestDelegate is null
                && contentTypes.TryGetContentType(context.Request.Path.Value!, out _)
                && app.Environment.WebRootFileProvider.GetFileInfo($"{context.Request.Path}.br").Exists, precompressedApp =>
            {
                precompressedApp.Use(async (context, next) =>
                {
                    context.Response.Headers.Append(HeaderNames.Vary, HeaderNames.AcceptEncoding);

                    if (context.Request.GetTypedHeaders().AcceptEncoding.Any(encoding => encoding.Value.Equals("br", StringComparison.OrdinalIgnoreCase) && encoding.Quality is not 0))
                    {
                        context.Request.Path = $"{context.Request.Path}.br";
                        context.Response.Headers.ContentEncoding = "br";
                        context.Response.GetTypedHeaders().CacheControl = new() { NoTransform = true };
                    }

                    await next.Invoke();
                });

                precompressedApp.UseStaticFiles(new StaticFileOptions
                {
                    ContentTypeProvider = contentTypes,
                    OnPrepareResponse = file =>
                    {
                        if (file.Context.Response.Headers.ContentEncoding == "br"
                            && contentTypes.TryGetContentType(Path.GetFileNameWithoutExtension(file.File.Name), out var contentType))
                        {
                            file.Context.Response.ContentType = contentType;
                        }
                    }
                });
            });

            return app;
        }

        /// <summary>
        /// Sends <c>X-Robots-Tag: noindex</c> with every page whose <see cref="PageUrls"/> url carries
        /// <see cref="NoIndexAttribute"/>. <c>AppPageData</c> renders the same as a meta tag, but with pre-rendering off
        /// that tag only exists once the page's scripts have run, and a header is read from the first response.
        /// </summary>
        public WebApplication UseNoIndexHeader()
        {
            app.Use(async (context, next) =>
            {
                if (context.IsBlazorPageContext() && PageUrls.IsNoIndex(context.Request.GetUri()))
                {
                    context.Response.Headers["X-Robots-Tag"] = "noindex";
                }

                await next(context);
            });

            return app;
        }

        public WebApplication UseSiteMap()
        {
            const string siteMapHeader = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<urlset xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">";

            app.MapGet("/sitemap_index.xml", [AppResponseCache(SharedMaxAge = 3600 * 24 * 7)] async (context) =>
            {
                // The conditional may not live inside the xml literal below. A template directive is a line based text
                // directive, so the engine strips it from generated projects wherever it sits - but in this repo's own
                // tree, where no engine runs, those two lines are ordinary string content and get served as character
                // data inside <sitemapindex>, which is not valid against the sitemaps.org schema.
                List<string> sitemaps = ["sitemap.xml"];

                var baseUrl = context.Request.GetBaseUrl();

                var sitemapIndex = @$"<?xml version=""1.0"" encoding=""UTF-8""?>
<sitemapindex xmlns=""http://www.sitemaps.org/schemas/sitemap/0.9"">
    {string.Join(Environment.NewLine, sitemaps.Select(sitemap => $"<sitemap><loc>{new Uri(baseUrl, sitemap)}</loc></sitemap>"))}
</sitemapindex>";

                context.Response.Headers.ContentType = "application/xml";

                await context.Response.WriteAsync(sitemapIndex, context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            app.MapGet("/sitemap.xml", [AppResponseCache(SharedMaxAge = 3600 * 24 * 7)] async (context) =>
            {
                var urls = AssemblyLoadContext.Default.Assemblies.Where(asm => asm.GetName().Name?.Contains("Bit.Fabric.Client") is true)
                     .SelectMany(asm => asm.ExportedTypes)
                     .Where(att => att.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() is false)
                     .SelectMany(t => t.GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>())
                     .Where(att => RouteRegex().IsMatch(att.Template) is false)
                     .Select(att => att.Template)
                     // A page search engines should not index has no place in the list of pages to index, nor has one that
                     // only exists to sign in or needs an account to show anything (See NotInSitemapAttribute).
                     .Where(url => PageUrls.NotInSitemapUrls.Contains(url) is false)
                     .Distinct()
                     .ToArray();

                // Culture-prefixed urls only: the bare url always 302s (See UseCultureUrlRedirection), and a sitemap
                // entry that always redirects is one crawlers flag rather than index.
                urls = CultureInfoManager.InvariantGlobalization is false
                        ? [.. CultureInfoManager.SupportedCultures.SelectMany(sc => urls.Select(url => $"{sc.Culture.Name}{url}"))]
                        : urls;

                var baseUrl = context.Request.GetBaseUrl();

                var siteMap = @$"{siteMapHeader}
    {string.Join(Environment.NewLine, urls.Select(u => $"<url><loc>{new Uri(baseUrl, u)}</loc></url>"))}
</urlset>";

                context.Response.Headers.ContentType = "application/xml";

                await context.Response.WriteAsync(siteMap, context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            app.MapGet("/robots.txt", [AppResponseCache(SharedMaxAge = 3600 * 24 * 7)] async (context) =>
            {
                var robots = "User-agent: *\nDisallow:\n";

                if (app.Environment.IsProduction())
                {
                    robots += $"\nSitemap: {new Uri(context.Request.GetBaseUrl(), "sitemap_index.xml")}\n";
                }

                context.Response.Headers.ContentType = "text/plain; charset=utf-8";

                await context.Response.WriteAsync(robots, context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            // https://llmstxt.org - a markdown file that tells AI agents what the site is and which of its pages to read.
            // Written by hand for bit Fabric (Seo/llms.txt) rather than made of PageUrls' descriptions, which describe the
            // template's pages, those that need an account included. Its links carry this host's own address, so the Test
            // site's lead to the Test site. Its lines about the plans are kept only where /pricing shows them (See WithPlans).
            var llmsTxt = WithPlans(ReadSeoFile("llms.txt"));

            app.MapGet("/llms.txt", [AppResponseCache(SharedMaxAge = 3600 * 24 * 7)] async (context) =>
            {
                context.Response.Headers.ContentType = "text/plain; charset=utf-8";

                await context.Response.WriteAsync(WithOrigin(llmsTxt, context), context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            // /pricing as Markdown for AI agents, which the page links from its head. AppPricing writes it from the texts and
            // the bill the page renders, so the two can't say different things; in English, the language agents read, with
            // links that carry this host's own address. Not where the page says "Coming soon" (See AppPricing.IsShown).
            if (AppPricing.IsShown)
            {
                app.MapGet(AppPricing.MarkdownUrl, [AppResponseCache(SharedMaxAge = 3600 * 24)] async (HttpContext context, IStringLocalizer<AppStrings> localizer) =>
                {
                    CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfoManager.DefaultCulture;

                    var origin = context.Request.GetBaseUrl().ToString().TrimEnd('/');
                    var markdown = AppPricing.Markdown(localizer,
                        pageUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing)}",
                        getStartedUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject)}");

                    context.Response.Headers.ContentType = "text/markdown; charset=utf-8";

                    await context.Response.WriteAsync(markdown, context.RequestAborted);
                }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");
            }

            // /cost as Markdown for AI agents, which the page links from its head. AppCostCase writes it from the texts and
            // the figures the page renders, so the two can't say different things; in English, with links that carry this
            // host's own address.
            app.MapGet(AppCostCase.MarkdownUrl, [AppResponseCache(SharedMaxAge = 3600 * 24)] async (HttpContext context, IStringLocalizer<AppStrings> localizer) =>
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfoManager.DefaultCulture;

                var origin = context.Request.GetBaseUrl().ToString().TrimEnd('/');
                var markdown = AppCostCase.Markdown(localizer,
                    pageUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cost)}",
                    pricingUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.Pricing)}",
                    getStartedUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject)}");

                context.Response.Headers.ContentType = "text/markdown; charset=utf-8";

                await context.Response.WriteAsync(markdown, context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            // /cli as Markdown for AI agents, which the page links from its head. AppCliGuide writes it from the texts the page
            // renders, so the two can't say different things; in English, with links that carry this host's own address.
            app.MapGet(AppCliGuide.MarkdownUrl, [AppResponseCache(SharedMaxAge = 3600 * 24)] async (HttpContext context, IStringLocalizer<AppStrings> localizer) =>
            {
                CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfoManager.DefaultCulture;

                var origin = context.Request.GetBaseUrl().ToString().TrimEnd('/');
                var markdown = AppCliGuide.Markdown(localizer,
                    pageUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.Cli)}",
                    getStartedUrl: $"{origin}{CultureInfoManager.GetCurrentCultureUrl(PageUrls.NewProject)}");

                context.Response.Headers.ContentType = "text/markdown; charset=utf-8";

                await context.Response.WriteAsync(markdown, context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            // https://www.rfc-editor.org/rfc/rfc9116 - whom to tell about a vulnerability. Its Expires is a date in the file,
            // renewed by hand before it passes (See HomePageSeoTests).
            var securityTxt = ReadSeoFile("security.txt");

            app.MapGet("/.well-known/security.txt", [AppResponseCache(SharedMaxAge = 3600 * 24)] async (context) =>
            {
                context.Response.Headers.ContentType = "text/plain; charset=utf-8";

                await context.Response.WriteAsync(WithOrigin(securityTxt, context), context.RequestAborted);
            }).CacheOutput("AppResponseCachePolicy").WithTags("Sitemaps");

            return app;
        }
    }

    /// <summary>A text file embedded in Server.Web from its Seo folder, with LF line ends whatever the checkout did to them.</summary>
    private static string ReadSeoFile(string name)
    {
        using var stream = typeof(WebApplicationExtensions).Assembly.GetManifestResourceStream($"Seo/{name}")
            ?? throw new InvalidOperationException($"Seo/{name} isn't embedded in Server.Web.");
        using var reader = new StreamReader(stream);

        return reader.ReadToEnd().ReplaceLineEndings("\n");
    }

    /// <summary>
    /// The file without the lines this site mustn't say: one starting with <c>{{IfPricing}}</c> stays only where /pricing shows the
    /// plans, and one starting with <c>{{IfNoPricing}}</c> only where it says "Coming soon" (See AppPricing.IsShown); the marker goes.
    /// </summary>
    private static string WithPlans(string text)
    {
        const string ifPricing = "{{IfPricing}}";
        const string ifNoPricing = "{{IfNoPricing}}";

        var lines = new List<string>();

        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith(ifPricing, StringComparison.Ordinal))
            {
                if (AppPricing.IsShown) lines.Add(line[ifPricing.Length..]);
            }
            else if (line.StartsWith(ifNoPricing, StringComparison.Ordinal))
            {
                if (AppPricing.IsShown is false) lines.Add(line[ifNoPricing.Length..]);
            }
            else
            {
                lines.Add(line);
            }
        }

        return string.Join('\n', lines);
    }

    /// <summary>The file with <c>{{Origin}}</c> replaced by the address this host answers on, without its trailing slash.</summary>
    private static string WithOrigin(string text, HttpContext context)
        => text.Replace("{{Origin}}", context.Request.GetBaseUrl().ToString().TrimEnd('/'), StringComparison.Ordinal);

    [GeneratedRegex(@"\{.*?\}")]
    private static partial Regex RouteRegex();
}
