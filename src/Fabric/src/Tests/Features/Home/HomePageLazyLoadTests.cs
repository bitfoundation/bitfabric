using System.Reflection;
using Bit.Fabric.Client.Core.Components.Layout;
using Bit.Fabric.Client.Core.Components.Pages.Home;
using Bit.Fabric.Client.Core.Infrastructure.Services;

namespace Bit.Fabric.Tests.Features.Home;

/// <summary>
/// The home page loads the films' library and Bonboarding lazily, so in Blazor WebAssembly the code it runs
/// before they are loaded may not name their types where the runtime resolves them as it loads a type: a field, the cached
/// delegate of a lambda, a captured variable, a method's signature. One did (2026-11-08): the films' captions were read
/// with a lambda typed with a scene, which the compiler kept in a static field of <c>HomePage+&lt;&gt;c</c>, so the page's
/// first lambda of any kind failed with "Could not load file or assembly 'Bit.Fabric.Client.Films'" and the onboarding never
/// started. Blazor Server has every assembly loaded and the UI tests run on it, so only a check of the types finds it.
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class HomePageLazyLoadTests
{
    private static readonly string[] LazyAssemblies = ["Bit.Fabric.Client.Films", "Bit.Bonboarding", "Bit.Fabric.Client.Diagrams", "Bit.Blow", "Bit.Bmotion"];

    /// <summary>The three pages, what they share, and what hosts the onboarding (bitfabric-internal/specs/home-page.md, 2026-11-18).</summary>
    private static readonly Type[] RunBeforeTheyLoad =
    [
        typeof(HomePage), typeof(FeaturesPage), typeof(ContentsPage), typeof(AppHomePageBase), typeof(AppHomeFilmDialog), typeof(HomeOnboardingService), typeof(AppOnboardingHost)
    ];

    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void TheHomePage_Should_NameNoLazilyLoadedType_WhereItsTypesAreLoaded()
    {
        var found = RunBeforeTheyLoad.SelectMany(WithNested)
            .SelectMany(type => MembersOf(type).Where(m => IsLazy(m.Type)).Select(m => $"{type.FullName}.{m.Name}: {m.Type}"))
            .ToArray();

        Assert.IsEmpty(found, $"Named before {string.Join(" and ", LazyAssemblies)} are loaded:{Environment.NewLine}{string.Join(Environment.NewLine, found)}");
    }

    private static IEnumerable<Type> WithNested(Type type) => type.GetNestedTypes(Declared).SelectMany(WithNested).Prepend(type);

    private static IEnumerable<(string Name, Type Type)> MembersOf(Type type) =>
        type.GetFields(Declared).Select(f => (f.Name, f.FieldType))
            .Concat(type.GetProperties(Declared).Select(p => (p.Name, p.PropertyType)))
            .Concat(type.GetMethods(Declared).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType).Select(t => (m.Name, t))))
            .Append(("base", type.BaseType ?? typeof(object)))
            .Concat(type.GetInterfaces().Select(i => ("interface", i)));

    private static bool IsLazy(Type type)
    {
        if (type.HasElementType)
            return IsLazy(type.GetElementType()!);

        if (type.IsGenericParameter)
            return false;

        return LazyAssemblies.Contains(type.Assembly.GetName().Name) || type.GetGenericArguments().Any(IsLazy);
    }
}
