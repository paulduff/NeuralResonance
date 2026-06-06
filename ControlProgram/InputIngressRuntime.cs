using System.Collections.Concurrent;
using System.Threading;

internal enum AdminInputIngressKind
{
    Sensory,
    Video
}

internal sealed record InputIngressKindSnapshot(
    int MaxConcurrent,
    int InFlight,
    int PeakInFlight,
    long Accepted,
    long Rejected);

internal sealed record InputIngressSnapshot(
    InputIngressKindSnapshot Sensory,
    InputIngressKindSnapshot Video);

internal sealed class InputIngressRuntime(IConfiguration configuration)
{
    private readonly ConcurrentDictionary<AdminInputIngressKind, InputIngressCounter> _counters = new();

    public bool TryEnter(AdminInputIngressKind kind, out IDisposable? lease, out InputIngressSnapshot snapshot)
    {
        var counter = _counters.GetOrAdd(kind, key => CreateCounter(key, configuration));
        if (!counter.TryEnter())
        {
            lease = null;
            snapshot = GetSnapshot();
            return false;
        }

        lease = new IngressLease(counter);
        snapshot = GetSnapshot();
        return true;
    }

    public InputIngressSnapshot GetSnapshot()
    {
        var sensory = _counters.GetOrAdd(AdminInputIngressKind.Sensory, key => CreateCounter(key, configuration));
        var video = _counters.GetOrAdd(AdminInputIngressKind.Video, key => CreateCounter(key, configuration));
        return new InputIngressSnapshot(sensory.ToSnapshot(), video.ToSnapshot());
    }

    private static InputIngressCounter CreateCounter(AdminInputIngressKind kind, IConfiguration configuration)
    {
        var configured = kind == AdminInputIngressKind.Video
            ? configuration.GetValue<int>("AdminInputIngress:VideoMaxConcurrent", 6)
            : configuration.GetValue<int>("AdminInputIngress:SensoryMaxConcurrent", 12);
        return new InputIngressCounter(Math.Clamp(configured, 1, 128));
    }

    private sealed class IngressLease(InputIngressCounter counter) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                counter.Exit();
            }
        }
    }

    private sealed class InputIngressCounter(int maxConcurrent)
    {
        private int _inFlight;
        private int _peakInFlight;
        private long _accepted;
        private long _rejected;

        public bool TryEnter()
        {
            var currentInFlight = Interlocked.Increment(ref _inFlight);
            if (currentInFlight > maxConcurrent)
            {
                Interlocked.Decrement(ref _inFlight);
                Interlocked.Increment(ref _rejected);
                return false;
            }

            Interlocked.Increment(ref _accepted);
            UpdatePeak(currentInFlight);
            return true;
        }

        public void Exit()
        {
            Interlocked.Decrement(ref _inFlight);
        }

        public InputIngressKindSnapshot ToSnapshot()
        {
            return new InputIngressKindSnapshot(
                maxConcurrent,
                Math.Max(0, Volatile.Read(ref _inFlight)),
                Volatile.Read(ref _peakInFlight),
                Volatile.Read(ref _accepted),
                Volatile.Read(ref _rejected));
        }

        private void UpdatePeak(int candidate)
        {
            while (true)
            {
                var current = Volatile.Read(ref _peakInFlight);
                if (candidate <= current)
                {
                    return;
                }

                if (Interlocked.CompareExchange(ref _peakInFlight, candidate, current) == current)
                {
                    return;
                }
            }
        }
    }
}
