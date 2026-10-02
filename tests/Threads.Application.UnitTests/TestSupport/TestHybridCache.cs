using Microsoft.Extensions.Caching.Hybrid;

namespace Threads.Application.UnitTests.TestSupport;

internal sealed class TestHybridCache : HybridCache
{
    private readonly Dictionary<string, object?> _values = new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> RemovedKeys => _removedKeys;

    public IReadOnlyDictionary<string, object?> Values => _values;

    private readonly List<string> _removedKeys = [];

    public void Seed<T>(string key, T value)
    {
        _values[key] = value;
    }

    public override async ValueTask<T> GetOrCreateAsync<TState, T>(
        string key,
        TState state,
        Func<TState, CancellationToken, ValueTask<T>> factory,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        if (_values.TryGetValue(key, out var cachedValue))
        {
            return cachedValue is null
                ? default!
                : (T)cachedValue;
        }

        var value = await factory(state, cancellationToken);
        _values[key] = value;
        return value;
    }

    public override ValueTask SetAsync<T>(
        string key,
        T value,
        HybridCacheEntryOptions? options = null,
        IEnumerable<string>? tags = null,
        CancellationToken cancellationToken = default)
    {
        _values[key] = value;
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        _values.Remove(key);
        _removedKeys.Add(key);
        return ValueTask.CompletedTask;
    }

    public override ValueTask RemoveByTagAsync(
        string tag,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.CompletedTask;
    }
}
