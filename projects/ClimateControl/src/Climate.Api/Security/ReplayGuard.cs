using System.Collections.Concurrent;

namespace Climate.Api.Security;

/// <summary>Atomically reserves nonces; shared durable storage is needed for multiple API nodes.</summary>
public sealed class ReplayGuard(TimeProvider clock)
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _used = new();
    private readonly object _gate = new();

    public bool TryUse(string key)
    {
        lock (_gate)
        {
            DateTimeOffset now = clock.GetUtcNow();
            foreach (var entry in _used)
                if (entry.Value < now)
                    _used.TryRemove(entry.Key, out _);
            if (_used.Count >= 10000)
                return false;
            return _used.TryAdd(key, now.AddSeconds(241));
        }
    }
}
