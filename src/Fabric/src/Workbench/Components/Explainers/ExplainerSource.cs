namespace Bit.Fabric.Workbench.Components.Explainers;

/// <summary>A source of an explainer: a page at <paramref name="Url"/>, or a file of the repository when it has none.</summary>
public sealed record ExplainerSource(string Text, string? Url = null, string? Note = null);
