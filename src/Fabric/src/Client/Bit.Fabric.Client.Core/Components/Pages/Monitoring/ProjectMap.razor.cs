using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Monitoring.Dtos;
using Microsoft.AspNetCore.Components.WebAssembly.Services;

namespace Bit.Fabric.Client.Core.Components.Pages.Monitoring;

/// <summary>
/// A project's Application map tab (bitfabric-internal/specs/monitoring.md, Application map): the app in the middle, what
/// it talks to around it by its report's categories, each part coloured by how it's doing, and the picked part's last 24
/// hours beside it. The drawing is bit Blow's, in the lazily loaded Bit.Fabric.Client.Diagrams; this page names none of
/// its types until it is loaded, and falls back to a plain list when it can't be.
/// </summary>
public partial class ProjectMap
{
    private const string MapTypeName = "Bit.Fabric.Client.Diagrams.Components.ProjectMapView, Bit.Fabric.Client.Diagrams";

    /// <summary>What the drawing needs, downloaded the first time the tab opens.</summary>
    private static readonly string[] MapAssemblies = ["Bit.Fabric.Client.Diagrams.wasm", "Bit.Blow.wasm"];

    [AutoInject] private LazyAssemblyLoader lazyAssemblyLoader = default!;
    [AutoInject] private IMonitoringsController monitoringsController = default!;

    [Parameter, EditorRequired] public MonitoringDto Project { get; set; } = default!;

    /// <summary>Opens another tab of the project, such as its history, on one part when the link names it.</summary>
    [Parameter] public EventCallback<ProjectTabLink> OnOpenTab { get; set; }

    [CascadingParameter] public BitDir? CurrentDir { get; set; }

    private ProjectMapDto? map;
    private Type? mapType;
    private bool isMapWanted;
    private bool isMapFailed;
    private bool isNarrow;
    private bool isSheetOpen;
    private string? selectedKey;

    private ProjectMapPartDto? selected => map?.Parts.FirstOrDefault(part => part.Key == selectedKey);

    private ProjectMapPartTone AppTone => ProjectMapTones.ToneOf(Project.Health);

    /// <summary>The report's categories in the order the Overview shows them, and in each the worst part first.</summary>
    private IEnumerable<IGrouping<string, ProjectMapPartDto>> Groups => (map?.Parts ?? [])
        .OrderBy(part => ProjectMapTones.RankOf(ProjectMapTones.ToneOf(part)))
        .ThenBy(ProjectMapTones.TitleOf, StringComparer.CurrentCultureIgnoreCase)
        .GroupBy(part => ProjectChecks.GroupOf(part.Category))
        .OrderBy(group => ProjectChecks.OrderOf(group.Key))
        .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase);

    protected override async Task OnInitAsync()
    {
        await base.OnInitAsync();

        map = await monitoringsController.GetMap(Project.Id, CurrentCancellationToken);

        // What needs attention is picked at once; a map where everything works starts with the summary.
        selectedKey = map.Parts.Where(part => ProjectMapTones.ToneOf(part) is ProjectMapPartTone.Failing or ProjectMapPartTone.Degraded)
                               .OrderBy(part => ProjectMapTones.RankOf(ProjectMapTones.ToneOf(part)))
                               .FirstOrDefault()?.Key;
    }

    protected override async Task OnAfterFirstRenderAsync()
    {
        await base.OnAfterFirstRenderAsync();

        await LoadMap();
    }

    /// <summary>
    /// Brings in the drawing. A download that fails leaves the plain list in its place: the team has lost only a picture.
    /// It names none of the drawing's types, which the browser would resolve before they are loaded.
    /// </summary>
    private async Task LoadMap()
    {
        isMapWanted = true;
        StateHasChanged();

        try
        {
            // An AOT build has every assembly in it already
            if (AppPlatform.IsAot is false)
            {
                await lazyAssemblyLoader.LoadAssembliesAsync(MapAssemblies);
            }

            mapType = Type.GetType(MapTypeName, throwOnError: true);
        }
        catch (Exception exp) when (exp is not OperationCanceledException)
        {
            isMapFailed = true;
            ExceptionHandler.Handle(exp, ExceptionDisplayKind.None);
        }

        await InvokeAsync(StateHasChanged);
    }

    /// <summary>
    /// Picks a part, or lets go of the one picked. On a phone the sheet opens only for a part the team picked itself, so
    /// the part picked for them on arrival doesn't cover the map.
    /// </summary>
    private void Select(string? key)
    {
        selectedKey = isNarrow is false && key == selectedKey ? null : key;
        isSheetOpen = isNarrow && selectedKey is not null;
    }

    private void SheetChanged(bool isOpen)
    {
        isSheetOpen = isOpen;
    }

    /// <summary>The drawing's parameters. Called only once its library is loaded, as it names its types.</summary>
    private Dictionary<string, object?> MapParameters() => new()
    {
        ["Picture"] = Picture(),
        ["SelectedKey"] = selectedKey,
        ["OnSelect"] = EventCallback.Factory.Create<string>(this, Select),
        ["Dir"] = CurrentDir
    };

    private Bit.Fabric.Client.Diagrams.Components.ProjectMapPicture Picture()
    {
        static Bit.Fabric.Client.Diagrams.Components.ProjectMapTone Look(ProjectMapPartTone tone)
            => (Bit.Fabric.Client.Diagrams.Components.ProjectMapTone)(int)tone;

        var host = Uri.TryCreate(Project.Address, UriKind.Absolute, out var address) ? address.Host : Project.Address;

        return new(Project.Name ?? host ?? "",
            host,
            map!.HomePageMs is { } homePageMs ? Localizer[nameof(AppStrings.MonitoringAppMapHomePage), Ms(homePageMs)] : null,
            Look(AppTone),
            [.. Groups.Select(group => new Bit.Fabric.Client.Diagrams.Components.ProjectMapGroup(group.Key,
                Localizer[ProjectChecks.TitleOf(group.Key)],
                ProjectChecks.IconOf(group.Key),
                Look(ProjectMapTones.WorstOf(group)),
                [.. group.Select(part => new Bit.Fabric.Client.Diagrams.Components.ProjectMapPart(part.Key ?? "",
                    ProjectMapTones.TitleOf(part),
                    part.IconName,
                    part.IsNotConfigured ? null : Ms(part.TypicalMs ?? part.LastMs),
                    Look(ProjectMapTones.ToneOf(part)),
                    part.OngoingIncidentId is null ? null : Localizer[nameof(AppStrings.MonitoringAppMapIncident)],
                    ProjectMapTones.BehindOf(part)))]))],
            Localizer[nameof(AppStrings.MonitoringAppMapAria), Project.Name ?? host ?? ""]);
    }

    private string? Ms(int? ms) => ms switch
    {
        null => null,
        < 1 => Localizer[nameof(AppStrings.MonitoringLessThanOneMs)],
        < 1000 => Localizer[nameof(AppStrings.AppHomeMs), ms],
        _ => Localizer[nameof(AppStrings.AppHomeS), (ms.Value / 1000.0).ToString("N1", CultureInfo.CurrentUICulture)]
    };
}
