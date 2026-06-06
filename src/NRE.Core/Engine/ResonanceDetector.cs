using System.Collections;
using System.Numerics;

namespace NRE.Core.Engine;

/// <summary>
/// Resonance detector: tracks recent spike activity to identify synchronized neural assemblies.
/// Uses a sliding window of spike history to find voxels with high temporal coherence.
/// 
/// Ring buffer: _head points to where we LAST wrote. On push:
///   1) Advance head to next slot (this is the oldest data we're overwriting)
///   2) Count spikes being removed from that oldest slot
///   3) Clear and write new spikes
/// </summary>
public sealed class ResonanceDetector
{
    private readonly int _w, _h, _d;
    private readonly int _window;
    private readonly BitArray[] _history;
    private int _head;
    
    // Cache for counts to reduce allocation
    private readonly int[] _countCache;
    
    // Track total spikes for activity level
    private int _totalSpikesInWindow;

    public int WindowSteps => _window;
    public int TotalSpikesInWindow => _totalSpikesInWindow;

    public ResonanceDetector(int w, int h, int d, int windowSteps = 32)
    {
        _w = w; _h = h; _d = d;
        _window = Math.Max(12, windowSteps);

        int n = w * h * d;
        _history = new BitArray[_window];
        for (int i = 0; i < _window; i++)
            _history[i] = new BitArray(n);

        _countCache = new int[n];
        _head = 0;
    }

    public void PushSpikes(int[] spikingIndices)
    {
        // Advance head FIRST to get the oldest slot (the one we're about to overwrite)
        _head = (_head + 1) % _window;
        
        // Count spikes being removed from this oldest slot
        var oldBits = _history[_head];
        int removed = 0;
        for (int i = oldBits.NextSetBit(0); i >= 0; i = oldBits.NextSetBit(i + 1))
            removed++;
        
        // Clear and write new spikes
        oldBits.SetAll(false);
        for (int i = 0; i < spikingIndices.Length; i++)
        {
            int idx = spikingIndices[i];
            if (idx >= 0 && idx < oldBits.Length)
                oldBits[idx] = true;
        }
        
        _totalSpikesInWindow += spikingIndices.Length - removed;
        if (_totalSpikesInWindow < 0) _totalSpikesInWindow = 0;
    }
    
    /// <summary>
    /// OPTIMIZED: Accept List directly to avoid .ToArray() allocation in hot path.
    /// </summary>
    public void PushSpikesFromList(List<int> spikingIndices)
    {
        // Advance head FIRST to get the oldest slot (the one we're about to overwrite)
        _head = (_head + 1) % _window;
        
        // Count spikes being removed from this oldest slot
        var oldBits = _history[_head];
        int removed = 0;
        for (int i = oldBits.NextSetBit(0); i >= 0; i = oldBits.NextSetBit(i + 1))
            removed++;
        
        // Clear and write new spikes
        oldBits.SetAll(false);
        for (int i = 0; i < spikingIndices.Count; i++)
        {
            int idx = spikingIndices[i];
            if (idx >= 0 && idx < oldBits.Length)
                oldBits[idx] = true;
        }
        
        _totalSpikesInWindow += spikingIndices.Count - removed;
        if (_totalSpikesInWindow < 0) _totalSpikesInWindow = 0;
    }

    public Vector3[] GetHighResonantClusters(float minDensity = 0.15f, int maxPoints = 4096)
    {
        var vox = GetResonantVoxels(minDensity, maxPoints);
        var pts = new Vector3[vox.Length];
        for (int i = 0; i < vox.Length; i++) pts[i] = vox[i].Pos;
        return pts;
    }

    /// <summary>Returns resonant voxels with density in [0..1].</summary>
    public ResonantVoxel[] GetResonantVoxels(float minDensity = 0.15f, int maxPoints = 4096)
    {
        int n = _w * _h * _d;
        
        // Clear cache
        Array.Clear(_countCache, 0, n);

        // Count spikes per voxel across window
        for (int t = 0; t < _window; t++)
        {
            var bits = _history[t];
            for (int i = bits.NextSetBit(0); i >= 0; i = bits.NextSetBit(i + 1))
                _countCache[i]++;
        }

        // Adaptive threshold: lower when activity is low
        float effectiveMinDensity = minDensity;
        if (_totalSpikesInWindow < _window * 10)
        {
            // Very low activity - lower threshold to still find patterns
            effectiveMinDensity = Math.Max(0.06f, minDensity * 0.5f);
        }
        
        int threshold = Math.Max(2, (int)MathF.Ceiling(effectiveMinDensity * _window));
        var voxels = new List<ResonantVoxel>(512);

        for (int idx = 0; idx < n && voxels.Count < maxPoints; idx++)
        {
            int c = _countCache[idx];
            if (c >= threshold)
            {
                int x = idx % _w;
                int t = idx / _w;
                int y = t % _h;
                int z = t / _h;

                float dens = c / (float)_window;
                voxels.Add(new ResonantVoxel(idx, new Vector3(x, y, z), dens));
            }
        }

        return voxels.ToArray();
    }

    public readonly record struct ResonantVoxel(int Index, Vector3 Pos, float Density01);
}

internal static class BitArrayExt
{
    public static int NextSetBit(this BitArray bits, int startIndex)
    {
        for (int i = startIndex; i < bits.Length; i++)
            if (bits[i]) return i;
        return -1;
    }
}
