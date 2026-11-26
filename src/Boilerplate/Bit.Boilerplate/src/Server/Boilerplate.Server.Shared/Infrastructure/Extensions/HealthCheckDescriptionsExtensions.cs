using Boilerplate.Server.Shared.Infrastructure.HealthChecks;

namespace Microsoft.Extensions.DependencyInjection;

public static class HealthCheckDescriptionsExtensions
{
    extension(IHealthChecksBuilder builder)
    {
        /// <summary>
        /// The title, icon (a <c>BitIconName</c>) and about /healthz reports for the check registered as
        /// <paramref name="name"/> (See <see cref="HealthCheckDescriptions"/>).
        /// </summary>
        public IHealthChecksBuilder Describe(string name, string title, string? iconName = null, string? about = null)
        {
            builder.Services.Configure<HealthCheckDescriptions>(descriptions => descriptions[name] = new(title, iconName, about));

            return builder;
        }

        /// <summary>
        /// The outside service /healthz reports the check registered as <paramref name="name"/> talks to (See
        /// <see cref="HealthCheckDependencies"/>): a short name, its host and, for an AI check, its model. Names only,
        /// never a key or a connection string; give <paramref name="host"/> through <see cref="HealthCheckDependency.HostOf(string?)"/>
        /// when it comes from a setting. Nothing is registered when <paramref name="provider"/> is empty.
        /// </summary>
        public IHealthChecksBuilder TalksTo(string name, string? provider, string? host = null, string? model = null)
        {
            if (string.IsNullOrWhiteSpace(provider) is false)
            {
                builder.Services.Configure<HealthCheckDependencies>(dependencies => dependencies[name] = new(provider, host, string.IsNullOrWhiteSpace(model) ? null : model));
            }

            return builder;
        }
    }
}
