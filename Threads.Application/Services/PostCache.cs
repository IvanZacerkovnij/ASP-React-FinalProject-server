using Microsoft.Extensions.Caching.Hybrid;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Posts.Models;

namespace Threads.Application.Services;

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
        PostReadModel post)
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
        catch
        {
            return false;
        }
    }
}
