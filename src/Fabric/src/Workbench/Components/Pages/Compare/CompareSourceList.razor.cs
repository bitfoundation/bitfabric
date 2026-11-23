using Bit.Fabric.Workbench.Components.Pages.Compare.Data;
using Bit.Fabric.Workbench.Components.Pages.HomeDirections;

namespace Bit.Fabric.Workbench.Components.Pages.Compare;

public partial class CompareSourceList
{
    /// <summary>The ids of the sources, in the order the page numbers them (See CompareSubject.SourceIds).</summary>
    [Parameter, EditorRequired] public string[] Ids { get; set; } = [];

    private CompareSource[] Sources => [.. Ids.Select(id => CompareSourceCatalog.All[id])];

    /// <summary>An address as a reader would type it, without its scheme.</summary>
    private static string Shown(string url) => url.Replace("https://", string.Empty, StringComparison.Ordinal).TrimEnd('/');
}
