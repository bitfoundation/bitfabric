using Bit.Fabric.Workbench.Components.Pages.Compare.Data;
using Bit.Fabric.Workbench.Components.Pages.HomeDirections;

namespace Bit.Fabric.Workbench.Components.Pages.Compare;

public partial class CompareCards
{
    [Parameter, EditorRequired] public CompareSubject[] Subjects { get; set; } = [];

    /// <summary>Opens the comparison of the card picked, by its key.</summary>
    [Parameter] public EventCallback<string> OnOpen { get; set; }
}
