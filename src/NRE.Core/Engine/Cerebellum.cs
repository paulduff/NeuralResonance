namespace NRE.Core.Engine;

/// <summary>
/// Cerebellum: Motor Coordination and Forward Model
/// 
/// ANATOMICAL STRUCTURE (Eccles, Ito & Szentágothai 1967):
/// 
/// CEREBELLAR CORTEX (3-layer structure):
/// ┌─────────────────────────────────────────────────┐
/// │ MOLECULAR LAYER                                  │
/// │   - Parallel fibers (granule cell axons)         │
/// │   - Purkinje cell dendrites                      │
/// │   - Stellate/Basket interneurons                 │
/// ├─────────────────────────────────────────────────┤
/// │ PURKINJE CELL LAYER (THE output neurons)         │
/// │   - Single row of large Purkinje cells           │
/// │   - GABAergic (inhibitory) → Deep Nuclei         │
/// │   - Receive ~200,000 parallel fiber inputs       │
/// ├─────────────────────────────────────────────────┤
/// │ GRANULAR LAYER                                   │
/// │   - Densest neurons in brain                     │
/// │   - Granule cells (excitatory)                   │
/// │   - Golgi cells (inhibitory)                     │
/// │   - Receives mossy fiber input                   │
/// └─────────────────────────────────────────────────┘
/// 
/// INPUT PATHWAYS:
/// - MOSSY FIBERS: From pons, spinal cord, vestibular → Granule layer
/// - CLIMBING FIBERS: From inferior olive → Purkinje cells (teaching signal)
/// 
/// DEEP CEREBELLAR NUCLEI (actual output):
/// - Dentate: Planning, timing (lateral)
/// - Interposed: Limb movements (intermediate) 
/// - Fastigial: Balance, posture (medial)
/// 
/// OUTPUT: Deep Nuclei → VL Thalamus → Motor Cortex
/// </summary>
public sealed class Cerebellum
{
    private readonly object _gate = new();
    
    // === CEREBELLAR CORTEX COMPONENTS ===
    
    /// <summary>Granular layer: mossy fiber processing</summary>
    private readonly GranularLayer _granular;
    
    /// <summary>Purkinje cell layer: main computational unit</summary>
    private readonly PurkinjeLayer _purkinje;
    
    /// <summary>Deep cerebellar nuclei: actual output pathway</summary>
    private readonly DeepNuclei _deepNuclei;
    
    // Per-region statistics (kept for backward compatibility)
    private readonly Dictionary<byte, RegionStats> _regionStats = new();
    
    // Reusable buffers
    private readonly Dictionary<byte, int> _regionCountsBuffer = new(32);
    private readonly Dictionary<byte, float> _regionInhibitionsBuffer = new(32);
    private readonly List<byte> _inactiveRegionsBuffer = new(32);
    
    // Configuration
    public float BaseInhibitionGain { get; set; } = 0.04f;
    public float VarianceThreshold { get; set; } = 0.15f;
    public float AdaptationRate { get; set; } = 0.02f;
    public float SmoothingDecay { get; set; } = 0.92f;
    
    // State
    private float _globalPredictionError;
    private float _smoothedActivityLevel;
    private float _globalInhibition;
    private float _climbingFiberSignal;
    private float _mossyFiberActivity;
    
    public float GetGlobalInhibition() { lock (_gate) return _globalInhibition; }
    public float GetGlobalPredictionError() { lock (_gate) return _globalPredictionError; }
    
    public Cerebellum()
    {
        _granular = new GranularLayer(expansionRatio: 4);
        _purkinje = new PurkinjeLayer(learningRate: 0.02f);
        _deepNuclei = new DeepNuclei();
    }
    
    public CerebellumState Snapshot()
    {
        lock (_gate)
        {
            return new CerebellumState(
                GlobalPredictionError: _globalPredictionError,
                SmoothedActivity: _smoothedActivityLevel,
                RegionCount: _regionStats.Count,
                ClimbingFiberSignal: _climbingFiberSignal,
                MossyFiberActivity: _mossyFiberActivity,
                PurkinjeOutput: _purkinje.GetAverageOutput(),
                DeepNucleiOutput: _deepNuclei.GetOutput());
        }
    }
    
    /// <summary>
    /// Process current activity through cerebellar circuit.
    /// 
    /// Biology:
    /// 1. Mossy fibers carry motor commands + sensory info → Granular layer
    /// 2. Granule cells expand representation → Parallel fibers
    /// 3. Parallel fibers contact Purkinje dendrites (learns timing/coordination)
    /// 4. Climbing fibers signal errors → teaches Purkinje cells
    /// 5. Purkinje cells INHIBIT deep nuclei
    /// 6. Deep nuclei output → VL thalamus → motor cortex
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
            // === 1) MOSSY FIBER INPUT: Motor commands + proprioception ===
            // Count spikes by region (represents motor/sensory input)
            _regionCountsBuffer.Clear();
            for (int i = 0; i < spikes.Count; i++)
            {
                var region = spikes[i].region;
                _regionCountsBuffer.TryGetValue(region, out var count);
                _regionCountsBuffer[region] = count + 1;
            }
            
            // Compute mossy fiber activity (from motor regions: 11, 12)
            float motorInput = 0f;
            if (_regionCountsBuffer.TryGetValue(11, out int motorCount))
                motorInput += motorCount;
            if (_regionCountsBuffer.TryGetValue(12, out int somatoCount))
                motorInput += somatoCount * 0.5f;
            
            _mossyFiberActivity = motorInput / Math.Max(1f, totalVoxels / 16f);
            
            // === 2) GRANULAR LAYER: Sparse expansion ===
            var granularOutput = _granular.Process(_mossyFiberActivity + motorCommandIntensity, dt);
            
            // === 3) CLIMBING FIBER INPUT: Error/teaching signal ===
            // Climbing fibers carry prediction errors from inferior olive
            _climbingFiberSignal = sensoryMismatch;
            
            // === 4) PURKINJE CELL LAYER: Learn and inhibit ===
            // Purkinje cells learn from climbing fiber signals (LTD at active parallel fiber synapses)
            float purkinjeOutput = _purkinje.Process(granularOutput, _climbingFiberSignal, dt);
            
            // === 5) DEEP NUCLEI: Tonic activity - Purkinje inhibition ===
            // Deep nuclei are tonically active; Purkinje inhibition modulates their output
            float deepNucleiOutput = _deepNuclei.Process(purkinjeOutput, dt);
            
            // === LEGACY: Region-based variance tracking (kept for compatibility) ===
            float totalInhibition = 0f;
            int inhibitedRegions = 0;
            
            foreach (var kv in _regionCountsBuffer)
            {
                byte region = kv.Key;
                int count = kv.Value;
                
                if (!_regionStats.TryGetValue(region, out var stats))
                {
                    stats = new RegionStats();
                    _regionStats[region] = stats;
                }
                
                float activity = count / (float)Math.Max(1, totalVoxels / 16);
                stats.SmoothedMean = stats.SmoothedMean * SmoothingDecay + activity * (1f - SmoothingDecay);
                
                float deviation = MathF.Abs(activity - stats.SmoothedMean);
                stats.Variance = stats.Variance * 0.95f + deviation * 0.05f;
                
                if (stats.Variance > VarianceThreshold)
                {
                    float inhibTarget = BaseInhibitionGain * (stats.Variance / VarianceThreshold);
                    stats.AdaptiveInhibition += (inhibTarget - stats.AdaptiveInhibition) * AdaptationRate;
                    stats.AdaptiveInhibition = Math.Clamp(stats.AdaptiveInhibition, 0f, 0.3f);
                    
                    totalInhibition += stats.AdaptiveInhibition;
                    inhibitedRegions++;
                }
                else
                {
                    stats.AdaptiveInhibition *= 0.98f;
                }
            }
            
            // Decay stats for inactive regions
            _inactiveRegionsBuffer.Clear();
            foreach (var region in _regionStats.Keys)
            {
                if (!_regionCountsBuffer.ContainsKey(region))
                    _inactiveRegionsBuffer.Add(region);
            }
            
            for (int i = 0; i < _inactiveRegionsBuffer.Count; i++)
            {
                var stats = _regionStats[_inactiveRegionsBuffer[i]];
                stats.SmoothedMean *= 0.99f;
                stats.AdaptiveInhibition *= 0.995f;
            }
            
            // Global activity metrics
            float globalActivity = spikes.Count / (float)Math.Max(1, totalVoxels);
            float prevSmoothed = _smoothedActivityLevel;
            _smoothedActivityLevel = _smoothedActivityLevel * SmoothingDecay + globalActivity * (1f - SmoothingDecay);
            _globalPredictionError = MathF.Abs(globalActivity - prevSmoothed);
            
            float avgInhibition = inhibitedRegions > 0 ? totalInhibition / inhibitedRegions : 0f;
            
            // Combine cerebellar circuit output with legacy variance-based inhibition
            _globalInhibition = avgInhibition + deepNucleiOutput * 0.1f;
            
            // Build region inhibitions
            _regionInhibitionsBuffer.Clear();
            foreach (var kv in _regionStats)
            {
                if (kv.Value.AdaptiveInhibition > 0.01f)
                    _regionInhibitionsBuffer[kv.Key] = kv.Value.AdaptiveInhibition;
            }
            
            return new CerebellumOutput(
                GlobalInhibitionBoost: _globalInhibition,
                PredictionError: _globalPredictionError,
                SmoothedActivity: _smoothedActivityLevel,
                RegionInhibitions: _regionInhibitionsBuffer,
                DeepNucleiOutput: deepNucleiOutput,
                PurkinjeInhibition: purkinjeOutput);
        }
    }
    
    /// <summary>Get the adaptive inhibition level for a specific region.</summary>
    public float GetRegionInhibition(byte regionId)
    {
        lock (_gate)
        {
            return _regionStats.TryGetValue(regionId, out var stats) 
                ? stats.AdaptiveInhibition 
                : 0f;
        }
    }
    
    /// <summary>Reset all learned statistics.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _regionStats.Clear();
            _globalPredictionError = 0f;
            _smoothedActivityLevel = 0f;
            _purkinje.Reset();
            _deepNuclei.Reset();
        }
    }
    
    // === INNER CLASSES: Cerebellar Circuit Components ===
    
    /// <summary>
    /// Granular Layer: Sparse expansion of mossy fiber input
    /// Biology: ~50 billion granule cells create massive parallel fiber representation
    /// </summary>
    private sealed class GranularLayer
    {
        private readonly int _expansionRatio;
        private float _activity;
        
        public GranularLayer(int expansionRatio)
        {
            _expansionRatio = expansionRatio;
        }
        
        /// <summary>
        /// Expand input through granule cells.
        /// Returns normalized parallel fiber activity.
        /// </summary>
        public float Process(float mossyFiberInput, float dt)
        {
            // Granule cells create sparse, expanded representation
            // Golgi cell feedback provides temporal dynamics
            float target = MathF.Tanh(mossyFiberInput * _expansionRatio);
            _activity = _activity * 0.8f + target * 0.2f; // Temporal smoothing
            return _activity;
        }
    }
    
    /// <summary>
    /// Purkinje Cell Layer: The computational core
    /// Biology: Large inhibitory neurons that integrate parallel fibers and learn from climbing fibers
    /// </summary>
    private sealed class PurkinjeLayer
    {
        private readonly float _learningRate;
        private float _parallelFiberWeight = 0.5f; // LTD/LTP adjustable
        private float _output;
        private float _simpleSpikerate; // Driven by parallel fibers
        private float _complexSpikeRate; // Driven by climbing fibers
        
        public PurkinjeLayer(float learningRate)
        {
            _learningRate = learningRate;
        }
        
        /// <summary>
        /// Process parallel fiber input with climbing fiber modulation.
        /// Returns inhibitory output to deep nuclei.
        /// </summary>
        public float Process(float parallelFiberInput, float climbingFiberSignal, float dt)
        {
            // Simple spikes: driven by parallel fiber input, weighted by learned strength
            _simpleSpikerate = parallelFiberInput * _parallelFiberWeight;
            
            // Complex spikes: driven by climbing fiber (rare, ~1Hz)
            _complexSpikeRate = climbingFiberSignal;
            
            // Long-term depression (LTD): climbing fiber + parallel fiber = weaken synapse
            // This is the core learning rule of the cerebellum
            if (_complexSpikeRate > 0.1f && parallelFiberInput > 0.2f)
            {
                // LTD: reduce weight when both are active (error signal)
                _parallelFiberWeight -= _learningRate * _complexSpikeRate * parallelFiberInput * dt;
            }
            else if (parallelFiberInput > 0.2f)
            {
                // Slow LTP: strengthen active synapses when no error
                _parallelFiberWeight += _learningRate * 0.1f * parallelFiberInput * dt;
            }
            
            _parallelFiberWeight = Math.Clamp(_parallelFiberWeight, 0.1f, 0.9f);
            
            // Purkinje output (GABAergic = inhibitory)
            _output = _simpleSpikerate + _complexSpikeRate * 0.5f;
            return _output;
        }
        
        public float GetAverageOutput() => _output;
        
        public void Reset()
        {
            _parallelFiberWeight = 0.5f;
            _output = 0f;
        }
    }
    
    /// <summary>
    /// Deep Cerebellar Nuclei: Output pathway
    /// Biology: Dentate, Interposed, Fastigial nuclei are tonically active.
    /// Purkinje inhibition shapes their output → VL thalamus → motor cortex.
    /// </summary>
    private sealed class DeepNuclei
    {
        private float _tonicActivity = 0.6f; // Baseline tonic firing
        private float _output;
        
        // Separate nuclei outputs
        private float _dentateOutput;     // Planning/timing
        private float _interposedOutput;  // Limb movements
        private float _fastigialOutput;   // Balance/posture
        
        /// <summary>
        /// Compute output based on Purkinje inhibition.
        /// Higher Purkinje activity → lower deep nuclei output.
        /// </summary>
        public float Process(float purkinjeInhibition, float dt)
        {
            // Purkinje cells inhibit deep nuclei
            // Less inhibition = more output = more motor drive
            float inhibitionStrength = purkinjeInhibition * 0.8f;
            
            // Deep nuclei output = tonic - inhibition
            _output = MathF.Max(0f, _tonicActivity - inhibitionStrength);
            
            // Distribute across nuclei (simplified)
            _dentateOutput = _output * 0.4f;     // Lateral - planning
            _interposedOutput = _output * 0.35f; // Intermediate - limbs
            _fastigialOutput = _output * 0.25f;  // Medial - posture
            
            return _output;
        }
        
        public float GetOutput() => _output;
        public float GetDentateOutput() => _dentateOutput;
        public float GetInterposedOutput() => _interposedOutput;
        public float GetFastigialOutput() => _fastigialOutput;
        
        public void Reset()
        {
            _output = 0f;
            _dentateOutput = 0f;
            _interposedOutput = 0f;
            _fastigialOutput = 0f;
        }
    }
    
    private sealed class RegionStats
    {
        public float SmoothedMean;
        public float Variance;
        public float AdaptiveInhibition;
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
