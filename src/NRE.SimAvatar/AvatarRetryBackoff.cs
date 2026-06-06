namespace NRE.SimAvatar;

public sealed class AvatarRetryBackoff
{
    private readonly int _maxStreak;
    private readonly int _maxExponent;
    private readonly long _baseDelayMs;

    public AvatarRetryBackoff(int maxStreak = 12, int maxExponent = 6, long baseDelayMs = 250)
    {
        _maxStreak = Math.Max(1, maxStreak);
        _maxExponent = Math.Max(0, maxExponent);
        _baseDelayMs = Math.Max(1L, baseDelayMs);
    }

    public int FailureStreak { get; private set; }

    public long RetryAfterMs { get; private set; }

    public bool IsBlocked(long nowMs) => nowMs < RetryAfterMs;

    public long RegisterFailure(long nowMs)
    {
        FailureStreak = Math.Min(FailureStreak + 1, _maxStreak);
        var exponent = Math.Min(Math.Max(0, FailureStreak - 1), _maxExponent);
        var delayMs = _baseDelayMs * (1L << exponent);
        var nextAllowed = nowMs + delayMs;
        if (nextAllowed > RetryAfterMs)
        {
            RetryAfterMs = nextAllowed;
        }

        return delayMs;
    }

    public void Reset()
    {
        FailureStreak = 0;
        RetryAfterMs = 0;
    }
}
