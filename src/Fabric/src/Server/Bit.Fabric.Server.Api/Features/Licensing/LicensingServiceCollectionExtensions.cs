namespace Bit.Fabric.Server.Api.Features.Licensing;

public static class LicensingServiceCollectionExtensions
{
    public static IServiceCollection AddLicensing(this IServiceCollection services)
    {
        services.AddSingleton<LicenseSigner>();

        // Makes the key when the server starts, so a missing or broken one fails the start rather than the first license.
        services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<LicenseSigner>());

        services.AddScoped<IProjectLicenseStatus, ProjectLicenseStatus>();

        services.AddScoped<Projects.ProjectLicenses>();

        return services;
    }
}
