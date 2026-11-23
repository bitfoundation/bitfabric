using System.Diagnostics.CodeAnalysis;
using Bit.BevTools;
using Bit.Belemetry;
using Bit.Fabric.Client.Core;
using Microsoft.AspNetCore.Components.WebAssembly.Services;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

public static partial class IClientCoreServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddClientCoreProjectServices(IConfiguration configuration)
        {
            // Services being registered here can get injected in client side (WebAssembly, Android, iOS, Windows and macOS) + server side (during pre-rendering and Blazor Server)
            services.AddSharedProjectServices(configuration);

            services.AddTransient<IPrerenderStateService, NoOpPrerenderStateService>();

            services.AddScoped<ThemeService>();
            services.AddScoped<CultureService>();
            services.AddScoped<TimeZoneService>();
            services.AddScoped<LazyAssemblyLoader>();
            var bevToolsAtStartup = AppEnvironment.IsDevelopment() || AppPlatform.IsBlazorHybrid;
            services.AddBitBevToolsLoaderServices(options =>
            {
                options.RegisterAtStartup = bevToolsAtStartup;
                options.ShowBadge = false;
            });
            services.AddBitBelemetryServices(options =>
            {
                options.Consent.Default = ConsentService.TelemetryWithoutAnalytics;
                options.Diagnostics.Enabled = bevToolsAtStartup;
            });
            if (bevToolsAtStartup)
            {
                services.AddBevToolsBelemetry();
            }
            services.AddScoped<SignInModalService>();
            services.AddScoped<AccountSwitchService>();
            services.AddScoped<ImageEditorService>();
            services.AddScoped<IAuthTokenProvider, ClientSideAuthTokenProvider>();
            services.AddScoped<IExternalNavigationService, DefaultExternalNavigationService>();
            services.AddScoped<FileSaveService>();
            services.AddScoped<ConsentService>();
            services.AddScoped<HomeOnboardingService>();

            // The home page's onboarding loads Bit.Bonboarding lazily with the films' library, so nothing registers its services:
            // its components create them themselves, on every platform alike (Bonboarding's guide, "Lazy loading on WebAssembly").
            AppContext.SetSwitch("Bit.Bonboarding.LazyLoad", true);

            if (Uri.TryCreate(configuration.GetServerAddress(), UriKind.Absolute, out var serverAddress))
            {
                services.AddScoped<AbsoluteServerAddressProvider>(sp => new() { GetAddress = () => serverAddress });
            }
            else
            {
                services.AddScoped<AbsoluteServerAddressProvider>(sp => new()
                {
                    /* Read AbsoluteServerAddressProvider's comments for more info. */
                    GetAddress = () => sp.GetRequiredService<HttpClient>().BaseAddress!
                });
            }

            // The following services must be unique to each app session.
            // Defining them as singletons would result in them being shared across all users in Blazor Server and during pre-rendering.
            // To address this, we use the AddSessioned extension method.
            // AddSessioned applies AddSingleton in BlazorHybrid and AddScoped in Blazor WebAssembly and Blazor Server, ensuring correct service lifetimes for each environment.
            services.AddSessioned<PubSubService>();
            services.AddSessioned<SnackBarService>();
            services.AddSessioned<ElevatedAccessService>();
            services.AddSessioned<AccountPinCipher>();
            services.AddSessioned<NotificationPreferenceService>();
            services.AddSessioned<ILocalHttpServer, NoOpLocalHttpServer>();
            services.AddSessioned<ITelemetryContext, AppTelemetryContext>();
            services.AddSessioned<AuthenticationStateProvider>(sp =>
            {
                var authenticationStateProvider = ActivatorUtilities.CreateInstance<AuthManager>(sp);
                authenticationStateProvider.OnInit();
                return authenticationStateProvider;
            });
            services.AddSessioned(sp => (AuthManager)sp.GetRequiredService<AuthenticationStateProvider>());

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<ClientCoreSettings>>().Value);

            services.AddSingleton<IValidateOptions<ClientCoreSettings>, ClientCoreSettingsValidator>();
            services.AddOptions<ClientCoreSettings>()
                .Bind(configuration)
                .ValidateOnStart();

            services.AddBitButilServices();
            services.AddBitBrouterServices();

            services.AddBitBlazorUIExtrasServices(trySingleton: AppPlatform.IsBlazorHybrid);
            AppThemePresets.RegisterSurfaces();

            // Read HttpMessageHandlersChainFactory comments for more info.
            services.AddScoped<HttpMessageHandlersChainFactory>(serviceProvider => transportHandler =>
            {
                transportHandler ??= AppPlatform.IsBrowser ? new HttpClientHandler() : new SocketsHttpHandler() // SocketsHttpHandler doesn't work in BlazorWebAssembly.
                {
                    EnableMultipleHttp2Connections = true,
                    EnableMultipleHttp3Connections = true,
                    PooledConnectionLifetime = TimeSpan.FromMinutes(15),
                    AutomaticDecompression = System.Net.DecompressionMethods.All,
                    SslOptions = new()
                    {
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13
                    }
                };

                var constructedHttpMessageHandler = ActivatorUtilities.CreateInstance<LoggingDelegatingHandler>(serviceProvider,
                            [ActivatorUtilities.CreateInstance<CacheDelegatingHandler>(serviceProvider,
                            [ActivatorUtilities.CreateInstance<RequestHeadersDelegatingHandler>(serviceProvider,
                            [ActivatorUtilities.CreateInstance<AuthDelegatingHandler>(serviceProvider,
                            [ActivatorUtilities.CreateInstance<RetryDelegatingHandler>(serviceProvider,
                            [ActivatorUtilities.CreateInstance<ExceptionDelegatingHandler>(serviceProvider, [transportHandler!])])])])])]);
                return constructedHttpMessageHandler;
            });


            services.AddTypedHttpClients(); // See Bit.Fabric.Shared/Controllers/Readme.md

            services.AddScoped<IRetryPolicy, SignalRInfiniteRetryPolicy>();
            services.AddSessioned<AppHubConnection>();

            return services;
        }

        // Apart, so the startup code names no Bit.BevTools type: the browser lazy loads it outside Development
        [MethodImpl(MethodImplOptions.NoInlining)]
        private void AddBevToolsBelemetry()
        {
            services.AddBitBevToolsBelemetry();
        }


        /// <summary>
        /// Blazor Server creates one scope for each connected user's client app.
        /// Blazor WebAssembly has only one scope for the client app.
        /// MAUI + Blazor Hybrid would create two scopes for the client app: one for the native part and one for the Blazor's WebView.
        /// So, in Blazor Hybrid, we register sessioned services as singletons to share them between the two scopes.
        /// And in Blazor Server we **MUST** register sessioned services as scoped to avoid sharing them between different users.
        /// And in Blazor WebAssembly it doesn't matter if we register sessioned services as scoped or singleton because there's only one scope per client app.
        /// </summary>
        internal IServiceCollection AddSessioned<TService, TImplementation>()
            where TImplementation : class, TService
            where TService : class
        {
            if (AppPlatform.IsBlazorHybrid)
            {
                return services.AddSingleton<TService, TImplementation>();
            }
            else
            {
                return services.AddScoped<TService, TImplementation>();
            }
        }

        /// <summary>
        /// <inheritdoc cref="AddSessioned{TService, TImplementation}(IServiceCollection)"/>
        /// </summary>
        internal IServiceCollection AddSessioned<TService>(Func<IServiceProvider, TService> implementationFactory)
            where TService : class
        {
            if (AppPlatform.IsBlazorHybrid)
            {
                services.Add(ServiceDescriptor.Singleton(implementationFactory));
            }
            else
            {
                services.Add(ServiceDescriptor.Scoped(implementationFactory));
            }

            return services;
        }

        /// <summary>
        /// <inheritdoc cref="AddSessioned{TService, TImplementation}(IServiceCollection)"/>
        /// </summary>
        internal void AddSessioned<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TService>()
            where TService : class
        {
            if (AppPlatform.IsBlazorHybrid)
            {
                services.AddSingleton<TService, TService>();
            }
            else
            {
                services.AddScoped<TService, TService>();
            }
        }
    }
}
