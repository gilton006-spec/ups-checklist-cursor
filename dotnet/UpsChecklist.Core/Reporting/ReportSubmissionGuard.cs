using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace UpsChecklist.Core.Reporting;

/// <summary>
/// Best-effort duplicate suppression for a single process. It does not survive
/// restarts and does not coordinate across multiple machines.
/// </summary>
public sealed class ReportSubmissionGuard(TimeSpan? window = null)
{
    private readonly TimeSpan _window = window ?? TimeSpan.FromSeconds(45);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new();

    public bool TryAccept(string payload)
    {
        Prune(DateTimeOffset.UtcNow);
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        var now = DateTimeOffset.UtcNow;
        if (_recent.TryGetValue(key, out var previous) && now - previous < _window)
            return false;
        _recent[key] = now;
        return true;
    }

    public void Release(string payload)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
        _recent.TryRemove(key, out _);
    }

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _recent)
        {
            if (now - pair.Value >= _window)
                _recent.TryRemove(pair.Key, out _);
        }
    }
}
