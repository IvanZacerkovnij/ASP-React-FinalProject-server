using Microsoft.Extensions.Caching.Hybrid;

namespace Threads.Application.Services;

internal static class UserProfileCache
{
    public static HybridCacheEntryOptions ProfileEntryOptions { get; } = new()
    {
        Expiration = TimeSpan.FromMinutes(5),
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    public static HybridCacheEntryOptions UsernameEntryOptions { get; } = new()
    {
        Expiration = TimeSpan.FromMinutes(30),
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    public static string GetProfileKey(Guid userId)
    {
        return $"users:profile:v1:{userId:N}";
    }

    public static string GetUsernameKey(string normalizedUsername)
    {
        return $"users:username:v1:{normalizedUsername}";
    }

}
