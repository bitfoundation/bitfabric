using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Parts;

namespace Bit.Fabric.Client.Core.Components.Pages.NewProject;

/// <summary>
/// bitfabric.co's new project page: the questions a semi-technical founder can answer, a sketch of their product that follows
/// the answers, and the one line that creates it on their computer. It needs no account and no
/// assistant, and every question has a recommended answer and a help panel. It asks one step at a time, the design
/// picked in the Workbench. The answers live in the page's address, so a reload, a copied link or one sent
/// to a computer opens it as it was.
/// </summary>
public partial class NewProjectPage
{
    [AutoInject] private UserAgent userAgent = default!;
    [AutoInject] private History history = default!;

    private readonly NewProjectState state = new();

    private NewProjectState Store => state;

    private NewProjectAnswers Answers => state.Answers;

    private static bool IsRtl => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

    private void OpenHelp(string topic) => state.OpenHelp(topic);

    private string[] StepTitles =>
    [
        Localizer[nameof(AppStrings.NewProjectStepProduct)],
        Localizer[nameof(AppStrings.NewProjectStepWhatItDoes)],
        Localizer[nameof(AppStrings.NewProjectStepExtras)],
        Localizer[nameof(AppStrings.NewProjectStepDevices)],
        Localizer[nameof(AppStrings.NewProjectStepGetIt)]
    ];

    // The arrow points the way the text reads.
    private static string BackIcon => IsRtl ? BitIconName.Forward : BitIconName.Back;

    /// <summary>The step shown; the first while the name is invalid, so no line is made from a name that can't name the project.</summary>
    private int CurrentStep => Answers.IsNameValid ? Math.Clamp(Store.Step, 0, StepTitles.Length - 1) : 0;

    protected override async Task OnInitAsync()
    {
        var query = new Uri(NavigationManager.Uri).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(pair => Uri.UnescapeDataString(pair[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First() is [_, var value] ? Uri.UnescapeDataString(value.Replace('+', ' ')) : string.Empty, StringComparer.OrdinalIgnoreCase);
        state.Restore(key => query.GetValueOrDefault(key));
        state.Changed += Render;

        await base.OnInitAsync();
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        state.Changed += WriteAddress;

        // The visitor's operating system, so the computer is guessed for them; a phone or tablet leaves it to them.
        var browser = await userAgent.Extract();
        state.ReadBrowser(browser.OsName);
    }

    /// <summary>
    /// Keeps the answers and the step in the address, without a history entry for each and without the router: the page
    /// stays as it is, and a reload or a copied address opens it the same way.
    /// </summary>
    private void WriteAddress() => _ = InvokeAsync(async () =>
    {
        var share = new Uri(Answers.ShareLink).Query;
        var step = CurrentStep is 0 ? string.Empty : $"step={CurrentStep.ToString(CultureInfo.InvariantCulture)}";
        var query = (share, step) switch
        {
            (_, "") => share,
            ("", _) => $"?{step}",
            _ => $"{share}&{step}"
        };
        var path = new Uri(NavigationManager.Uri).AbsolutePath;

        await history.ReplaceState(null, $"{path}{query}");
    });

    private void Render() => _ = InvokeAsync(StateHasChanged);

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        state.Changed -= Render;
        state.Changed -= WriteAddress;

        await base.DisposeAsync(disposing);
    }
}
