namespace NRE.Core.Engine;

/// <summary>
/// Cerebellum: Forward-Model Error Correction via Per-Region Microzones
///
/// ANATOMICAL STRUCTURE (Eccles, Ito &amp; Szentágothai 1967; Marr 1969, Albus 1971):
///
///   MOSSY FIBERS (pons, spinal, vestibular) → granule cells → PARALLEL FIBERS
///                                                                   ↓
///   CLIMBING FIBERS (inferior olive, error/teaching) → PURKINJE CELLS (GABAergic)
///                                                                   ↓ (inhibition)
///                                                       DEEP CEREBELLAR NUCLEI
///                                                                   ↓
///                                                       VL Thalamus → Motor Cortex
///
/// The defining cerebellar computation is the Marr-Albus-Ito learning rule: a
/// parallel-fiber→Purkinje synapse is depressed/potentiated only when parallel-fiber
/// activity (context) coincides with a climbing-fiber error signal. Over time the
/// cerebellum learns to cancel the *predictable* component of an error and habituates
/// when the error stops recurring.
///
/// In NRE the cerebellum stabilizes cortical/subcortical activity. It is modelled as a
/// bank of per-region MICROZONES: each region's activity is the parallel-fiber drive,
/// its activity variance is the climbing-fiber (surprise/error) signal, and a learned
/// per-region Purkinje weight produces the corrective inhibition the engine applies.
/// This is the single source of cerebellar inhibition — there is no separate scalar
/// circuit shadowing it. (A per-region prediction error from <see cref="PredictiveCoding"/>
/// could later be blended into the climbing-fiber signal; kept intrinsic here so the
/// cerebellum does not depend on predictive coding being enabled.)
/// </summary>
public sealed class Cerebellum
{
    private readonly object _gate = new();

    // === PER-REGION MICROZONES (the cerebellar learners) ===
    private readonly Dictionary<byte, Microzone> _microzones = new();

    // Reusable buffers
    private readonly Dictionary<byte, int> _regionCountsBuffer = new(32);
    private readonly Dictionary<byte, float> _regionInhibitionsBuffer = new(32);
    private readonly List<byte> _inactiveRegionsBuffer = new(32);

    // Configuration
    public float BaseInhibitionGain { get; set; } = 0.04f;
    public float VarianceThreshold { get; set; } = 0.15f;
    public float AdaptationRate { get; set; } = 0.02f;
    public float SmoothingDecay { get; set; } = 0.92f;

    // The corrective inhibition keeps the original engine range so it cannot
    // destabilize the hot loop by over-damping.
    private const float MaxRegionInhibition = 0.3f;
    private const float DeepNucleiTonic = 0.6f;
    private const float HabituationDecay = 0.98f;
    private const float ParallelFiberActiveThreshold = 0.02f;

    // State / telemetry
    private float _globalPredictionError;
    private float _smoothedActivityLevel;
    private float _globalInhibition;
    private float _climbingFiberSignal;
    private float _mossyFiberActivity;
    private float _purkinjeOutput;
    private float _deepNucleiOutput;

    public float GetGlobalInhibition() { lock (_gate) return _globalInhibition; }
    public float GetGlobalPredictionError() { lock (_gate) return _globalPredictionError; }

    public CerebellumState Snapshot()
    {
        lock (_gate)
        {
            return new CerebellumState(
                GlobalPredictionError: _globalPredictionError,
                SmoothedActivity: _smoothedActivityLevel,
                RegionCount: _microzones.Count,
                ClimbingFiberSignal: _climbingFiberSignal,
                MossyFiberActivity: _mossyFiberActivity,
                PurkinjeOutput: _purkinjeOutput,
                DeepNucleiOutput: _deepNucleiOutput);
        }
    }

    /// <summary>
    /// Process current activity through the cerebellar microzones.
    ///
    /// Biology:
    /// 1. Mossy/parallel fibers carry each region's activity (context).
    /// 2. Climbing fibers carry the region's activity variance (error/surprise).
    /// 3. Purkinje weights learn by the Marr-Albus conjunction (active + error).
    /// 4. The learned weight is the GABAergic corrective inhibition for that region.
    /// 5. Deep nuclei (tonic − Purkinje) summarize the disinhibited output.
    /// </summary>
    public CerebellumOutput Step(
        float dt,
        IReadOnlyList<(byte hemi, int idx, byte region)> spikes,
        int totalVoxels,
        float motorCommandIntensity = 0f,
        float sensoryMismatch = 0f)
    {
        lock (_gate)
        {
            float voxelScale = MathF.Max(1f, totalVoxels / 16f);

            // === 1) MOSSY FIBER INPUT: count spikes per region ===
            _regionCountsBuffer.Clear();
            for (int i = 0; i < spikes.Count; i++)
            {
                var region = spikes[i].region;
                _regionCountsBuffer.TryGetValue(region, out var count);
                _regionCountsBuffer[region] = count + 1;
            }

            // Motor mossy-fiber drive (regions 11/12) for telemetry/diagnostics.
            float motorInput = 0f;
            if (_regionCountsBuffer.TryGetValue(11, out int motorCount)) motorInput += motorCount;
            if (_regionCountsBuffer.TryGetValue(12, out int somatoCount)) motorInput += somatoCount * 0.5f;
            _mossyFiberActivity = motorInput / voxelScale + motorCommandIntensity;

            // === 2) PER-REGION MICROZONES: variance error + conjunction learning ===
            float inhibSum = 0f;
            int inhibCount = 0;
            float weightedPurkinje = 0f;
            float varianceSum = 0f;

            foreach (var kv in _regionCountsBuffer)
            {
                byte region = kv.Key;
                if (!_microzones.TryGetValue(region, out var mz))
                {
                    mz = new Microzone();
                    _microzones[region] = mz;
                }

                float activity = kv.Value / voxelScale;
                mz.SmoothedMean = mz.SmoothedMean * SmoothingDecay + activity * (1f - SmoothingDecay);

                float deviation = MathF.Abs(activity - mz.SmoothedMean);
                mz.Variance = mz.Variance * 0.95f + deviation * 0.05f; // climbing-fiber error

                // Marr-Albus conjunction: the Purkinje weight only adapts when parallel-fiber
                // activity coincides with a climbing-fiber error. Otherwise it habituates.
                bool parallelFiberActive = activity > ParallelFiberActiveThreshold;
                bool climbingFiberError = mz.Variance > VarianceThreshold;
                if (parallelFiberActive && climbingFiberError)
                {
                    float target = Math.Clamp(
                        BaseInhibitionGain * (mz.Variance / VarianceThreshold),
                        0f, MaxRegionInhibition);
                    mz.PurkinjeWeight += (target - mz.PurkinjeWeight) * AdaptationRate;
                }
                else
                {
                    mz.PurkinjeWeight *= HabituationDecay;
                }
                mz.PurkinjeWeight = Math.Clamp(mz.PurkinjeWeight, 0f, MaxRegionInhibition);

                varianceSum += mz.Variance;
                weightedPurkinje += mz.PurkinjeWeight * activity;
                if (mz.PurkinjeWeight > 0.01f)
                {
                    inhibSum += mz.PurkinjeWeight;
                    inhibCount++;
                }
            }

            // Habituate microzones that received no parallel-fiber drive this step
            // (no climbing-fiber learning is possible without parallel-fiber activity).
            _inactiveRegionsBuffer.Clear();
            foreach (var region in _microzones.Keys)
            {
                if (!_regionCountsBuffer.ContainsKey(region))
                    _inactiveRegionsBuffer.Add(region);
            }
            for (int i = 0; i < _inactiveRegionsBuffer.Count; i++)
            {
                var mz = _microzones[_inactiveRegionsBuffer[i]];
                mz.SmoothedMean *= 0.99f;
                mz.Variance *= 0.95f;
                mz.PurkinjeWeight *= HabituationDecay;
            }

            // === 3) GLOBAL METRICS & DEEP-NUCLEI SUMMARY ===
            float globalActivity = spikes.Count / (float)Math.Max(1, totalVoxels);
            float prevSmoothed = _smoothedActivityLevel;
            _smoothedActivityLevel = _smoothedActivityLevel * SmoothingDecay + globalActivity * (1f - SmoothingDecay);
            _globalPredictionError = MathF.Abs(globalActivity - prevSmoothed);

            int activeRegions = _regionCountsBuffer.Count;
            _climbingFiberSignal = (activeRegions > 0 ? varianceSum / activeRegions : 0f) + sensoryMismatch;
            _purkinjeOutput = activeRegions > 0 ? weightedPurkinje / activeRegions : 0f;
            _deepNucleiOutput = MathF.Max(0f, DeepNucleiTonic - _purkinjeOutput);
            _globalInhibition = inhibCount > 0 ? inhibSum / inhibCount : 0f;

            // Build per-region inhibition map for the output DTO.
            _regionInhibitionsBuffer.Clear();
            foreach (var kv in _microzones)
            {
                if (kv.Value.PurkinjeWeight > 0.01f)
                    _regionInhibitionsBuffer[kv.Key] = kv.Value.PurkinjeWeight;
            }

            return new CerebellumOutput(
                GlobalInhibitionBoost: _globalInhibition,
                PredictionError: _globalPredictionError,
                SmoothedActivity: _smoothedActivityLevel,
                RegionInhibitions: _regionInhibitionsBuffer,
                DeepNucleiOutput: _deepNucleiOutput,
                PurkinjeInhibition: _purkinjeOutput);
        }
    }

    /// <summary>Get the learned corrective inhibition for a specific region.</summary>
    public float GetRegionInhibition(byte regionId)
    {
        lock (_gate)
        {
            return _microzones.TryGetValue(regionId, out var mz) ? mz.PurkinjeWeight : 0f;
        }
    }

    /// <summary>Reset all learned microzone state.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _microzones.Clear();
            _globalPredictionError = 0f;
            _smoothedActivityLevel = 0f;
            _globalInhibition = 0f;
            _climbingFiberSignal = 0f;
            _mossyFiberActivity = 0f;
            _purkinjeOutput = 0f;
            _deepNucleiOutput = 0f;
        }
    }

    /// <summary>
    /// A cerebellar microzone: parallel-fiber context (SmoothedMean), climbing-fiber
    /// error (Variance), and the learned Purkinje weight that becomes the corrective
    /// inhibition for the region.
    /// </summary>
    private sealed class Microzone
    {
        public float SmoothedMean;
        public float Variance;
        public float PurkinjeWeight;
    }
}

// === DTOs ===

public readonly record struct CerebellumState(
    float GlobalPredictionError,
    float SmoothedActivity,
    int RegionCount,
    float ClimbingFiberSignal,
    float MossyFiberActivity,
    float PurkinjeOutput,
    float DeepNucleiOutput);

public readonly record struct CerebellumOutput(
    float GlobalInhibitionBoost,
    float PredictionError,
    float SmoothedActivity,
    IReadOnlyDictionary<byte, float> RegionInhibitions,
    float DeepNucleiOutput,
    float PurkinjeInhibition);
