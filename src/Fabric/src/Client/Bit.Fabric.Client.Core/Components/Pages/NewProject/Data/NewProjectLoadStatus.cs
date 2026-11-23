using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>Where loading the release stands.</summary>
public enum NewProjectLoadStatus
{
    Loading,
    Ready,
    Failed
}
