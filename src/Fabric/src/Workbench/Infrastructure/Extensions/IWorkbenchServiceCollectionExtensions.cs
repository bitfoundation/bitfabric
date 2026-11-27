using Bit.Bcrdt;
using Bit.Bonboarding;
using Bit.Fabric.Shared.Features.Attachments;
using Bit.Fabric.Shared.Features.Identity;
using Bit.Fabric.Shared.Features.Monitoring;
using Bit.Fabric.Shared.Features.Tenants;
using Bit.Fabric.Workbench.Infrastructure.Services.StandIns;
using Microsoft.Extensions.Localization;

namespace Bit.Fabric.Workbench.Infrastructure.Extensions;

/// <summary>
/// Registers the Workbench's own services: the scenario, sample data, page stores, screenshots, Edit text mode and the review.
/// </summary>
public static class IWorkbenchServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public void AddWorkbenchServices(IConfiguration configuration)
        {
            services.AddScoped<WorkbenchScenarioState>();
            // What a link of the app's own pages opens in a view (See PreviewPage.StayInThePreview).
            services.AddScoped<WorkbenchAppLinks>();
            services.AddSingleton<WorkbenchScreenshotService>();
            services.AddSingleton(_ => new WorkbenchScreenshotLimiter(configuration));
            services.AddSingleton<WorkbenchSnapshots>();
            services.AddScoped<AssistantService>();

            // The app's monitoring pages run on the sample world, through stand-ins of bit Fabric's controllers that win over
            // Client.Core's HTTP clients, and of what those pages ask the app's client for: this device's notifications, a team
            // to create, the approval page in the customer's own app.
            services.AddScoped<MonitoringSampleWorld>();
            services.AddScoped<IMonitoringsController, WorkbenchMonitoringsController>();
            services.AddScoped<Bit.Fabric.Shared.Features.Projects.IProjectsController, WorkbenchProjectsController>();
            services.AddScoped<IProjectIncidentsController, WorkbenchProjectIncidentsController>();
            services.AddScoped<IProjectScansController, WorkbenchProjectScansController>();
            services.AddScoped<IProjectSlaController, WorkbenchProjectSlaController>();
            services.AddScoped<IStatusPagesController, WorkbenchStatusPagesController>();
            services.AddScoped<IPublicStatusController, WorkbenchPublicStatusController>();
            services.AddScoped<ITenantController, WorkbenchTenantController>();

            // The app's growth page, on the sample worlds of Components/Pages/Growth/Data.
            services.AddScoped<Bit.Fabric.Shared.Features.Growth.IGrowthController, Components.Pages.Growth.Data.WorkbenchGrowthController>();
            services.AddScoped<WorkbenchPushNotificationService>();
            services.AddScoped<IPushNotificationService>(sp => sp.GetRequiredService<WorkbenchPushNotificationService>());
            services.AddScoped<IExternalNavigationService, WorkbenchApprovalNavigation>();

            // The app's settings page runs on the view's sample account, through stand-ins of the user and attachment
            // controllers and of the device's passkeys. The add project page's notifications step uses the same user stand-in.
            services.AddScoped<SettingsSampleAccount>();
            services.AddScoped<IUserController, WorkbenchUserController>();
            services.AddScoped<IAttachmentController, WorkbenchAttachmentController>();
            services.AddScoped<IWebAuthnService, WorkbenchWebAuthnService>();
            services.AddScoped<WorkbenchScenarioSignOut>();

            // The app's own pages, on stand-ins of its server: the settings page on the view's sample account, through the
            // user and attachment controllers and the device's passkeys, and the dashboard on a sample product catalog.
            services.AddScoped<SettingsSampleAccount>();
            services.AddScoped<IUserController, WorkbenchUserController>();
            services.AddScoped<IAttachmentController, WorkbenchAttachmentController>();
            services.AddScoped<IWebAuthnService, WorkbenchWebAuthnService>();
            services.AddScoped<WorkbenchScenarioSignOut>();

            // The assistant page's chat runs on the sample replies of AssistantService.
            services.AddBitBlazorUIAIServices(ai =>
            {
                ai.UseTransport(sp => sp.GetRequiredService<AssistantService>());
                ai.ConfigureGenerativeUi(components =>
                {
                    components.Register<AssistantSalesCard>("sales_trend");
                    components.Register<AssistantStockCard>("low_stock");
                    // The Theme page's sample of BitAiGenerativeUiView.
                    components.Register<AiWeatherCard>(AiSamples.WeatherCardName);
                });
            });

            // The guides that point at the shell's own controls, between panels and dropdown lists, so a dropdown they point at
            // still opens above the dimmed page.
            services.AddBitBonboardingServices(options => options.ZIndex = 1250);
            services.AddBitBonboardingLocalizer<WorkbenchTourLocalizer>();
            // A view's tours keep their progress in memory, so each view is the visit its switches name, and the Workbench's own
            // guides in the browser, as Bonboarding does by default (See WorkbenchOnboardingStore).
            services.AddScoped<BonboardingBrowserStore>();
            services.AddScoped<IBonboardingStore, WorkbenchOnboardingStore>();

            // The pages' stores, saved per view in IndexedDB, so a reload and a screenshot keep what was done on a page.
            services.AddBitBlexServices(options => options.DevToolsName = "bit Workbench");
            services.AddBitBlexStore<WorkbenchStore>();
            services.AddBitBlexStore<ChromeStore>();
            services.AddBitBlexStore<AssistantStore>();
            services.AddBitBlexStore<SettingsStore>();
            services.AddBitBlexStore<NewProjectAddressStore>();
            services.AddBitBlexStore<WorkbenchDeviceStore>();
            services.AddScoped<IBlexStorage, WorkbenchStateStorage>();
            services.AddBitBlexPersistence();
            // What the reader picks on a board goes to a file of the board's too, where the agent that built it reads it.
            services.AddSingleton<WorkbenchBoardFiles>();
            services.AddScoped<IBitAccentColorStore, ChromeAccentStore>();

            // Every localizer records the texts it shows, so Edit text mode can trace a text on the page to its resource entry.
            services.AddSingleton<WorkbenchTextRecorder>();
            services.AddSingleton<WorkbenchTextEdits>();
            services.AddTransient(typeof(IStringLocalizer<>), typeof(WorkbenchRecordingLocalizer<>));

            // Inspect mode: each bit BlazorUI component is recorded as it's created, so a click in a view finds it, and its tag in the code takes parameter changes.
            services.AddSingleton<WorkbenchComponentInstances>();
            services.AddScoped<IComponentActivator, WorkbenchComponentActivator>();
            services.AddSingleton<WorkbenchComponentEdits>();

            // The review: a Bcrdt document kept in WorkbenchFolders.Reviews, synced to every circuit through the relay and served to the agent over HTTP.
            services.AddSingleton<WorkbenchReviewAgent>();
            services.AddSingleton<WorkbenchProgressAgent>();
            services.AddSingleton<WorkbenchSecrets>();
            services.AddSingleton<WorkbenchReviewAttachments>();
            services.AddSingleton<WorkbenchReviewRelay>();
            services.AddScoped<WorkbenchReviewSession>();
            services.AddBitBcrdtServices(collaboration => collaboration.UseTransport<WorkbenchReviewRelay>().UseInMemoryStore());
            services.AddBitBcrdtDocumentHost(sp => new()
            {
                Store = WorkbenchFolders.Project is null ? new BcrdtInMemoryDocumentStore() : new BcrdtFileSystemDocumentStore(WorkbenchFolders.Reviews),
                ValidateUpdate = context =>
                {
                    // Wakes an agent waiting for comments or for an answer on its task; only the review document is accepted.
                    sp.GetRequiredService<WorkbenchReviewAgent>().Pulse();
                    sp.GetRequiredService<WorkbenchProgressAgent>().Pulse();
                    return ValueTask.FromResult(context.DocumentId == WorkbenchReviewDocument.Id);
                }
            });
        }
    }
}
