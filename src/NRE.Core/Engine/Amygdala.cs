namespace NRE.Core.Engine;

/// <summary>
/// Amygdala: Salience Detection with Biologically Accurate Nuclei
/// 
/// ANATOMICAL STRUCTURE (LeDoux 2000, Sah et al. 2003):
/// 
///   Sensory Input (thalamus/cortex)
///           ↓
///   ┌─────────────────────────────────────┐
///   │  LATERAL NUCLEUS (LA)               │
///   │  - Primary sensory input            │
///   │  - Fear conditioning site           │
///   │  - Plasticity for threat learning   │
///   └─────────────────────────────────────┘
///           ↓
///   ┌─────────────────────────────────────┐
///   │  BASAL NUCLEUS (B/BLA)              │
///   │  - Integration hub                  │
///   │  - Receives hippocampal context     │
///   │  - Bidirectional cortical links     │
///   └─────────────────────────────────────┘
///           ↓
///   ┌─────────────────────────────────────┐
///   │  INTERCALATED CELLS (ITC)           │
///   │  - GABAergic gate clusters          │
///   │  - Extinction learning              │
///   │  - PFC-mediated fear inhibition     │
///   └─────────────────────────────────────┘
///           ↓
///   ┌─────────────────────────────────────┐
///   │  CENTRAL NUCLEUS (CeA)              │
///   │  - CeL: Local inhibition            │
///   │  - CeM: OUTPUT to brainstem         │
///   │    → Autonomic responses            │
///   │    → Freezing, startle              │
///   │    → Noradrenaline release          │
///   └─────────────────────────────────────┘
/// </summary>
public sealed class Amygdala
{
    private readonly object _gate = new();
    
    // === NUCLEI ===
    private readonly LateralNucleus _la;
    private readonly BasalNucleus _basal;
    private readonly IntercalatedCells _itc;
    private readonly CentralNucleus _cea;
    
    // Legacy compatibility
    private readonly HashSet<int> _salientVoxels = new();
    private readonly Dictionary<byte, float> _regionSalience = new();
    
    // Noradrenaline pulse state
    private float _noradrenalinePulseRemaining;
    private float _currentPulseIntensity;
    private int _salientEventsThisSecond;
    private float _secondTimer;
    
    // Cached values
    private float _cachedThresholdAdjustment;
    private float _cachedSalience;
    
    public float PulseDurationSeconds { get; set; } = 0.25f;
    public float PulseIntensity { get; set; } = 0.15f;
    public float ThresholdLoweringFactor { get; set; } = 0.12f;
    
    public float CurrentSalience => _cachedSalience;
    public float GetThresholdAdjustment() => _cachedThresholdAdjustment;
    
    public Amygdala()
    {
        _la = new LateralNucleus(learningRate: 0.01f);
        _basal = new BasalNucleus();
        _itc = new IntercalatedCells();
        _cea = new CentralNucleus();
    }
    
    public AmygdalaState Snapshot()
    {
        lock (_gate)
        {
            return new AmygdalaState(
                SalientVoxelCount: _salientVoxels.Count,
                NoradrenalinePulseActive: _noradrenalinePulseRemaining > 0,
                PulseIntensity: _currentPulseIntensity,
                PulseRemainingMs: _noradrenalinePulseRemaining * 1000f,
                SalientEventsPerSecond: _salientEventsThisSecond,
                LAActivity: _la.GetActivity(),
                BasalActivity: _basal.GetActivity(),
                CeAOutput: _cea.GetOutput(),
                ITCGating: _itc.GetGatingLevel());
        }
    }
    
    public void MarkSalient(int voxelIndex, float salience = 1.0f)
    {
        lock (_gate)
        {
            if (salience > 0.1f) _salientVoxels.Add(voxelIndex);
            else _salientVoxels.Remove(voxelIndex);
        }
    }
    
    public void SetRegionSalience(byte regionId, float salience)
    {
        lock (_gate)
        {
            if (salience > 0.01f) _regionSalience[regionId] = Math.Clamp(salience, 0f, 1f);
            else _regionSalience.Remove(regionId);
        }
    }
    
    public float GetRegionSalience(byte regionId)
    {
        lock (_gate) return _regionSalience.TryGetValue(regionId, out var s) ? s : 0f;
    }
    
    /// <summary>
    /// Process spikes through amygdala circuit: LA→B→(ITC)→CeA
    /// </summary>
    public AmygdalaOutput Step(float dt, long stepIndex,
        IReadOnlyList<(byte hemi, int idx, byte region)> spikes,
        float hippocampalContext = 0f, float pfcInhibition = 0f)
    {
        lock (_gate)
        {
            // Update pulse timer
            if (_noradrenalinePulseRemaining > 0)
            {
                _noradrenalinePulseRemaining -= dt;
                if (_noradrenalinePulseRemaining <= 0)
                {
                    _noradrenalinePulseRemaining = 0;
                    _currentPulseIntensity = 0;
                }
            }
            
            _secondTimer += dt;
            if (_secondTimer >= 1.0f)
            {
                _secondTimer = 0f;
                _salientEventsThisSecond = 0;
            }
            
            // === 1) LATERAL NUCLEUS: Detect threat/salience ===
            float sensoryInput = 0f;
            bool foundSalient = false;
            float maxSalience = 0f;
            
            for (int i = 0; i < spikes.Count; i++)
            {
                var spike = spikes[i];
                
                if (_salientVoxels.Contains(spike.idx))
                {
                    foundSalient = true;
                    maxSalience = 1.0f;
                    sensoryInput += 1.0f;
                }
                
                if (_regionSalience.TryGetValue(spike.region, out var regionSal) && regionSal > 0.3f)
                {
                    foundSalient = true;
                    if (regionSal > maxSalience) maxSalience = regionSal;
                    sensoryInput += regionSal;
                }
                
                if (maxSalience >= 1.0f) break;
            }
            
            float laOutput = _la.Process(sensoryInput, dt);
            
            // === 2) BASAL NUCLEUS: Integrate with hippocampal context ===
            float basalOutput = _basal.Process(laOutput, hippocampalContext, dt);
            
            // === 3) INTERCALATED CELLS: Gate based on PFC extinction signal ===
            float itcGating = _itc.Process(basalOutput, pfcInhibition, dt);
            
            // === 4) CENTRAL NUCLEUS: Output to brainstem ===
            float gatedInput = basalOutput * (1.0f - itcGating);
            float ceaOutput = _cea.Process(gatedInput, dt);
            
            _cachedSalience = maxSalience;
            
            // Trigger noradrenaline pulse based on CeA output
            if (ceaOutput > 0.3f && _noradrenalinePulseRemaining <= 0)
            {
                _noradrenalinePulseRemaining = PulseDurationSeconds;
                _currentPulseIntensity = PulseIntensity * ceaOutput;
                _salientEventsThisSecond++;
            }
            
            _cachedThresholdAdjustment = ComputeThresholdAdjustment();
            
            return new AmygdalaOutput(
                ThresholdAdjustment: _cachedThresholdAdjustment,
                NoradrenalineLevel: _currentPulseIntensity,
                SalientEventDetected: foundSalient,
                Salience: maxSalience,
                LAActivity: laOutput,
                CeAOutput: ceaOutput);
        }
    }
    
    private float ComputeThresholdAdjustment()
    {
        if (_noradrenalinePulseRemaining <= 0) return 0f;
        float pulsePhase = 1.0f - (_noradrenalinePulseRemaining / PulseDurationSeconds);
        float envelope = pulsePhase < 0.1f ? pulsePhase * 10f : 1.0f - (pulsePhase - 0.1f) / 0.9f;
        return -_currentPulseIntensity * ThresholdLoweringFactor * envelope;
    }
    
    public void Reset()
    {
        lock (_gate)
        {
            _salientVoxels.Clear();
            _regionSalience.Clear();
            _noradrenalinePulseRemaining = 0;
            _currentPulseIntensity = 0;
            _cachedThresholdAdjustment = 0;
            _cachedSalience = 0;
            _la.Reset();
            _basal.Reset();
            _itc.Reset();
            _cea.Reset();
        }
    }
    
    // === INNER CLASSES: Amygdala Nuclei ===
    
    private sealed class LateralNucleus
    {
        private readonly float _learningRate;
        private float _activity;
        private readonly Dictionary<int, float> _threatWeights = new();
        
        public LateralNucleus(float learningRate) => _learningRate = learningRate;
        
        public float Process(float sensoryInput, float dt)
        {
            _activity = _activity * 0.9f + sensoryInput * 0.1f;
            return MathF.Tanh(_activity);
        }
        
        public float GetActivity() => _activity;
        public void Reset() { _activity = 0; _threatWeights.Clear(); }
    }
    
    private sealed class BasalNucleus
    {
        private float _activity;
        
        public float Process(float laInput, float hippocampalContext, float dt)
        {
            // Integrate LA input with hippocampal context
            float contextMod = 1.0f + hippocampalContext * 0.5f;
            _activity = _activity * 0.85f + laInput * contextMod * 0.15f;
            return MathF.Tanh(_activity);
        }
        
        public float GetActivity() => _activity;
        public void Reset() => _activity = 0;
    }
    
    private sealed class IntercalatedCells
    {
        private float _gatingLevel;
        
        public float Process(float basalInput, float pfcInhibition, float dt)
        {
            // PFC activates ITC → inhibits CeA (extinction)
            _gatingLevel = _gatingLevel * 0.9f + pfcInhibition * 0.1f;
            return Math.Clamp(_gatingLevel, 0f, 0.8f);
        }
        
        public float GetGatingLevel() => _gatingLevel;
        public void Reset() => _gatingLevel = 0;
    }
    
    private sealed class CentralNucleus
    {
        private float _output;
        
        public float Process(float gatedInput, float dt)
        {
            _output = _output * 0.8f + gatedInput * 0.2f;
            return MathF.Max(0f, _output);
        }
        
        public float GetOutput() => _output;
        public void Reset() => _output = 0;
    }
}

public readonly record struct AmygdalaState(
    int SalientVoxelCount, bool NoradrenalinePulseActive, float PulseIntensity,
    float PulseRemainingMs, int SalientEventsPerSecond,
    float LAActivity, float BasalActivity, float CeAOutput, float ITCGating);

public readonly record struct AmygdalaOutput(
    float ThresholdAdjustment, float NoradrenalineLevel, bool SalientEventDetected,
    float Salience, float LAActivity, float CeAOutput);
