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
    private readonly TimeSpan _window = window ?? TimeSpan.FromMinutes(10);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _recent = new();

    public bool TryReserve(string payload)
    {
        var key = Hash(payload);
        var now = DateTimeOffset.UtcNow;
        Prune(now);

        while (true)
        {
            if (_recent.TryAdd(key, now))
                return true;

            if (!_recent.TryGetValue(key, out var previous))
                continue;

            if (now - previous < _window)
                return false;

            // Expired reservation: only one thread wins the replace.
            if (_recent.TryUpdate(key, now, previous))
                return true;
        }
    }

    /// <summary>
    /// Clears a reservation only when the server knows the mail transport was never contacted
    /// (validation/PDF failure before Send). Do not call after an uncertain SMTP outcome.
    /// </summary>
    public void ReleaseAfterDefiniteFailure(string payload)
    {
        _recent.TryRemove(Hash(payload), out _);
    }

    // Kept for tests/call sites that still use the short name.
    public bool TryAccept(string payload) => TryReserve(payload);

    public void Release(string payload) => ReleaseAfterDefiniteFailure(payload);

    private static string Hash(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));

    private void Prune(DateTimeOffset now)
    {
        foreach (var pair in _recent)
        {
            if (now - pair.Value >= _window)
                _recent.TryRemove(pair.Key, out _);
        }
    }
}
