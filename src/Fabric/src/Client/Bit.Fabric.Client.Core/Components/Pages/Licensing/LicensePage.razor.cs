using Bit.Fabric.Shared.Features.Licensing;
using Bit.Fabric.Shared.Features.Licensing.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Licensing;

/// <summary>
/// Where the link bit CLI prints leads: the signed-in account gets a free license for the project the link names, which
/// bit picks up by itself when it waits with the link's one-time code, or saves with <c>bit license &lt;license&gt;</c>.
/// </summary>
public partial class LicensePage
{
    [AutoInject] private ILicenseController licenseController = default!;

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

    private string Command => $"bit license {issued?.License}";

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
        isLoading = redirectToSignIn;
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

    private async Task Issue()
    {
        if (isIssuing) return;

        isIssuing = true;
        error = null;

        try
        {
            issued = await licenseController.Issue(new()
            {
                Project = Guid.Parse(Id!),
                Name = ProjectName,
                Commit = Commit,
                Code = Code
            }, CurrentCancellationToken);
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
