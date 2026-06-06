using System.Collections.Concurrent;

internal sealed class HttpRequestProfiler
{
    private const int RecentDurationSampleLimit = 256;
    private const int RecentSlowSampleLimit = 128;
    private const double SlowRequestThresholdMs = 250.0;

    private readonly ConcurrentDictionary<string, EndpointProfileAccumulator> _endpoints = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _slowGate = new();
    private readonly Queue<SlowRequestSample> _recentSlowRequests = new();
    private int _activeRequests;

    public int RequestStarted(string path)
    {
        var normalizedPath = NormalizePath(path);
        var endpoint = _endpoints.GetOrAdd(normalizedPath, static key => new EndpointProfileAccumulator(key));
        endpoint.RequestStarted();
        return Interlocked.Increment(ref _activeRequests);
    }

    public void RequestCompleted(string path, string method, string? query, int statusCode, double elapsedMs, string? errorType)
    {
        var normalizedPath = NormalizePath(path);
        var endpoint = _endpoints.GetOrAdd(normalizedPath, static key => new EndpointProfileAccumulator(key));
        endpoint.RequestCompleted(statusCode, elapsedMs, errorType, SlowRequestThresholdMs);
        var activeAfter = Math.Max(0, Interlocked.Decrement(ref _activeRequests));
        if (elapsedMs < SlowRequestThresholdMs)
        {
            return;
        }

        var sample = new SlowRequestSample(
            TimestampUtc: DateTimeOffset.UtcNow,
            Method: string.IsNullOrWhiteSpace(method) ? "GET" : method,
            Path: normalizedPath,
            Query: NormalizeQuery(query),
            StatusCode: statusCode,
            DurationMs: elapsedMs,
            ActiveRequestsAfter: activeAfter,
            ErrorType: string.IsNullOrWhiteSpace(errorType) ? null : errorType);

        lock (_slowGate)
        {
            _recentSlowRequests.Enqueue(sample);
            while (_recentSlowRequests.Count > RecentSlowSampleLimit)
            {
                _recentSlowRequests.Dequeue();
            }
        }
    }

    public object GetSnapshot(int maxEndpoints = 24, int maxRecentSlow = 24)
    {
        var endpoints = _endpoints.Values
            .Select(static accumulator => accumulator.CreateSnapshot())
            .OrderByDescending(static snapshot => snapshot.MaxDurationMs)
            .ThenByDescending(static snapshot => snapshot.CompletedRequests)
            .Take(Math.Clamp(maxEndpoints, 1, 256))
            .ToArray();

        SlowRequestSample[] recentSlowRequests;
        lock (_slowGate)
        {
            recentSlowRequests = _recentSlowRequests
                .TakeLast(Math.Clamp(maxRecentSlow, 1, RecentSlowSampleLimit))
                .ToArray();
        }

        return new
        {
            capturedUtc = DateTimeOffset.UtcNow,
            activeRequests = Math.Max(0, Volatile.Read(ref _activeRequests)),
            slowThresholdMs = SlowRequestThresholdMs,
            endpoints,
            recentSlowRequests
        };
    }

    private static string NormalizePath(string? path)
        => string.IsNullOrWhiteSpace(path) ? "/" : path.Trim();

    private static string? NormalizeQuery(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var trimmed = query.Trim();
        if (trimmed.Length <= 240)
        {
            return trimmed;
        }

        return $"{trimmed[..240]}...";
    }

    private sealed class EndpointProfileAccumulator(string path)
    {
        private readonly object _gate = new();
        private readonly Queue<double> _recentDurationsMs = new();
        private long _completedRequests;
        private long _slowRequests;
        private double _totalDurationMs;
        private double _maxDurationMs;
        private int _activeRequests;
        private int _lastStatusCode;
        private string? _lastErrorType;
        private DateTimeOffset _lastCompletedUtc = DateTimeOffset.MinValue;

        public void RequestStarted()
            => Interlocked.Increment(ref _activeRequests);

        public void RequestCompleted(int statusCode, double elapsedMs, string? errorType, double slowThresholdMs)
        {
            lock (_gate)
            {
                _completedRequests++;
                _totalDurationMs += elapsedMs;
                if (elapsedMs > _maxDurationMs)
                {
                    _maxDurationMs = elapsedMs;
                }

                if (elapsedMs >= slowThresholdMs)
                {
                    _slowRequests++;
                }

                _lastStatusCode = statusCode;
                _lastErrorType = string.IsNullOrWhiteSpace(errorType) ? null : errorType;
                _lastCompletedUtc = DateTimeOffset.UtcNow;
                _recentDurationsMs.Enqueue(elapsedMs);
                while (_recentDurationsMs.Count > RecentDurationSampleLimit)
                {
                    _recentDurationsMs.Dequeue();
                }
            }

            Interlocked.Decrement(ref _activeRequests);
        }

        public EndpointProfileSnapshot CreateSnapshot()
        {
            lock (_gate)
            {
                var recentDurations = _recentDurationsMs.ToArray();
                Array.Sort(recentDurations);
                var avgMs = _completedRequests > 0 ? _totalDurationMs / _completedRequests : 0.0;
                var p95Index = recentDurations.Length == 0
                    ? -1
                    : Math.Clamp((int)Math.Ceiling(recentDurations.Length * 0.95) - 1, 0, recentDurations.Length - 1);
                var p95Ms = p95Index >= 0 ? recentDurations[p95Index] : 0.0;
                return new EndpointProfileSnapshot(
                    Path: path,
                    CompletedRequests: _completedRequests,
                    ActiveRequests: Math.Max(0, Volatile.Read(ref _activeRequests)),
                    SlowRequests: _slowRequests,
                    AverageDurationMs: avgMs,
                    P95DurationMs: p95Ms,
                    MaxDurationMs: _maxDurationMs,
                    LastStatusCode: _lastStatusCode,
                    LastErrorType: _lastErrorType,
                    LastCompletedUtc: _lastCompletedUtc);
            }
        }
    }

    internal sealed record EndpointProfileSnapshot(
        string Path,
        long CompletedRequests,
        int ActiveRequests,
        long SlowRequests,
        double AverageDurationMs,
        double P95DurationMs,
        double MaxDurationMs,
        int LastStatusCode,
        string? LastErrorType,
        DateTimeOffset LastCompletedUtc);

    internal sealed record SlowRequestSample(
        DateTimeOffset TimestampUtc,
        string Method,
        string Path,
        string? Query,
        int StatusCode,
        double DurationMs,
        int ActiveRequestsAfter,
        string? ErrorType);
}
