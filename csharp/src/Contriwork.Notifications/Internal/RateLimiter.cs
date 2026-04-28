namespace Contriwork.Notifications.Internal;

internal sealed class SlidingWindow
{
    private readonly int _maxCount;
    private readonly TimeSpan _window;
    private readonly Queue<DateTimeOffset> _timestamps = new();
    private readonly Lock _lock = new();

    public SlidingWindow(int maxCount, int windowSeconds)
    {
        _maxCount = maxCount;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    public bool Allow(DateTimeOffset now)
    {
        lock (_lock)
        {
            while (_timestamps.Count > 0 && now - _timestamps.Peek() > _window)
            {
                _timestamps.Dequeue();
            }

            if (_timestamps.Count >= _maxCount)
            {
                return false;
            }

            _timestamps.Enqueue(now);
            return true;
        }
    }
}

internal sealed class RateLimiter
{
    private readonly Dictionary<string, RateLimitPolicy> _policies;
    private readonly Dictionary<string, SlidingWindow> _windows = new();
    private readonly Lock _lock = new();

    public RateLimiter(IReadOnlyDictionary<string, RateLimitPolicy>? policies)
    {
        _policies = policies is null
            ? new Dictionary<string, RateLimitPolicy>(StringComparer.Ordinal)
            : new Dictionary<string, RateLimitPolicy>(policies, StringComparer.Ordinal);
    }

    public RateLimitPolicy? PolicyFor(string adapterName) =>
        _policies.GetValueOrDefault(adapterName);

    public bool Allow(string adapterName, DateTimeOffset now)
    {
        if (!_policies.TryGetValue(adapterName, out var policy))
        {
            return true;
        }

        if (policy.MaxCount <= 0)
        {
            // 0 (or negative) is a valid "always block" config used by tests
            // and by callers that want to circuit-break a channel.
            return false;
        }

        SlidingWindow window;
        lock (_lock)
        {
            if (!_windows.TryGetValue(adapterName, out var existing))
            {
                existing = new SlidingWindow(policy.MaxCount, policy.WindowSeconds);
                _windows[adapterName] = existing;
            }
            window = existing;
        }

        return window.Allow(now);
    }
}
