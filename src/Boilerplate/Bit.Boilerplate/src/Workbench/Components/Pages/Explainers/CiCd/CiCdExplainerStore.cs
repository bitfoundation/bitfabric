namespace Boilerplate.Workbench.Components.Pages.Explainers.CiCd;

/// <summary>
/// What the reader narrows the CI/CD explainer's settings to: secrets or variables, and a search. It is persisted per
/// view, so a reload or a screenshot shows the list as it was left.
/// </summary>
[BlexStore(Name = "explainer-ci-cd", Persist = true)]
public partial class CiCdExplainerStore
{
    // All, Secret or Variable; null until the reader picks one, which shows all.
    [BlexState] private string? _settingsKind;
    [BlexState] private string? _settingsSearch;

    [BlexAction] private void OnShowSettings(string? kind) => SettingsKind = kind;

    [BlexAction] private void OnSearchSettings(string? text) => SettingsSearch = text;
}
