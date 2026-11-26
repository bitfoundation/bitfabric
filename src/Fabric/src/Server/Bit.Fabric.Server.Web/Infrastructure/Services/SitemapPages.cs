using System.Reflection;
using System.Runtime.Loader;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;

namespace Bit.Fabric.Server.Web.Infrastructure.Services;

/// <summary>
/// The public pages, each by its address without a culture, as the sitemap lists them under every culture and the Markdown
/// copies take them (See PageMarkdown).
/// </summary>
public static partial class SitemapPages
{
    public static string[] All() => [.. AssemblyLoadContext.Default.Assemblies.Where(asm => asm.GetName().Name?.Contains("Bit.Fabric.Client") is true)
        .SelectMany(asm => asm.ExportedTypes)
        .Where(att => att.GetCustomAttributes<AuthorizeAttribute>(inherit: true).Any() is false)
        .SelectMany(t => t.GetCustomAttributes<Microsoft.AspNetCore.Components.RouteAttribute>())
        .Where(att => RouteRegex().IsMatch(att.Template) is false)
        .Select(att => att.Template)
        // A page search engines should not index has no place in the list of pages to index, nor has one that
        // only exists to sign in or needs an account to show anything (See NotInSitemapAttribute).
        .Where(url => PageUrls.NotInSitemapUrls.Contains(url) is false)
        .Distinct()];

    [GeneratedRegex(@"\{.*?\}")]
    private static partial Regex RouteRegex();
}
