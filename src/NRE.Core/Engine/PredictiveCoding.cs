namespace NRE.Core.Engine;

/// <summary>
/// Hierarchical Predictive Coding (Folded Archive Entry 022)
/// 
/// Implements the canonical cortical computation:
/// - Higher regions generate predictions about lower regions
/// - Lower regions compute prediction errors (|actual - predicted|)
/// - Errors propagate UP the hierarchy, predictions flow DOWN
/// - Precision (attention) weights error signals
/// 
/// Biology: Rao & Ballard 1999, Friston 2005, Bastos et al. 2012
/// </summary>
public sealed class PredictiveCoding
{
    private readonly object _gate = new();
    
    // Hierarchy levels (0 = lowest/relay, 3 = highest/executive)
    // Updated to use per-gyrus region IDs from RegionIds.cs
    private static readonly Dictionary<byte, int> RegionHierarchy = new()
    {
        // Level 0: Relay / brainstem
        [RegionIds.Thalamus] = 0,
        [RegionIds.Brainstem] = 0,
        [RegionIds.Pons] = 0,

        // Level 1: Primary sensory + homeostatic + cerebellum
        [RegionIds.Hypothalamus] = 1,
        [RegionIds.Cerebellum] = 1,
        [RegionIds.PostcentralGyrus] = 1,        // S1 - primary somatosensory
        [RegionIds.SuperiorOccipital] = 1,        // visual (V1/V2 belt)
        [RegionIds.InferiorOccipital] = 1,        // visual (primary visual belt)
        [RegionIds.SuperiorTemporalGyrus] = 1,    // auditory (A1/Wernicke's)

        // Level 2: Association, motor, limbic
        [RegionIds.PrecentralGyrus] = 2,          // M1 - primary motor
        [RegionIds.BasalGanglia] = 2,
        [RegionIds.Amygdala] = 2,
        [RegionIds.Hippocampus] = 2,
        [RegionIds.MiddleTemporalGyrus] = 2,      // lexical-semantic association
        [RegionIds.InferiorTemporalGyrus] = 2,    // ventral stream (object recognition)
        [RegionIds.SuperiorParietal] = 2,          // dorsal stream (spatial)
        [RegionIds.InferiorParietal] = 2,          // multimodal integration
        [RegionIds.SupramarginalGyrus] = 2,        // phonological processing
        [RegionIds.AngularGyrus] = 2,              // semantic hub

        // Level 3: Executive / prefrontal
        [RegionIds.SuperiorFrontalGyrus] = 3,     // dorsolateral PFC
        [RegionIds.MiddleFrontalGyrus] = 3,       // PFC association
        [RegionIds.InferiorFrontalGyrus] = 3,     // Broca's area / executive
    };
    
    // Which regions send predictions TO which (top-down, feedback)
    // Updated: old "PFC(13)" → frontal gyri; "Visual(9)" → occipital; "Auditory(10)" → STG
    private static readonly Dictionary<byte, byte[]> PredictionTargets = new()
    {
        // Frontal executive → motor, sensory, limbic, thalamus
        [RegionIds.SuperiorFrontalGyrus] = new byte[] {
            RegionIds.PrecentralGyrus, RegionIds.PostcentralGyrus,
            RegionIds.SuperiorOccipital, RegionIds.InferiorOccipital,
            RegionIds.SuperiorTemporalGyrus,
            RegionIds.Hippocampus, RegionIds.Amygdala, RegionIds.BasalGanglia, RegionIds.Thalamus },
        [RegionIds.MiddleFrontalGyrus] = new byte[] {
            RegionIds.PrecentralGyrus, RegionIds.SuperiorParietal, RegionIds.InferiorParietal,
            RegionIds.Thalamus },
        [RegionIds.InferiorFrontalGyrus] = new byte[] {
            RegionIds.PrecentralGyrus, RegionIds.SuperiorTemporalGyrus,
            RegionIds.MiddleTemporalGyrus, RegionIds.AngularGyrus },

        // Motor → somatosensory, cerebellum, BG
        [RegionIds.PrecentralGyrus] = new byte[] {
            RegionIds.PostcentralGyrus, RegionIds.Cerebellum, RegionIds.BasalGanglia },

        // Somatosensory → thalamus
        [RegionIds.PostcentralGyrus] = new byte[] { RegionIds.Thalamus },

        // Visual → thalamus (LGN)
        [RegionIds.SuperiorOccipital] = new byte[] { RegionIds.Thalamus },
        [RegionIds.InferiorOccipital] = new byte[] { RegionIds.Thalamus },

        // Auditory → thalamus (MGN)
        [RegionIds.SuperiorTemporalGyrus] = new byte[] { RegionIds.Thalamus },

        // Hippocampus → sensory cortices, PFC (context predictions)
        [RegionIds.Hippocampus] = new byte[] {
            RegionIds.SuperiorOccipital, RegionIds.InferiorOccipital,
            RegionIds.SuperiorTemporalGyrus, RegionIds.PostcentralGyrus,
            RegionIds.SuperiorFrontalGyrus },

        // Amygdala → sensory, hippocampus, PFC (threat predictions)
        [RegionIds.Amygdala] = new byte[] {
            RegionIds.SuperiorOccipital, RegionIds.InferiorOccipital,
            RegionIds.SuperiorTemporalGyrus, RegionIds.Hippocampus,
            RegionIds.SuperiorFrontalGyrus },

        // BG → thalamus (gating)
        [RegionIds.BasalGanglia] = new byte[] { RegionIds.Thalamus },

        // Cerebellum → motor (forward model error correction)
        [RegionIds.Cerebellum] = new byte[] { RegionIds.PrecentralGyrus },

        // Parietal association → lower sensory
        [RegionIds.AngularGyrus] = new byte[] {
            RegionIds.SuperiorTemporalGyrus, RegionIds.SuperiorOccipital },
        [RegionIds.SuperiorParietal] = new byte[] {
            RegionIds.SuperiorOccipital, RegionIds.InferiorOccipital },
    };
    
    // Per-region state
    private readonly Dictionary<byte, RegionState> _states = new();
    
    // Reusable buffers
    private readonly Dictionary<byte, float> _activityBuffer = new(16);
    private readonly Dictionary<byte, float> _errorBuffer = new(16);
    
    // Global metrics
    private float _globalError;
    private float _globalPrecision;
    
    public PredictiveCoding()
    {
        // Initialize state for all regions
        foreach (var kv in RegionHierarchy)
        {
            _states[kv.Key] = new RegionState
            {
                RegionId = kv.Key,
                HierarchyLevel = kv.Value,
                Precision = 0.5f,
                ActivityEma = 0f,
            };
            
            // Initialize prediction buffers based on targets
            if (PredictionTargets.TryGetValue(kv.Key, out var targets))
            {
                foreach (var target in targets)
                    _states[kv.Key].Predictions[target] = 0.5f; // Neutral prediction
            }
        }
    }
    
    /// <summary>Snapshot for monitoring/UI.</summary>
    public PredictiveCodingSnapshot Snapshot()
    {
        lock (_gate)
        {
            var regionErrors = new Dictionary<byte, float>();
            var regionPrecisions = new Dictionary<byte, float>();
            
            foreach (var kv in _states)
            {
                regionErrors[kv.Key] = kv.Value.TotalError;
                regionPrecisions[kv.Key] = kv.Value.Precision;
            }
            
            return new PredictiveCodingSnapshot(
                GlobalError: _globalError,
                GlobalPrecision: _globalPrecision,
                RegionErrors: regionErrors,
                RegionPrecisions: regionPrecisions);
        }
    }
    
    /// <summary>
    /// Main update step. Called on intermediate lane (~20Hz).
    /// </summary>
    public PredictiveCodingOutput Step(
        float dt,
        IReadOnlyList<(byte hemi, int idx, byte region)> spikes,
        int totalVoxels,
        NeuromodulatorField mods,
        NreEngineOptions opt)
    {
        if (!opt.EnablePredictiveCoding)
        {
            return new PredictiveCodingOutput(
                TotalError: 0f,
                ThresholdModByRegion: new Dictionary<byte, float>(),
                SurprisedRegions: Array.Empty<byte>());
        }
        
        lock (_gate)
        {
            // 1) Compute current activity per region
            _activityBuffer.Clear();
            for (int i = 0; i < spikes.Count; i++)
            {
                byte r = spikes[i].region;
                if (r == 255) continue;
                _activityBuffer.TryGetValue(r, out float count);
                _activityBuffer[r] = count + 1f;
            }
            
            // Normalize and update EMA
            float voxelsPerRegion = MathF.Max(1f, totalVoxels / 13f);
            foreach (var kv in _activityBuffer)
            {
                float activity01 = MathF.Min(1f, kv.Value / voxelsPerRegion);
                if (_states.TryGetValue(kv.Key, out var state))
                {
                    // EMA update
                    state.ActivityEma = state.ActivityEma * 0.92f + activity01 * 0.08f;
                }
            }
            
            // 2) Update precision based on neuromodulators
            float basePrecision = opt.BasePrecision 
                + mods.Noradrenaline * opt.NoradrenalinePrecisionGain
                + mods.Dopamine * opt.DopaminePrecisionGain
                - mods.Serotonin * 0.15f; // Serotonin reduces precision slightly
            basePrecision = MathF.Max(0.1f, MathF.Min(1.5f, basePrecision));
            _globalPrecision = basePrecision;
            
            foreach (var state in _states.Values)
            {
                // Higher regions have slightly lower precision (more abstract/tolerant)
                float levelMod = 1.0f - state.HierarchyLevel * 0.08f;
                state.Precision = basePrecision * levelMod;
            }
            
            // 3) Compute prediction errors
            _errorBuffer.Clear();
            float totalError = 0f;
            
            foreach (var state in _states.Values)
            {
                float regionError = 0f;
                
                // For each region this one predicts...
                foreach (var kv in state.Predictions)
                {
                    byte targetRegion = kv.Key;
                    float predicted = kv.Value;
                    
                    // Get actual activity of target
                    float actual = 0f;
                    if (_states.TryGetValue(targetRegion, out var targetState))
                        actual = targetState.ActivityEma;
                    
                    // Compute precision-weighted error
                    float rawError = MathF.Abs(actual - predicted);
                    float weightedError = rawError * state.Precision;
                    
                    // Store error for target region (errors propagate UP)
                    _errorBuffer.TryGetValue(targetRegion, out float existingError);
                    _errorBuffer[targetRegion] = existingError + weightedError;
                    
                    regionError += weightedError;
                }
                
                state.TotalError = regionError;
                totalError += regionError;
            }
            
            _globalError = totalError / MathF.Max(1f, _states.Count);
            
            // 4) Update predictions (learning)
            float lr = opt.PredictionLearningRate * dt * 10f; // Scale by dt
            lr = MathF.Min(lr, 0.1f);
            
            foreach (var state in _states.Values)
            {
                var keys = state.GetPredictionKeys(); // Use cached keys
                foreach (var targetRegion in keys)
                {
                    float predicted = state.Predictions[targetRegion];
                    float actual = 0f;
                    if (_states.TryGetValue(targetRegion, out var targetState))
                        actual = targetState.ActivityEma;
                    
                    // Update prediction toward actual
                    float delta = (actual - predicted) * lr * state.Precision;
                    state.Predictions[targetRegion] = MathF.Max(0f, MathF.Min(1f, predicted + delta));
                }
                
                // Slow decay toward baseline
                float decay = opt.PredictionDecayRate * dt;
                foreach (var targetRegion in keys)
                {
                    float p = state.Predictions[targetRegion];
                    state.Predictions[targetRegion] = p + (0.5f - p) * decay;
                }
            }
            
            // 5) Build output
            var thresholdMods = new Dictionary<byte, float>();
            var surprised = new List<byte>();
            
            foreach (var kv in _errorBuffer)
            {
                // High error = lower threshold (easier to fire, pay attention)
                float mod = -kv.Value * opt.PredictionErrorThresholdGain;
                thresholdMods[kv.Key] = mod;
                
                // Track surprised regions
                if (kv.Value > 0.15f)
                    surprised.Add(kv.Key);
            }
            
            return new PredictiveCodingOutput(
                TotalError: totalError,
                ThresholdModByRegion: thresholdMods,
                SurprisedRegions: surprised.ToArray());
        }
    }
    
    /// <summary>Get prediction error threshold modifier for a region.</summary>
    public float GetThresholdMod(byte regionId)
    {
        lock (_gate)
        {
            _errorBuffer.TryGetValue(regionId, out float error);
            return -error * 0.06f; // Negative = easier to fire
        }
    }
    
    /// <summary>Get total error for a region (for monitoring).</summary>
    public float GetRegionError(byte regionId)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(regionId, out var state))
                return state.TotalError;
            return 0f;
        }
    }
    
    /// <summary>Get precision for a region.</summary>
    public float GetRegionPrecision(byte regionId)
    {
        lock (_gate)
        {
            if (_states.TryGetValue(regionId, out var state))
                return state.Precision;
            return 0.5f;
        }
    }
    
    /// <summary>Reset all predictions to baseline.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            foreach (var state in _states.Values)
            {
                state.ActivityEma = 0f;
                state.TotalError = 0f;
                state.Precision = 0.5f;
                
                var keys = state.GetPredictionKeys();
                foreach (var k in keys)
                    state.Predictions[k] = 0.5f;
            }
            
            _globalError = 0f;
            _globalPrecision = 0.5f;
            _errorBuffer.Clear();
        }
    }
    
    private sealed class RegionState
    {
        public byte RegionId;
        public int HierarchyLevel;
        public float Precision;
        public float ActivityEma;
        public float TotalError;
        public Dictionary<byte, float> Predictions { get; } = new();
        
        // Cached key array to avoid allocation during update loop
        private byte[]? _cachedKeys;
        private bool _keysDirty = true;
        
        public void MarkKeysDirty() => _keysDirty = true;
        
        public byte[] GetPredictionKeys()
        {
            if (_keysDirty || _cachedKeys == null || _cachedKeys.Length != Predictions.Count)
            {
                _cachedKeys = Predictions.Keys.ToArray();
                _keysDirty = false;
            }
            return _cachedKeys;
        }
    }
}

/// <summary>Snapshot for UI/monitoring.</summary>
public readonly record struct PredictiveCodingSnapshot(
    float GlobalError,
    float GlobalPrecision,
    IReadOnlyDictionary<byte, float> RegionErrors,
    IReadOnlyDictionary<byte, float> RegionPrecisions);

/// <summary>Output from predictive coding step.</summary>
public readonly record struct PredictiveCodingOutput(
    float TotalError,
    IReadOnlyDictionary<byte, float> ThresholdModByRegion,
    byte[] SurprisedRegions);
