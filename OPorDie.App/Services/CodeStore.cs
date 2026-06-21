using System.Collections.Concurrent;

namespace OPorDie.Services;

// Thread-safe in-memory store for short-lived 6-digit verification codes.
// Codes expire after 15 minutes and are deleted after first use.
public class CodeStore
{
    private record Entry(string Code, DateTime Expires);
    private readonly ConcurrentDictionary<string, Entry> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly Random _rng = new();

    public string Generate(string key)
    {
        var code = _rng.Next(100_000, 1_000_000).ToString();
        _store[key] = new Entry(code, DateTime.UtcNow.AddMinutes(15));
        return code;
    }

    public bool Validate(string key, string code)
    {
        if (!_store.TryGetValue(key, out var entry)) return false;
        if (DateTime.UtcNow > entry.Expires) { _store.TryRemove(key, out _); return false; }
        if (!string.Equals(entry.Code, code, StringComparison.Ordinal)) return false;
        _store.TryRemove(key, out _);
        return true;
    }
}
