using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Threads.Application.Services.Common;

internal static class CacheInvalidation
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<bool> TryRemoveAsync(
        HybridCache cache,
        ILogger logger,
        params string[] cacheKeys)
    {
        using var timeout = new CancellationTokenSource(Timeout);

        try
        {
            var removals = cacheKeys
                .Distinct(StringComparer.Ordinal)
                .Select(cacheKey => cache.RemoveAsync(cacheKey, timeout.Token).AsTask());

            await Task.WhenAll(removals);
            return true;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Failed to invalidate {CacheKeyCount} cache entries",
                cacheKeys.Length);
            return false;
        }
    }
}
