using Microsoft.Extensions.Caching.Hybrid;

namespace Threads.Application.Services;

internal static class CacheInvalidation
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public static async Task<bool> TryRemoveAsync(
        HybridCache cache,
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
        catch
        {
            return false;
        }
    }
}
