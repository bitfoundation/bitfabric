using Bit.Fabric.Client.Core.Components.Pages.NewProject;
using Bit.Fabric.Client.Core.Components.Pages.NewProject.Data;

namespace Bit.Fabric.Client.Core.Components.Pages.SetupProject;

/// <summary>
/// bitfabric.co's page for a project that already exists (bitfabric-internal/specs/getting-started.md, Setting up an
/// existing project): a teammate's, the visitor's own on another computer, or an app they left for later. It asks where the
/// project is, which computer this is and which apps to set up on it, then gives the one line: the installer, then bit setup
/// with the repository's address, which clones it first. It reuses the new project page's parts for the computer, the apps,
/// the help and the line, so both pages say it the same way. It keeps nothing: the answers live only in its address.
/// </summary>
public partial class SetupProjectPage
{
    [AutoInject] private UserAgent userAgent = default!;
    [AutoInject] private History history = default!;

    private readonly NewProjectState state = new();

    /// <summary>An address reads left to right in every language; the field's label and description follow the page.</summary>
    private readonly Dictionary<string, object> ltrInput = new() { ["dir"] = "ltr" };

    private string? repository;
    private bool isOnThisComputer;

    private SetupProjectAnswers Answers
    {
        get
        {
            var answers = state.Answers;

            return new()
            {
                Repository = repository,
                IsOnThisComputer = isOnThisComputer,
                Computer = answers.Computer,
                Devices = answers.Devices
            };
        }
    }

    protected override async Task OnInitAsync()
    {
        var query = new Uri(NavigationManager.Uri).Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .GroupBy(pair => Uri.UnescapeDataString(pair[0]), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First() is [_, var value] ? Uri.UnescapeDataString(value.Replace('+', ' ')) : string.Empty, StringComparer.OrdinalIgnoreCase);

        // The apps the link names; the state reads nothing else from this page's address
        state.Restore(key => key is "devices" ? query.GetValueOrDefault(key) : null);
        repository = query.GetValueOrDefault("repo");
        isOnThisComputer = query.GetValueOrDefault("here") is "1";
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

    private void Locate(string? value)
    {
        repository = value;
        WriteAddress();
    }

    private void PickWhere(bool isHere)
    {
        isOnThisComputer = isHere;
        WriteAddress();
    }

    /// <summary>Why the typed address gets no line, or null while it is valid or nothing is typed yet.</summary>
    private string? RepositoryError(SetupProjectAnswers answers) => answers.RepositoryAddress is null || answers.IsRepositoryValid
        ? null
        : Localizer[nameof(AppStrings.SetupProjectRepositoryNotValid)].Value;

    /// <summary>
    /// Keeps the answers in the address, without a history entry for each and without the router: the page stays as it is,
    /// and a reload or a copied address opens it the same way.
    /// </summary>
    private void WriteAddress() => _ = InvokeAsync(async () =>
    {
        var path = new Uri(NavigationManager.Uri).AbsolutePath;

        await history.ReplaceState(null, $"{path}{new Uri(Answers.ShareLink).Query}");
    });

    private void Render() => _ = InvokeAsync(StateHasChanged);

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        state.Changed -= Render;
        state.Changed -= WriteAddress;

        await base.DisposeAsync(disposing);
    }
}
