using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;
using Bit.Fabric.Shared.Features.Tenants;

namespace Bit.Fabric.Client.Core.Components.Pages.Licensing;

/// <summary>
/// Where the link bit CLI prints leads (bitfabric-internal/specs/product.md, A License belongs to a Team): one of the
/// signed-in account's teams gets a free license for the project the link names, the first team picked for them, the only
/// one without asking, and a new one, named here with no elevated session, for someone in none. bit picks the license up by
/// itself when it waits with the link's one-time code; otherwise the page gives a prompt for the customer's own AI first and
/// the command second.
/// </summary>
public partial class LicensePage
{
    [AutoInject] private ILicenseController licenseController = default!;

    [AutoInject] private ITenantController tenantController = default!;

    [AutoInject] private Clipboard clipboard = default!;

    /// <summary>The project's name in its bit.json.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "project")]
    public string? ProjectName { get; set; }

    /// <summary>A string, not a <see cref="Guid"/>: a malformed one shows a line on this page rather than failing to bind.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "id")]
    public string? Id { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "commit")]
    public string? Commit { get; set; }

    /// <summary>The one-time code bit CLI waits for the license with; empty when nothing waits.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "code")]
    public string? Code { get; set; }

    private bool isLoading = true;
    private bool redirectToSignIn;
    private bool isIssuing;
    private string? missing;
    private string? error;
    private string? copied;
    private IssuedLicenseDto? issued;

    private List<LicenseTeamDto> teams = [];
    private string? selectedTeamId;
    private string? teamTitle;
    private string? teamName;

    private BitDropdownItem<string>[] TeamItems => [.. teams.Select(team => new BitDropdownItem<string> { Text = team.Title, Value = team.Id.ToString() })];

    private bool CanIssue => teams.Count > 0
        ? Guid.TryParse(selectedTeamId, out _)
        : string.IsNullOrWhiteSpace(teamTitle) is false && string.IsNullOrWhiteSpace(teamName) is false;

    private string Prompt => LicensePrompt.For(issued?.ProjectName, issued?.License ?? "");

    private string Command => LicensePrompt.Command(issued?.License ?? "");

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        if (Guid.TryParse(Id, out var id) is false || id == Guid.Empty || string.IsNullOrWhiteSpace(ProjectName))
        {
            missing = Localizer["This page opens from the link bit prints in a project's folder. Run any bit command there, such as bit license, for the link."];
        }
        else if (string.IsNullOrWhiteSpace(Commit))
        {
            missing = Localizer["A license names the project's first git commit, and this link has none. Commit the project to git, then run bit license in its folder for a new link."];
        }

        redirectToSignIn = missing is null && (await AuthenticationStateTask).User.IsAuthenticated() is false;

        if (missing is null && redirectToSignIn is false)
        {
            try
            {
                // The first team the account joined is picked; with one there is nothing to ask.
                teams = await licenseController.GetTeams(CurrentCancellationToken);
                selectedTeamId = teams.FirstOrDefault()?.Id.ToString();
            }
            finally
            {
                isLoading = false;
            }
        }
        else
        {
            isLoading = redirectToSignIn;
        }
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        if (redirectToSignIn)
        {
            // Relative, query string and all: the sign-in page only honours an app-relative return url.
            NavigationManager.NavigateTo($"{PageUrls.SignIn}?return-url={Uri.EscapeDataString(NavigationManager.GetRelativePath())}", replace: true);
        }
    }

    /// <summary>The short name follows the team's name until someone edits it: lowercase letters, digits and dashes.</summary>
    private void TitleChanged(string? title)
    {
        if (string.IsNullOrEmpty(teamName) || teamName == ShortNameOf(teamTitle))
        {
            teamName = ShortNameOf(title);
        }

        teamTitle = title;
    }

    private static string ShortNameOf(string? title)
    {
        var letters = string.Join('-', (title ?? "").ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => new string([.. word.Where(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9')).Take(30)]))
            .Where(word => word.Length > 0));

        return letters.Length > 40 ? letters[..40].TrimEnd('-') : letters;
    }

    private async Task Issue()
    {
        if (isIssuing || CanIssue is false) return;

        isIssuing = true;
        error = null;

        try
        {
            if (teams.Count is 0)
            {
                // Making a team asks for no elevated session (bitfabric-internal/specs/monitoring.md, Decisions).
                var team = await tenantController.Create(new() { Name = teamName!.Trim(), Title = teamTitle!.Trim() }, CurrentCancellationToken);
                teams = [new() { Id = team.Id, Title = team.Title ?? team.Name }];
                selectedTeamId = team.Id.ToString();
            }

            issued = await licenseController.Issue(new()
            {
                Project = Guid.Parse(Id!),
                Name = ProjectName,
                Commit = Commit,
                Code = Code,
                TeamId = Guid.Parse(selectedTeamId!)
            }, CurrentCancellationToken);
        }
        catch (ResourceValidationException exception)
        {
            error = string.Join(" ", exception.Payload.Details.SelectMany(detail => detail.Errors).Select(validation => validation.Message));
        }
        catch (KnownException exception)
        {
            error = exception.Message;
        }
        finally
        {
            isIssuing = false;
        }
    }

    private async Task Copy(string text)
    {
        await clipboard.WriteText(text);
        copied = text;
    }
}
