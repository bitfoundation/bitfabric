// [mirror] the bitplatform Network's locations - keep in sync with:
// - src/Network/src/locations.ts
namespace Bit.Fabric.Shared.Features.Monitoring;

/// <summary>
/// Where the bitplatform Network can check a project from (bitfabric-internal/specs/monitoring.md, Germany, plus the
/// locations the customer picks): the areas a hint really places a check in. South America, Africa and the Middle East are
/// left out, since a check asked for there runs in a nearby area instead. Every reading names the data center it really
/// ran in.
/// </summary>
public static class NetworkLocations
{
    /// <summary>
    /// The code each location is stored by, its name in English, which is also the region its readings carry, and the key
    /// of the name a page shows in the reader's language (See MonitoringWords.Region).
    /// </summary>
    public static readonly NetworkLocation[] All =
    [
        new("wnam", "Western North America", nameof(AppStrings.MonitoringLocationWesternNorthAmerica)),
        new("enam", "Eastern North America", nameof(AppStrings.MonitoringLocationEasternNorthAmerica)),
        new("weur", "Western Europe", nameof(AppStrings.MonitoringLocationWesternEurope)),
        new("eeur", "Eastern Europe", nameof(AppStrings.MonitoringLocationEasternEurope)),
        new("apac", "Asia-Pacific", nameof(AppStrings.MonitoringLocationAsiaPacific)),
        new("apac-ne", "Northeast Asia-Pacific", nameof(AppStrings.MonitoringLocationNortheastAsiaPacific)),
        new("apac-se", "Southeast Asia-Pacific", nameof(AppStrings.MonitoringLocationSoutheastAsiaPacific)),
        new("oc", "Oceania", nameof(AppStrings.MonitoringLocationOceania))
    ];

    /// <summary>What a new project is checked from, besides bit Fabric's own server.</summary>
    public static readonly string[] Defaults = ["enam", "oc"];

    public static NetworkLocation? Find(string? code) => All.FirstOrDefault(location => location.Code == code);

    /// <summary>The location a region's readings come from, by the name they carry.</summary>
    public static NetworkLocation? FindByName(string? name) => All.FirstOrDefault(location => location.Name == name);
}

/// <param name="Code">The location's hint, such as <c>enam</c>.</param>
/// <param name="Name">Its name in English, such as "Eastern North America".</param>
/// <param name="NameKey">The AppStrings key of its name, which every language translates.</param>
public sealed record NetworkLocation(string Code, string Name, string NameKey);
