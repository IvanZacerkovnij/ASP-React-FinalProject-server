using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.Services.Common;

namespace Threads.Application.UnitTests.Services.Common;

public class CacheInvalidationTests
{
    [Fact]
    public async Task TryRemoveAsync_RemovesEachDistinctKeyOnce()
    {
        var cache = Substitute.For<HybridCache>();
        var logger = Substitute.For<ILogger>();

        var result = await CacheInvalidation.TryRemoveAsync(cache, logger, "first", "first", "second");

        Assert.True(result);
        await cache.Received(1).RemoveAsync("first", Arg.Any<CancellationToken>());
        await cache.Received(1).RemoveAsync("second", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TryRemoveAsync_WhenCacheThrows_ReturnsFalse()
    {
        var cache = Substitute.For<HybridCache>();
        var logger = Substitute.For<ILogger>();
        cache
            .RemoveAsync("key", Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new InvalidOperationException("cache failed")));

        var result = await CacheInvalidation.TryRemoveAsync(cache, logger, "key");

        Assert.False(result);
    }
}
