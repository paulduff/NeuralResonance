namespace NRE.SimAvatar;

public sealed class AvatarWarningGate
{
    private readonly long _minimumIntervalMs;

    public AvatarWarningGate(long minimumIntervalMs = 4000)
    {
        _minimumIntervalMs = Math.Max(0L, minimumIntervalMs);
    }

    public string LastMessage { get; private set; } = string.Empty;

    public long LastLoggedMs { get; private set; }

    public bool ShouldLog(string message, long nowMs)
        => ShouldLog(message, message, nowMs);

    public bool ShouldLog(string message, string dedupeKey, long nowMs)
    {
        var key = string.IsNullOrWhiteSpace(dedupeKey) ? message : dedupeKey;
        if (string.Equals(LastMessage, key, StringComparison.Ordinal) &&
            (nowMs - LastLoggedMs) < _minimumIntervalMs)
        {
            return false;
        }

        LastMessage = key;
        LastLoggedMs = nowMs;
        return true;
    }

    public void Reset()
    {
        LastMessage = string.Empty;
        LastLoggedMs = 0;
    }
}
