using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

/// <summary>The release the wizard installs: bit CLI's version, the day it came out, and the computers its one-line installer is signed for.</summary>
public sealed record NewProjectRelease(string Version, DateOnly ReleasedOn, NewProjectComputer[] Installers)
{
    public static NewProjectRelease Current { get; } = new("11.0.0", new(2026, 11, 3), [NewProjectComputer.Windows, NewProjectComputer.MacOs, NewProjectComputer.Linux]);

    public bool HasInstaller(NewProjectComputer computer) => Installers.Contains(computer);
}
