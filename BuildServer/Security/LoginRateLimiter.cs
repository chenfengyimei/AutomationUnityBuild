using System.Collections.Concurrent;

namespace BuildServer.Security;

public sealed class LoginRateLimiter
{
    private readonly ConcurrentDictionary<string, AttemptWindow> _attempts = new(StringComparer.OrdinalIgnoreCase);

    public bool IsAllowed(string key)
    {
        AttemptWindow window = _attempts.GetOrAdd(key, _ => new AttemptWindow(DateTimeOffset.Now));
        lock (window)
        {
            if (DateTimeOffset.Now - window.StartedAt > TimeSpan.FromMinutes(10))
            {
                window.StartedAt = DateTimeOffset.Now;
                window.Failures = 0;
            }

            return window.Failures < 8;
        }
    }

    public void RecordFailure(string key)
    {
        AttemptWindow window = _attempts.GetOrAdd(key, _ => new AttemptWindow(DateTimeOffset.Now));
        lock (window)
        {
            window.Failures++;
        }
    }

    public void RecordSuccess(string key)
    {
        _attempts.TryRemove(key, out _);
    }

    private sealed class AttemptWindow(DateTimeOffset startedAt)
    {
        public DateTimeOffset StartedAt { get; set; } = startedAt;
        public int Failures { get; set; }
    }
}
