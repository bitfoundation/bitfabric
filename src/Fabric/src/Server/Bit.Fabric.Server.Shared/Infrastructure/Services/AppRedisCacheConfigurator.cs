using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using ZiggyCreatures.Caching.Fusion.Backplane.StackExchangeRedis;
using ZiggyCreatures.Caching.Fusion.Locking.Distributed.Redis;

namespace Bit.Fabric.Server.Shared.Infrastructure.Services;

/// <summary>
/// Configures FusionCache L2, Distributed Locking and Backplanes to share a single Redis connection enriched with logging and retry policies.
/// </summary>
public class AppRedisCacheConfigurator(
    [FromKeyedServices("redis-cache")] IConnectionMultiplexer redisCacheConnectionMultiplexer
    , IOptionsMonitor<ConfigurationOptions> redisConfigurationOptions
    ) :
        IPostConfigureOptions<RedisCacheOptions>,
        IPostConfigureOptions<RedisDistributedLockerOptions>,
        IPostConfigureOptions<RedisBackplaneOptions>
        , IPostConfigureOptions<RedisOptions>
{
    public void PostConfigure(string? name, RedisCacheOptions options)
    {
        options.ConnectionMultiplexerFactory = async () => redisCacheConnectionMultiplexer;
    }

    public void PostConfigure(string? name, RedisBackplaneOptions options)
    {
        // Redis backplane for FusionCache, used for synchronizing cache invalidation across multiple server instances in a distributed environment.
        options.ConnectionMultiplexerFactory = async () => redisCacheConnectionMultiplexer;
    }

    public void PostConfigure(string? name, RedisDistributedLockerOptions options)
    {
        // This refers to FusionCache's internal L2 distributed locking mechanism, specifically designed to prevent Distributed Cache Stampede.
        // Note: This is distinct from the general-purpose Distributed Locker (Medallion.Threading.Redis) we use for application-level business logic.
        options.ConnectionMultiplexerFactory = async () => redisCacheConnectionMultiplexer;
    }

    public void PostConfigure(string? name, RedisOptions options)
    {
        // Redis backplane for SignalR, used for scaling out SignalR across multiple server instances.
        options.ConnectionFactory = async writer => await ConnectionMultiplexer.ConnectAsync(redisConfigurationOptions.Get("redis-cache").Clone(), writer);
    }
}

public static class AppRedisConfiguratorExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection ConfigureRedisOptions()
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RedisCacheOptions>, AppRedisCacheConfigurator>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RedisBackplaneOptions>, AppRedisCacheConfigurator>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RedisDistributedLockerOptions>, AppRedisCacheConfigurator>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<RedisOptions>, AppRedisCacheConfigurator>());

            return services;
        }
    }
}
