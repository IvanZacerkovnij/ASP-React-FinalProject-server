using Microsoft.Extensions.Caching.Hybrid;

namespace Threads.Infrastracture.Services;

public static class HybridCacheConfigurator
{
    public static void Configure(HybridCacheOptions options)
    {
        options.DefaultEntryOptions = new HybridCacheEntryOptions()
        {
            Expiration = TimeSpan.FromMinutes(5),
            LocalCacheExpiration = TimeSpan.FromMinutes(1)
        };
    }
}