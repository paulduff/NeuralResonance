using System.Buffers;
using System.Runtime.CompilerServices;

namespace NRE.Core.Engine;

public sealed class NeuralVolume
{
    public int W { get; }
    public int H { get; }
    public int D { get; }
    public int Total { get; }

    // Flat 1D arrays — cache-friendly
    public float[] Vm;
    public float[] Energy;
    public float[] Theta;
    public bool[] Active;
    public byte[] RegionId;

    // ─────────────────────────────────────────────────────────────────────────────
    // Delayed signal transport (performance + thread safety)
    //
    // Ring of per-tick buckets:
    //   bucket[(cursor + delay) % ringSize].Add(fi, intensity)
    //
    // Each bucket stores events in a pooled array and is protected by a lock.
    // DrainDueSignals() locks only the current bucket briefly while it applies
    // accumulated events and resets the count.
    //
    // This design:
    //   - avoids per-voxel Queue<T> (GC heavy, not thread-safe)
    //   - avoids List<T> reallocation churn under burst loads
    //   - remains safe for concurrent cross-hemisphere enqueues
    // ─────────────────────────────────────────────────────────────────────────────

    public readonly struct SignalEvent
    {
        public readonly int Fi;
        public readonly float Intensity;
        public SignalEvent(int fi, float intensity)
        {
            Fi = fi;
            Intensity = intensity;
        }
    }

    private sealed class SignalBucket
    {
        private SignalEvent[] _arr;
        private int _count;
        private readonly object _lock = new();

        public SignalBucket(int initialCapacity)
        {
            _arr = ArrayPool<SignalEvent>.Shared.Rent(Math.Max(64, initialCapacity));
            _count = 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(int fi, float intensity)
        {
            lock (_lock)
            {
                if (_count >= _arr.Length)
                {
                    // grow (pooled)
                    var old = _arr;
                    _arr = ArrayPool<SignalEvent>.Shared.Rent(old.Length * 2);
                    Array.Copy(old, 0, _arr, 0, _count);
                    ArrayPool<SignalEvent>.Shared.Return(old, clearArray: false);
                }

                _arr[_count++] = new SignalEvent(fi, intensity);
            }
        }

        public void AddMany(SignalEvent[] src, int count)
        {
            if (count <= 0) return;
            lock (_lock)
            {
                int needed = _count + count;
                if (needed > _arr.Length)
                {
                    // grow (pooled)
                    var old = _arr;
                    int newLen = old.Length;
                    while (newLen < needed) newLen *= 2;
                    _arr = ArrayPool<SignalEvent>.Shared.Rent(newLen);
                    Array.Copy(old, 0, _arr, 0, _count);
                    ArrayPool<SignalEvent>.Shared.Return(old, clearArray: false);
                }

                Array.Copy(src, 0, _arr, _count, count);
                _count += count;
            }
        }

        public void DrainInto(float[] vm)
        {
            lock (_lock)
            {
                for (int i = 0; i < _count; i++)
                {
                    var e = _arr[i];
                    vm[e.Fi] += e.Intensity;
                }
                _count = 0;
            }
        }
    }

    private readonly int _ringSize;
    private int _ringCursor;
    private readonly SignalBucket[] _ring;

    // Expose ring parameters for high-performance batching (cursor is stable during a tick).
    public int RingSize => _ringSize;
    public int RingCursor => _ringCursor;

    public float BaseHz { get; set; }
    public float Phase { get; private set; }
    public float OscGain { get; set; }

    public NeuralVolume(int w, int h, int d, float baseHz, float oscGain, float baseThreshold, float energyMax, int signalRingSize = 8)
    {
        W = w; H = h; D = d;
        Total = w * h * d;

        Vm = new float[Total];
        Energy = new float[Total];
        Theta = new float[Total];
        Active = new bool[Total];
        RegionId = new byte[Total];

        BaseHz = baseHz;
        OscGain = oscGain;

        _ringSize = Math.Max(4, signalRingSize);
        _ringCursor = 0;
        _ring = new SignalBucket[_ringSize];
        for (int i = 0; i < _ringSize; i++)
            _ring[i] = new SignalBucket(initialCapacity: 1024);

        for (int i = 0; i < Total; i++)
        {
            Vm[i] = 0f;
            Energy[i] = energyMax;
            Theta[i] = baseThreshold;
            Active[i] = true;
            RegionId[i] = 0;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(int x, int y, int z) => (z * H + y) * W + x;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public (int x, int y, int z) FromIndex(int idx)
    {
        int x = idx % W;
        int t = idx / W;
        int y = t % H;
        int z = t / H;
        return (x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int BucketIndex(int delayTicks)
    {
        if (delayTicks < 0) delayTicks = 0;
        return (_ringCursor + delayTicks) % _ringSize;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int BucketIndexForDelay(int delayTicks)
        => BucketIndex(delayTicks);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddManyToBucket(int bucketIndex, SignalEvent[] events, int count)
    {
        if ((uint)bucketIndex >= (uint)_ringSize) return;
        _ring[bucketIndex].AddMany(events, count);
    }

    public void EnqueueSignal(int x, int y, int z, DelayedSignal s)
    {
        int fi = IndexOf(x, y, z);
        EnqueueSignalByIndex(fi, s);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnqueueSignalByIndex(int fi, DelayedSignal s)
    {
        if (!Active[fi]) return;
        int bi = BucketIndex(s.TicksRemaining);
        _ring[bi].Add(fi, s.Intensity);
    }

    /// <summary>
    /// Drain all signals due this tick, apply them to Vm, then advance the ring cursor.
    /// Called once per sim tick (before voxel update).
    /// Thread-safe with concurrent enqueues into the due bucket.
    /// </summary>
    public void DrainDueSignals()
    {
        int bi = _ringCursor;
        _ring[bi].DrainInto(Vm);

        _ringCursor++;
        if (_ringCursor >= _ringSize) _ringCursor = 0;
    }

    public void StepOscillator(float dt)
    {
        Phase += dt * (2f * MathF.PI) * BaseHz;
        if (Phase > 2f * MathF.PI) Phase -= 2f * MathF.PI;
    }

    public float OscTerm() => MathF.Sin(Phase) * OscGain;

    public struct DelayedSignal
    {
        public int TicksRemaining;
        public float Intensity;

        public DelayedSignal(int ticks, float intensity)
        {
            TicksRemaining = ticks;
            Intensity = intensity;
        }

        public bool Step()
        {
            TicksRemaining--;
            return TicksRemaining <= 0;
        }
    }
}
