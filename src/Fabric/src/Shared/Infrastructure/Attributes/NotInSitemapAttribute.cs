namespace Bit.Fabric.Shared.Infrastructure.Attributes;

/// <summary>
/// Leaves a <see cref="PageUrls"/> url out of <c>sitemap.xml</c> while search engines may still index it: a page that only
/// exists to sign in or to create an account, or one that needs an account to show anything of use. The sitemap is the list
/// of pages the site asks search engines to crawl and rank, and such a page there spends a crawler's time on a form. A search
/// for the product's sign-in still finds it through the links that lead to it. A page that carries
/// <see cref="NoIndexAttribute"/> is left out of the sitemap already.
/// </summary>
/// <example>
/// People search for "bit Fabric sign in", so the sign-in page stays indexable, but it is a form, not a page to rank:
/// <code>
/// [Description("Sign in to your account.")]
/// [NotInSitemap]
/// public const string SignIn = "/sign-in";
/// </code>
/// <c>/en-US/sign-in</c> carries no noindex, so a search still finds it, while <c>sitemap.xml</c> leaves it out. A page a
/// search should never land on, such as <c>/reset-password</c>, takes <see cref="NoIndexAttribute"/> instead.
/// </example>
[AttributeUsage(AttributeTargets.Field)]
public sealed class NotInSitemapAttribute : Attribute
{
}
