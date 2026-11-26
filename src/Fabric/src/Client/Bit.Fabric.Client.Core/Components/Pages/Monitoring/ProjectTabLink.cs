namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A tab of a project's page to open (See ProjectPage), on one part of the app when <paramref name="Check"/> is its report
/// key: the address then carries <c>?tab=history&amp;check=smtp</c>, so the link can be shared.
/// </summary>
public sealed record ProjectTabLink(string Tab, string? Check = null);
