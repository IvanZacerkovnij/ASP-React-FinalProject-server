using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public static class RedisConfigurator
{
    public static void Configure(RedisCacheOptions options, IConfiguration configuration)
    {
        var connectionString = configuration["Redis:ConnectionString"] ??
                               throw new InfrastructureConfigurationException("Redis:ConnectionString");

        options.Configuration = connectionString;
        options.InstanceName = "threads:";
    }
}
