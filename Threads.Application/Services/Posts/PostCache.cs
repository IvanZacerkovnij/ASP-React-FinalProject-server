using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Posts.Models;

namespace Threads.Application.Services.Posts;

internal static class PostCache
{
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(2);

    public static HybridCacheEntryOptions EntryOptions { get; } = new()
    {
        Expiration = TimeSpan.FromMinutes(10),
        Flags = HybridCacheEntryFlags.DisableLocalCache
    };

    public static string GetKey(Guid postId)
    {
        return $"posts:core:v1:{postId:N}";
    }

    public static async Task<bool> TrySetAsync(
        HybridCache cache,
        string cacheKey,
        PostContentReadModel post,
        ILogger logger)
    {
        using var timeout = new CancellationTokenSource(OperationTimeout);

        try
        {
            await cache.SetAsync(
                cacheKey,
                post,
                EntryOptions,
                cancellationToken: timeout.Token);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Failed to update post cache entry {CacheKey}", cacheKey);
            return false;
        }
    }
}
