namespace NRE.SimAvatar;

public sealed class AvatarInputPressureGate
{
    private readonly object _sync = new();
    private readonly int _maxStreak;
    private readonly int _maxExponent;
    private readonly long _baseDelayMs;
    private readonly long _severeMinimumDelayMs;
    private int _failureStreak;
    private long _retryAfterMs;
    private string _lastReason = string.Empty;

    public AvatarInputPressureGate(
        int maxStreak = 10,
        int maxExponent = 5,
        long baseDelayMs = 500,
        long severeMinimumDelayMs = 2000)
    {
        _maxStreak = Math.Max(1, maxStreak);
        _maxExponent = Math.Max(0, maxExponent);
        _baseDelayMs = Math.Max(1L, baseDelayMs);
        _severeMinimumDelayMs = Math.Max(_baseDelayMs, severeMinimumDelayMs);
    }

    public int FailureStreak
    {
        get
        {
            lock (_sync)
            {
                return _failureStreak;
            }
        }
    }

    public long RetryAfterMs
    {
        get
        {
            lock (_sync)
            {
                return _retryAfterMs;
            }
        }
    }

    public bool ShouldPause(long nowMs, out string reason)
    {
        lock (_sync)
        {
            if (nowMs >= _retryAfterMs)
            {
                reason = string.Empty;
                return false;
            }

            var waitMs = Math.Max(0, _retryAfterMs - nowMs);
            reason = string.IsNullOrWhiteSpace(_lastReason)
                ? $"brain input pressure settling; retry in {waitMs}ms"
                : $"{_lastReason}; retry in {waitMs}ms";
            return true;
        }
    }

    public long RegisterFailure(long nowMs, string reason, bool severe = false)
    {
        lock (_sync)
        {
            _failureStreak = Math.Min(_failureStreak + 1, _maxStreak);
            var exponent = Math.Min(Math.Max(0, _failureStreak - 1), _maxExponent);
            var delayMs = _baseDelayMs * (1L << exponent);
            if (severe)
            {
                delayMs = Math.Max(delayMs, _severeMinimumDelayMs);
            }

            var nextAllowed = nowMs + delayMs;
            if (nextAllowed > _retryAfterMs)
            {
                _retryAfterMs = nextAllowed;
            }

            _lastReason = string.IsNullOrWhiteSpace(reason)
                ? "recent control endpoint dispatch failure"
                : reason.Trim();
            return Math.Max(delayMs, _retryAfterMs - nowMs);
        }
    }

    public void RegisterSuccess(long nowMs)
    {
        lock (_sync)
        {
            if (nowMs < _retryAfterMs)
            {
                return;
            }

            ResetCore();
        }
    }

    public void Reset()
    {
        lock (_sync)
        {
            ResetCore();
        }
    }

    private void ResetCore()
    {
        _failureStreak = 0;
        _retryAfterMs = 0;
        _lastReason = string.Empty;
    }
}
