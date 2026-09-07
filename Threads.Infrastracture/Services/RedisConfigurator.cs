using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;

namespace Threads.Infrastracture.Services;

public static class RedisConfigurator
{
    public static void Configure(RedisCacheOptions options, IConfiguration configuration)
    {
        options.Configuration = configuration["Redis:ConnectionString"];
        options.InstanceName = "threads:";
    }
}