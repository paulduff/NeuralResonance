using System.Collections.Concurrent;

// Keeps compatibility fallbacks per live target. A failed gRPC target is retried
// after a short cooldown so a transient restart does not leave it on HTTP forever.
internal sealed class TransportCapabilityCache
{
    private const int GrpcFailureThreshold = 6;
    private const long GrpcRetryCooldownMs = 15_000;
    private readonly ConcurrentDictionary<string, TargetCapabilities> _targets = new(StringComparer.OrdinalIgnoreCase);

    public bool ShouldAttemptGrpc(string targetInstanceKey, long nowMs)
    {
        if (!_targets.TryGetValue(targetInstanceKey, out var capabilities))
        {
            return true;
        }

        lock (capabilities.Gate)
        {
            if (capabilities.GrpcDisabledUntilMs <= nowMs)
            {
                capabilities.GrpcDisabledUntilMs = 0;
                capabilities.GrpcFailureCount = 0;
                return true;
            }

            return false;
        }
    }

    public int RecordGrpcFailure(string targetInstanceKey, bool immediateDisable, long nowMs)
    {
        var capabilities = _targets.GetOrAdd(targetInstanceKey, static _ => new TargetCapabilities());
        lock (capabilities.Gate)
        {
            capabilities.GrpcFailureCount = immediateDisable
                ? GrpcFailureThreshold
                : Math.Min(GrpcFailureThreshold, capabilities.GrpcFailureCount + 1);
            if (capabilities.GrpcFailureCount >= GrpcFailureThreshold)
            {
                capabilities.GrpcDisabledUntilMs = nowMs + GrpcRetryCooldownMs;
            }

            return capabilities.GrpcFailureCount;
        }
    }

    public void RecordGrpcSuccess(string targetInstanceKey)
    {
        if (!_targets.TryGetValue(targetInstanceKey, out var capabilities))
        {
            return;
        }

        lock (capabilities.Gate)
        {
            capabilities.GrpcFailureCount = 0;
            capabilities.GrpcDisabledUntilMs = 0;
        }
    }

    public bool IsHttpBatchEndpointUnavailable(string targetInstanceKey) =>
        _targets.TryGetValue(targetInstanceKey, out var capabilities) && Volatile.Read(ref capabilities.HttpBatchEndpointUnavailable) != 0;

    public bool PrefersJsonBatch(string targetInstanceKey) =>
        _targets.TryGetValue(targetInstanceKey, out var capabilities) && Volatile.Read(ref capabilities.PreferJsonBatch) != 0;

    public void MarkHttpBatchEndpointUnavailable(string targetInstanceKey) =>
        Interlocked.Exchange(ref _targets.GetOrAdd(targetInstanceKey, static _ => new TargetCapabilities()).HttpBatchEndpointUnavailable, 1);

    public void MarkPreferJsonBatch(string targetInstanceKey) =>
        Interlocked.Exchange(ref _targets.GetOrAdd(targetInstanceKey, static _ => new TargetCapabilities()).PreferJsonBatch, 1);

    public void MarkBinaryBatchSuccess(string targetInstanceKey)
    {
        if (!_targets.TryGetValue(targetInstanceKey, out var capabilities))
        {
            return;
        }

        Interlocked.Exchange(ref capabilities.HttpBatchEndpointUnavailable, 0);
        Interlocked.Exchange(ref capabilities.PreferJsonBatch, 0);
    }

    public void MarkJsonBatchSuccess(string targetInstanceKey)
    {
        var capabilities = _targets.GetOrAdd(targetInstanceKey, static _ => new TargetCapabilities());
        Interlocked.Exchange(ref capabilities.HttpBatchEndpointUnavailable, 0);
        Interlocked.Exchange(ref capabilities.PreferJsonBatch, 1);
    }

    public void Clear() => _targets.Clear();

    public void PruneTo(IEnumerable<string> liveInstanceKeys)
    {
        var live = new HashSet<string>(liveInstanceKeys, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in _targets)
        {
            if (!live.Contains(entry.Key))
            {
                _targets.TryRemove(entry.Key, out _);
            }
        }
    }

    private sealed class TargetCapabilities
    {
        public object Gate { get; } = new();
        public int GrpcFailureCount;
        public long GrpcDisabledUntilMs;
        public int HttpBatchEndpointUnavailable;
        public int PreferJsonBatch;
    }
}
