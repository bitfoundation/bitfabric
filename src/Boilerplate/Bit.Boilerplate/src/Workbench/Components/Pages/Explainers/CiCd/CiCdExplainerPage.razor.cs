namespace Boilerplate.Workbench.Components.Pages.Explainers.CiCd;

/// <summary>
/// An example explainer (the explainer skill, .github/agents/explainer.agent.md): how this project's GitHub Actions
/// workflows build and deploy it, as a diagram, cards, a timeline and a searchable list of its settings. Its content
/// is in the template's conditions of the workflows (See CiCdPipeline), so it matches the options the project was created
/// with. It is written in English, the language of the workflows it explains, and has no data to load.
/// </summary>
public partial class CiCdExplainerPage
{
    private const string AllKinds = "All";

    private const string JobsOnGitHub = "GitHub's hosted runners do every build.";

    private const string JobsOnYourServer = "GitHub's hosted runners do every build. Your server runs one job, once ci and the server's build are done.";

    // Per component, as the choice group and the timeline keep their own state on their items.
    private readonly BitChoiceGroupItem<string>[] kinds =
    [
        new() { Value = AllKinds, Text = "All" },
        new() { Value = nameof(CiCdSettingKind.Secret), Text = "Secrets" },
        new() { Value = nameof(CiCdSettingKind.Variable), Text = "Variables" }
    ];

    private readonly List<BitTimelineItem> deploySteps = [.. CiCdPipeline.DeploySteps.Select(step => new BitTimelineItem
    {
        PrimaryText = step.Title,
        SecondaryText = step.Text,
        IconName = step.IconName,
        Color = BitColor.Primary
    })];

    [AutoInject] private CiCdExplainerStore store = default!;

    /// <summary>The settings of the kind the reader picked whose name or description has the search in it.</summary>
    private IReadOnlyList<CiCdSetting> ShownSettings
    {
        get
        {
            var kind = store.SettingsKind ?? AllKinds;
            var search = store.SettingsSearch?.Trim();

            return [.. CiCdPipeline.Settings
                .Where(setting => kind == AllKinds || setting.Kind.ToString() == kind)
                .Where(setting => string.IsNullOrEmpty(search)
                                  || setting.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                                  || setting.Holds.Contains(search, StringComparison.OrdinalIgnoreCase))];
        }
    }

    protected override void OnInitialized()
    {
        Subscribe(store);
    }
}
