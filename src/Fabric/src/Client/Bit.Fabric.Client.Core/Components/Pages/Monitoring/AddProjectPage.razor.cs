using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// Adding a project by its address (bitfabric-internal/specs/monitoring.md, Connecting a project): the app says where its
/// health report is, then the customer approves bit Fabric in their own app. The approval opens beside this page, in a
/// popup or a new tab on the web and in the system browser in the native apps, and its answer comes back the way external
/// sign-in's does, so this page never reloads (bitfabric-internal/specs/monitoring.md, The approval opens beside the page).
/// When the browser blocked the popup, the approval runs in this tab, and its answer comes back to this page's callback
/// address instead. Either way the page goes on to its last step (See ProjectConnected).
/// </summary>
public partial class AddProjectPage
{
    private string? address;
    private string? name;
    private bool isProbing;
    private bool isStarting;
    private bool isWaitingForApproval;
    private string? copied;
    private string? connectError;
    private AddressProbeResultDto? probe;
    private Action? unsubscribe;

    [AutoInject] private IMonitoringsController monitoringsController = default!;
    [AutoInject] private Clipboard clipboard = default!;
    [AutoInject] private ILocalHttpServer localHttpServer = default!;
    [AutoInject] private IExternalNavigationService externalNavigationService = default!;

    [Parameter, SupplyParameterFromQuery(Name = "state")] public string? State { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "code")] public string? Code { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "error")] public string? Error { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "iss")] public string? Iss { get; set; }

    /// <summary>The project just connected, whose first checks the page shows (See ProjectConnected); in the address, so a refresh keeps it.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "connected")] public Guid? Connected { get; set; }

    /// <summary>The app's address, from Settings' "Reconnect as someone else", looked for at once.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "address")] public string? Address { get; set; }

    /// <summary>The project the environment joins, from its page's "Add an environment"; empty for a new project.</summary>
    [Parameter, SupplyParameterFromQuery(Name = "project")] public Guid? ProjectId { get; set; }

    [AutoInject] private Bit.Fabric.Shared.Features.Projects.IProjectsController projectsController = default!;

    /// <summary>The project the environment joins, by its name; null for a new project.</summary>
    private Bit.Fabric.Shared.Features.Projects.Dtos.ProjectDto? joining;

    /// <summary>Production, unless the project has it: then Test, then Staging (bitfabric-internal/specs/monitoring.md).</summary>
    private string environment = "Production";

    private static readonly string[] environmentChoices = ["Production", "Test", "Staging"];

    private bool IsCallback => string.IsNullOrEmpty(State) is false;

    private static string BackIcon => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft ? BitIconName.Forward : BitIconName.Back;

    private const string UpgradePrompt = "Upgrade this project to the newest bit platform release with the template-diff skill, then run the build and the tests.";

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        address ??= Address;

        if (ProjectId is { } projectId && Connected is null && IsCallback is false)
        {
            joining = await projectsController.GetProject(projectId, null, CurrentCancellationToken);
            environment = environmentChoices.FirstOrDefault(choice => joining.Monitorings.Any(taken => string.Equals(taken.Environment, choice, StringComparison.OrdinalIgnoreCase)) is false) ?? "";
        }
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        if (IsCallback is false)
        {
            // Reconnecting from Settings: the app's address is known, so it is looked for right away.
            if (Connected is null && string.IsNullOrWhiteSpace(Address) is false)
            {
                await Find();
                StateHasChanged();
            }

            return;
        }

        // Once, in the browser: the code can be exchanged only once, so a prerender must not spend it.
        try
        {
            var project = await monitoringsController.CompleteConnect(new() { State = State, Code = Code, Error = Error, Iss = Iss, TimeZoneId = TimeZoneService.CurrentTimeZone.Id }, CurrentCancellationToken);
            ShowConnected(project);
        }
        catch (KnownException exp)
        {
            connectError = exp.Message;
            StateHasChanged();
        }
    }

    private async Task Find()
    {
        if (isProbing || string.IsNullOrWhiteSpace(address))
            return;

        isProbing = true;
        CancelApproval();

        try
        {
            probe = await monitoringsController.ProbeAddress(new() { Address = address }, CurrentCancellationToken);
            name = probe.AppTitle ?? Host(probe.Address);
        }
        finally
        {
            isProbing = false;
        }
    }

    private async Task Connect()
    {
        if (isStarting || probe is null)
            return;

        isStarting = true;
        connectError = null;

        try
        {
            // The answer comes back as external sign-in's does (See MonitoringClientController.Callback).
            unsubscribe?.Invoke();
            unsubscribe = PubSubService.Subscribe(ClientAppMessages.EXTERNAL_SIGN_IN_CALLBACK, async payload => await InvokeAsync(() => Complete(payload?.ToString())));

            var port = localHttpServer.EnsureStarted();

            var start = await monitoringsController.StartConnect(new()
            {
                Address = probe.Address,
                Name = name,
                ProjectId = joining?.Id,
                Environment = string.IsNullOrWhiteSpace(environment) ? null : environment.Trim(),
                LocalHttpPort = port is -1 ? null : port,
                LocalHttpNonce = localHttpServer.CreateExternalSignInNonce()
            }, CurrentCancellationToken);

            isWaitingForApproval = true;

            // The customer's own app, where they sign in and approve.
            await externalNavigationService.NavigateTo(start.AuthorizeUrl!);
        }
        finally
        {
            isStarting = false;
        }
    }

    /// <summary>The approval's answer, as the relative address of this page's callback with its query.</summary>
    private async Task Complete(string? url)
    {
        if (Uri.IsAppRelativeUrl(url) is false)
            return;

        var queryIndex = url!.IndexOf('?');
        if (queryIndex < 0 || url[..queryIndex].Equals(PageUrls.AddProjectCallback, StringComparison.OrdinalIgnoreCase) is false)
            return; // Another external sign-in's answer.

        unsubscribe?.Invoke();
        unsubscribe = null;

        var query = AppQueryStringCollection.Parse(url[queryIndex..]);
        string? Value(string key) => query.TryGetValue(key, out var value) && value?.ToString() is { Length: > 0 } text ? text : null;

        try
        {
            var project = await monitoringsController.CompleteConnect(new() { State = Value("state"), Code = Value("code"), Error = Value("error"), Iss = Value("iss"), TimeZoneId = TimeZoneService.CurrentTimeZone.Id }, CurrentCancellationToken);
            isWaitingForApproval = false;
            ShowConnected(project);
        }
        catch (KnownException exp)
        {
            connectError = exp.Message;
            isWaitingForApproval = false;
            StateHasChanged();
        }
    }

    /// <summary>The last step, in place of the approval's answer in the address, which can't be spent twice.</summary>
    private void ShowConnected(MonitoringDto project)
        => NavigationManager.NavigateTo($"{PageUrls.AddProject}?connected={project.Id}", replace: true);

    private void CancelApproval()
    {
        unsubscribe?.Invoke();
        unsubscribe = null;
        isWaitingForApproval = false;
    }

    private async Task Copy(string text)
    {
        await clipboard.WriteText(text);
        copied = text;
    }

    private static string Host(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Authority : url ?? "";

    /// <summary>A host name inside a translated sentence, kept left to right in Persian and Arabic (FSI ... PDI, as &lt;bdi&gt; does).</summary>
    private static string Isolate(string text) => $"⁨{text}⁩";

    protected override async ValueTask DisposeAsync(bool disposing)
    {
        unsubscribe?.Invoke();

        await base.DisposeAsync(disposing);
    }
}
