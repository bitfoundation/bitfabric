using Microsoft.Extensions.Configuration;

namespace Bit.Fabric.Tests.Features.Configuration;

/// <summary>
/// Test is where a change is tried before it reaches Production, so it reads the settings Production reads
/// (bitfabric-internal/specs/build-and-ship.md, "Test mirrors Production"). The deployed Test runs as <c>Test</c> and
/// never reads an <c>appsettings.Production.json</c>, so a value put there alone changes Production and leaves Test behind
/// without anyone deciding it. Prerendering and the CDN edge cache were both left behind that way.
/// <para>
/// This compares, for every project, what <c>appsettings.json</c> and each environment's own file give Test and
/// Production. The settings the GitHub environments keep as secrets are not here, since the repository doesn't hold them.
/// </para>
/// </summary>
[TestClass, TestCategory("UnitTest")]
public class EnvironmentParityTests
{
    /// <summary>
    /// What makes Test a separate environment rather than a different one. Every other difference is drift.
    /// </summary>
    private static readonly HashSet<string> environmentOwnSettings = new(StringComparer.OrdinalIgnoreCase)
    {
        "WindowsDownloadUrl", // Each environment's own installer, on its own hostname (bitfabric-internal/specs/build-and-ship.md).
    };

    [TestMethod]
    public void EveryProject_Should_GiveTestTheSettingsProductionHas()
    {
        var srcDirectory = Directory.GetParent(AppTestServer.ProjectDirectoryOf("Bit.Fabric.Server.Web"))!.Parent!.FullName;

        var projectDirectories = Directory.EnumerateFiles(srcDirectory, "appsettings.json", SearchOption.AllDirectories)
            .Select(Path.GetDirectoryName)
            .Where(directory => directory!.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(part => part is "bin" or "obj" or "node_modules") is false)
            .ToArray();

        Assert.IsGreaterThan(5, projectDirectories.Length, $"Expected the projects' settings under {srcDirectory}.");

        var drift = new List<string>();

        foreach (var projectDirectory in projectDirectories)
        {
            var production = ReadSettings(projectDirectory!, "Production");
            var test = ReadSettings(projectDirectory!, "Test");

            foreach (var key in production.Keys.Union(test.Keys, StringComparer.OrdinalIgnoreCase))
            {
                if (key.EndsWith("_Comment", StringComparison.Ordinal) || environmentOwnSettings.Contains(key.Split(':').Last()))
                    continue;

                production.TryGetValue(key, out var productionValue);
                test.TryGetValue(key, out var testValue);

                if (productionValue != testValue)
                {
                    drift.Add($"{Path.GetRelativePath(srcDirectory, projectDirectory!)}: {key} is '{productionValue}' in Production and '{testValue}' in Test");
                }
            }
        }

        Assert.IsEmpty(drift, "A setting differs between Test and Production. Put it in appsettings.json for both, " +
            "or, if the user decided Test differs, record why in bitfabric-internal/specs/build-and-ship.md and add it to environmentOwnSettings:" +
            Environment.NewLine + string.Join(Environment.NewLine, drift));
    }

    private static Dictionary<string, string?> ReadSettings(string projectDirectory, string environmentName)
    {
        return new ConfigurationBuilder()
            .SetBasePath(projectDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
            .Build()
            .AsEnumerable()
            .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
    }
}
