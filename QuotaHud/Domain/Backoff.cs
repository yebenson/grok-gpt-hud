namespace QuotaHud;

public sealed class BackoffTracker
{
    public static readonly TimeSpan Min = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan Max = TimeSpan.FromMinutes(15);
    readonly Dictionary<string, int> _failures = new();
    readonly Dictionary<string, DateTime> _nextAllowedAt = new();

    public static TimeSpan Compute(int consecutiveFailures)
    {
        if (consecutiveFailures <= 0) return TimeSpan.Zero;
        var ms = Min.TotalMilliseconds * Math.Pow(2, consecutiveFailures - 1);
        return TimeSpan.FromMilliseconds(Math.Min(Max.TotalMilliseconds, ms));
    }

    public void RecordSuccess(string id)
    {
        _failures.Remove(id);
        _nextAllowedAt.Remove(id);
    }

    public TimeSpan RecordFailure(string id, DateTime? now = null)
    {
        var count = (_failures.TryGetValue(id, out var n) ? n : 0) + 1;
        _failures[id] = count;
        var delay = Compute(count);
        _nextAllowedAt[id] = (now ?? DateTime.UtcNow) + delay;
        return delay;
    }

    public bool ShouldSkip(string id, DateTime? now = null)
    {
        if (!_nextAllowedAt.TryGetValue(id, out var until)) return false;
        return (now ?? DateTime.UtcNow) < until;
    }
}
