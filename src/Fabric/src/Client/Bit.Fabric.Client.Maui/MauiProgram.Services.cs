// [mirror] blazor hybrid DI registrations and logging setup - keep in sync with:
// - src/Client/Bit.Fabric.Client.Windows/Program.Services.cs

using Bit.Bmotion;
using Bit.Fabric.Client.Core.Infrastructure.Services.HttpMessageHandlers;
using Bit.Fabric.Client.Maui.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Bit.Fabric.Client.Maui;

public static partial class MauiProgram
{
    extension(MauiAppBuilder builder)
    {
        public void ConfigureServices()
        {
            // Services being registered here can get injected in Maui (Android, iOS, macOS, Windows)
            var services = builder.Services;
            var configuration = builder.Configuration;
            services.AddClientCoreProjectServices(builder.Configuration);
            // bit Bmotion needs this everywhere but in WebAssembly, where the home page's pictures create its services once it's loaded
            services.AddBitBmotionServices();

            services.AddTransient<MainPage>();

            services.AddScoped<IWebAuthnService, MauiWebAuthnService>();
            services.AddScoped<ClientExceptionHandlerBase, MauiExceptionHandler>();
            services.AddScoped<SharedExceptionHandler>(sp => sp.GetRequiredService<ClientExceptionHandlerBase>());

            services.AddScoped<IAppUpdateService, MauiAppUpdateService>();
            services.AddScoped<IPermissionService, MauiPermissionService>();
            services.AddScoped<IBitDeviceCoordinator, MauiDeviceCoordinator>();
            services.AddScoped<IExternalNavigationService, MauiExternalNavigationService>();
            if (AppPlatform.IsWindows is false)
            {
                services.AddScoped<FileSaveService, MauiFileSaveService>();
            }

            services.AddScoped<HttpClient>(sp =>
            {
                var handlerFactory = sp.GetRequiredService<HttpMessageHandlersChainFactory>();
                var httpClient = new HttpClient(handlerFactory.Invoke())
                {
                    BaseAddress = new Uri(configuration.GetServerAddress(), UriKind.Absolute)
                };
                var origin = sp.GetRequiredService<ClientMauiSettings>().WebAppUrl ?? httpClient.BaseAddress;
                httpClient.DefaultRequestHeaders.Add("X-Origin", origin.ToString());
                return httpClient;
            });

            services.AddSingleton<IStorageService, MauiStorageService>();
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ClientMauiSettings>>().Value);
            services.AddSingleton(ITelemetryContext.Current!);
            services.AddSingleton<ILocalHttpServer, MauiLocalHttpServer>();

            services.AddMauiBlazorWebView();
            services.AddBlazorWebViewDeveloperTools();

            builder.Logging.ConfigureLoggers(configuration);

            builder.Logging.AddEventSourceLogger();

            if (AppPlatform.IsWindows)
            {
                builder.Logging.AddEventLog(options => configuration.Bind("Logging:EventLog", options));
            }

            services.AddSingleton<IValidateOptions<ClientMauiSettings>, ClientMauiSettingsValidator>();
            services.AddOptions<ClientMauiSettings>()
                .Bind(configuration)
                .ValidateOnStart();

#if Android
            services.AddClientMauiProjectAndroidServices(builder.Configuration);
#elif iOS
        services.AddClientMauiProjectIosServices(builder.Configuration);
#elif Mac
            services.AddClientMauiProjectMacCatalystServices(builder.Configuration);
#elif Windows
            services.AddClientMauiProjectWindowsServices(builder.Configuration);
#endif
        }
    }
}
