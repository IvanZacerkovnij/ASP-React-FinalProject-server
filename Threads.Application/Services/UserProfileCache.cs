using Microsoft.Extensions.Caching.Hybrid;

namespace Threads.Application.Services;

internal static class UserProfileCache
{
    private static readonly TimeSpan InvalidationTimeout = TimeSpan.FromSeconds(2);

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

    public static async Task<bool> TryRemoveAsync(
        HybridCache cache,
        params string[] cacheKeys)
    {
        using var timeout = new CancellationTokenSource(InvalidationTimeout);

        try
        {
            var removals = cacheKeys
                .Distinct(StringComparer.Ordinal)
                .Select(cacheKey => cache.RemoveAsync(cacheKey, timeout.Token).AsTask());

            await Task.WhenAll(removals);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
