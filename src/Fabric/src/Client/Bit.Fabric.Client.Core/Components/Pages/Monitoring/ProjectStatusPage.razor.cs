using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's status statusPage as its team runs it (bitfabric-internal/specs/monitoring.md, Status page): its address
/// and languages, the parts of the app as its users know them, the team's own host name through Cloudflare for SaaS, the
/// embed and the badge, and the composer that posts an incident, an update or an announcement in every language the
/// statusPage speaks, with AI drafts of the missing ones to review.
/// </summary>
public partial class ProjectStatusPage
{
    [Parameter, EditorRequired] public Guid ProjectId { get; set; }

    [AutoInject] private IStatusPagesController statusPagesController = default!;
    [AutoInject] private IProjectIncidentsController incidentsController = default!;
    [AutoInject] private Clipboard clipboard = default!;

    private StatusPageSettingsDto? statusPage;
    private ProjectIncidentDto[] openIncidents = [];
    private bool isSaving;
    private bool isConnecting;
    private bool isPosting;
    private bool isDrafting;
    private string? hostname;
    private string? copied;

    // The composer.
    private Guid? updatingIncidentId;
    private ProjectIncidentKind postKind = ProjectIncidentKind.Incident;
    private ProjectHealth postImpact = ProjectHealth.Degraded;
    private ProjectIncidentPhase postPhase = ProjectIncidentPhase.Investigating;
    private IEnumerable<Guid> postComponents = [];
    private Dictionary<string, string> postTitle = [];
    private Dictionary<string, string> postText = [];
    private bool notifiesFollowers = true;
    private string? editingLanguage;

    private static readonly (string Name, string DisplayName)[] cultures = [.. CultureInfoManager.SupportedCultures.Select(supported => (supported.Culture.Name, supported.DisplayName))];

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        statusPage = await statusPagesController.GetStatusPage(ProjectId, CurrentCancellationToken);
        hostname = statusPage.CustomHostname;
        editingLanguage = statusPage.Languages.FirstOrDefault();
        await LoadOpenIncidents();
    }

    private async Task LoadOpenIncidents()
        => openIncidents = [.. (await incidentsController.GetIncidents(ProjectId, CurrentCancellationToken)).Where(incident => incident.EndedAt is null)];

    private string MainLanguage => statusPage?.Languages.FirstOrDefault() ?? CultureInfo.CurrentUICulture.Name;

    private string LanguageName(string culture) => cultures.FirstOrDefault(c => c.Name == culture).DisplayName ?? culture;

    private void SetMainLanguage(string? culture)
    {
        if (culture is null || statusPage is null) return;
        statusPage.Languages = [culture, .. statusPage.Languages.Where(language => language != culture)];
    }

    private void SetOtherLanguages(IEnumerable<string?> languages)
    {
        if (statusPage is null) return;
        statusPage.Languages = [MainLanguage, .. languages.OfType<string>().Where(language => language != MainLanguage)];
    }

    private void AddComponent()
    {
        statusPage!.Components = [.. statusPage.Components, new() { Name = new() { [MainLanguage] = "" } }];
    }

    private void RemoveComponent(StatusComponentDto component)
    {
        statusPage!.Components = [.. statusPage.Components.Where(c => c != component)];
    }

    private static string NameIn(StatusComponentDto component, string culture) => component.Name.GetValueOrDefault(culture) ?? "";

    private static void SetName(StatusComponentDto component, string culture, string? name) => component.Name[culture] = name ?? "";

    private async Task Save()
    {
        if (isSaving || statusPage is null) return;
        isSaving = true;

        try
        {
            statusPage = await statusPagesController.SaveStatusPage(statusPage, CurrentCancellationToken);
            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringSaved)]);
        }
        finally
        {
            isSaving = false;
        }
    }

    private async Task Connect()
    {
        if (isConnecting || string.IsNullOrWhiteSpace(hostname)) return;
        isConnecting = true;

        try
        {
            statusPage = await statusPagesController.ConnectCustomHostname(new() { ProjectId = ProjectId, Hostname = hostname }, CurrentCancellationToken);
        }
        finally
        {
            isConnecting = false;
        }
    }

    private async Task Refresh() => statusPage = await statusPagesController.RefreshCustomHostname(ProjectId, CurrentCancellationToken);

    private async Task Disconnect()
    {
        statusPage = await statusPagesController.DisconnectCustomHostname(ProjectId, CurrentCancellationToken);
        hostname = null;
    }

    private void StartUpdate(Guid? incidentId)
    {
        updatingIncidentId = incidentId;
        postTitle = [];
        postText = [];
        postPhase = incidentId is null ? ProjectIncidentPhase.Investigating : ProjectIncidentPhase.Identified;
    }

    /// <summary>AI drafts of the post in the languages the team hasn't written it in, to review before posting.</summary>
    private async Task DraftMissing()
    {
        if (isDrafting || statusPage is null) return;
        isDrafting = true;

        try
        {
            string[] missing = [.. statusPage.Languages.Where(language => string.IsNullOrWhiteSpace(postText.GetValueOrDefault(language)))];

            if (postText.GetValueOrDefault(MainLanguage) is { Length: > 0 } text && missing.Length > 0)
            {
                foreach (var (culture, draft) in await statusPagesController.DraftTranslations(new() { FromCulture = MainLanguage, Text = text, ToCultures = missing }, CurrentCancellationToken))
                {
                    postText[culture] = draft;
                }
            }

            if (updatingIncidentId is null && postTitle.GetValueOrDefault(MainLanguage) is { Length: > 0 } title)
            {
                string[] missingTitles = [.. statusPage.Languages.Where(language => string.IsNullOrWhiteSpace(postTitle.GetValueOrDefault(language)))];

                if (missingTitles.Length > 0)
                {
                    foreach (var (culture, draft) in await statusPagesController.DraftTranslations(new() { FromCulture = MainLanguage, Text = title, ToCultures = missingTitles }, CurrentCancellationToken))
                    {
                        postTitle[culture] = draft;
                    }
                }
            }
        }
        finally
        {
            isDrafting = false;
        }
    }

    private bool CanPost => postText.GetValueOrDefault(MainLanguage) is { Length: > 0 }
                            && (updatingIncidentId is not null || postTitle.GetValueOrDefault(MainLanguage) is { Length: > 0 });

    private async Task Post()
    {
        if (isPosting || CanPost is false) return;
        isPosting = true;

        try
        {
            var incident = openIncidents.FirstOrDefault(incident => incident.Id == updatingIncidentId);

            await statusPagesController.Post(new()
            {
                ProjectId = ProjectId,
                IncidentId = updatingIncidentId,
                Kind = postKind,
                Impact = postImpact,
                Phase = postKind is ProjectIncidentKind.Announcement ? ProjectIncidentPhase.Resolved : postPhase,
                ComponentIds = [.. postComponents],
                Title = postTitle,
                Text = postText,
                NotifiesFollowers = notifiesFollowers,
                Version = incident?.Version ?? 0
            }, CurrentCancellationToken);

            SnackBarService.Success(Localizer[nameof(AppStrings.MonitoringPosted)]);
            StartUpdate(null);
            await LoadOpenIncidents();
        }
        finally
        {
            isPosting = false;
        }
    }

    private string PhaseText(ProjectIncidentPhase phase) => phase switch
    {
        ProjectIncidentPhase.Identified => Localizer[nameof(AppStrings.MonitoringIdentified)],
        ProjectIncidentPhase.Monitoring => Localizer[nameof(AppStrings.MonitoringMonitoringAFix)],
        ProjectIncidentPhase.Resolved => Localizer[nameof(AppStrings.MonitoringResolved)],
        _ => Localizer[nameof(AppStrings.MonitoringInvestigating)]
    };

    private string Api(string kind) => new Uri(AbsoluteServerAddress, $"api/v1/PublicStatus/{kind}/{statusPage?.Slug}").ToString();

    private string EmbedSnippet => $"<script src=\"{Api("Embed")}\" async></script>";

    private string BadgeSnippet => $"[![Status]({Api("Badge")})]({statusPage?.PublicUrl})";

    private async Task Copy(string text)
    {
        await clipboard.WriteText(text);
        copied = text;
    }
}
