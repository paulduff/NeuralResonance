namespace NRE.Core.Engine;

/// <summary>
/// Synaptic Tagging and Capture (STC) for Long-Term Potentiation.
/// 
/// Biology:
/// - Early-LTP (E-LTP): Short-lasting, protein synthesis independent (~1-3 hours)
/// - Late-LTP (L-LTP): Long-lasting, requires protein synthesis (hours to lifetime)
/// - Synaptic tag: Local marker set by strong activation
/// - Plasticity-related proteins (PRPs): Cell-wide resources captured by tagged synapses
/// 
/// The STC hypothesis explains how weak inputs can be consolidated if they
/// arrive near in time to strong inputs (behavioral tagging).
/// 
/// References:
/// - Frey & Morris 1997 (synaptic tagging)
/// - Redondo & Morris 2011 (STC review)
/// - Lisman et al. 2018 (memory allocation)
/// </summary>
public sealed class SynapticTagging
{
    private readonly object _gate = new();
    
    // Per-synapse tag state (keyed by synapse identity)
    private readonly Dictionary<SynapseKey, TagState> _tags = new();
    
    // Per-neuron PRP availability (keyed by voxel index)
    private readonly Dictionary<int, float> _prpAvailability = new();
    
    // Recently consolidated synapses (for UI monitoring)
    private readonly Queue<ConsolidationEvent> _recentConsolidations = new();
    private const int MaxRecentConsolidations = 100;
    
    // === PARAMETERS ===
    
    /// <summary>Minimum weight change to set a synaptic tag.</summary>
    public float TagThreshold { get; set; } = 0.02f;
    
    /// <summary>Strong activation threshold for PRP synthesis.</summary>
    public float PRPSynthesisThreshold { get; set; } = 0.15f;
    
    /// <summary>Tag decay half-life in seconds (~1-3 hours biologicaly, compressed for sim).</summary>
    public float TagHalfLifeSec { get; set; } = 120f; // 2 minutes for demo
    
    /// <summary>PRP decay half-life in seconds.</summary>
    public float PRPHalfLifeSec { get; set; } = 180f; // 3 minutes for demo
    
    /// <summary>Consolidation strength multiplier when tag captures PRP.</summary>
    public float ConsolidationBoost { get; set; } = 2.5f;
    
    /// <summary>Maximum weight boost from consolidation.</summary>
    public float MaxConsolidatedWeight { get; set; } = 0.8f;
    
    /// <summary>Consolidated synapses become more resistant to decay.</summary>
    public float ConsolidatedDecayResistance { get; set; } = 0.7f;
    
    // Sleep-dependent consolidation
    public float SleepConsolidationBoost { get; set; } = 1.5f;
    public float REMReplayBoost { get; set; } = 1.3f;
    
    private long _currentStep;
    private int _totalTags;
    private int _totalConsolidated;
    
    /// <summary>
    /// Set a synaptic tag after Hebbian update.
    /// Called when a synapse undergoes significant potentiation.
    /// </summary>
    public void SetTag(int preVoxel, int postVoxel, float weightChange, float currentWeight)
    {
        if (MathF.Abs(weightChange) < TagThreshold) return;
        
        lock (_gate)
        {
            var key = new SynapseKey(preVoxel, postVoxel);
            
            if (!_tags.TryGetValue(key, out var state))
            {
                state = new TagState();
                _tags[key] = state;
                _totalTags++;
            }
            
            // Set or strengthen tag
            state.TagStrength = MathF.Min(1f, state.TagStrength + MathF.Abs(weightChange) * 5f);
            state.TagSetStep = _currentStep;
            state.OriginalWeight = currentWeight - weightChange; // Weight before this change
            state.PotentiatedWeight = currentWeight;
            state.IsPositiveChange = weightChange > 0;
        }
    }
    
    /// <summary>
    /// Trigger PRP synthesis at a neuron after strong activation.
    /// Called when a neuron fires with high input or during replay.
    /// </summary>
    public void TriggerPRPSynthesis(int voxelIndex, float activationStrength)
    {
        if (activationStrength < PRPSynthesisThreshold) return;
        
        lock (_gate)
        {
            if (!_prpAvailability.TryGetValue(voxelIndex, out var current))
                current = 0f;
            
            // PRP synthesis is graded by activation strength
            float synthesis = (activationStrength - PRPSynthesisThreshold) * 2f;
            _prpAvailability[voxelIndex] = MathF.Min(1f, current + synthesis);
        }
    }
    
    /// <summary>
    /// Main update step. Handles tag decay, capture, and consolidation.
    /// Called on slow lane.
    /// </summary>
    public SynapticTaggingOutput Step(float dt, SleepPhase sleepPhase)
    {
        lock (_gate)
        {
            _currentStep++;
            
            float tagDecay = MathF.Pow(0.5f, dt / TagHalfLifeSec);
            float prpDecay = MathF.Pow(0.5f, dt / PRPHalfLifeSec);
            
            // Sleep modulates consolidation
            float sleepMod = sleepPhase switch
            {
                SleepPhase.Nrem => SleepConsolidationBoost,
                SleepPhase.Rem => REMReplayBoost,
                _ => 1.0f
            };
            
            var tagsToRemove = new List<SynapseKey>();
            var consolidations = new List<ConsolidationResult>();
            
            foreach (var kv in _tags)
            {
                var state = kv.Value;
                
                // Check for capture: does this synapse's postsynaptic neuron have PRPs?
                if (!state.IsConsolidated && _prpAvailability.TryGetValue(kv.Key.PostVoxel, out var prp) && prp > 0.1f)
                {
                    // Capture! Convert E-LTP to L-LTP
                    float captureStrength = MathF.Min(state.TagStrength, prp);
                    
                    if (captureStrength > 0.2f)
                    {
                        state.IsConsolidated = true;
                        state.ConsolidatedStrength = captureStrength * ConsolidationBoost * sleepMod;
                        
                        // Consume some PRP
                        _prpAvailability[kv.Key.PostVoxel] = prp - captureStrength * 0.3f;
                        
                        // Record consolidation
                        float weightBoost = state.IsPositiveChange 
                            ? state.ConsolidatedStrength * 0.1f
                            : -state.ConsolidatedStrength * 0.05f; // Depression consolidates too, but weaker
                        
                        consolidations.Add(new ConsolidationResult(
                            kv.Key.PreVoxel, 
                            kv.Key.PostVoxel, 
                            weightBoost,
                            ConsolidatedDecayResistance));
                        
                        // Record for monitoring
                        _recentConsolidations.Enqueue(new ConsolidationEvent(
                            _currentStep, kv.Key.PreVoxel, kv.Key.PostVoxel, 
                            weightBoost, state.ConsolidatedStrength));
                        
                        while (_recentConsolidations.Count > MaxRecentConsolidations)
                            _recentConsolidations.Dequeue();
                        
                        _totalConsolidated++;
                    }
                }
                
                // Decay tag strength
                if (!state.IsConsolidated)
                {
                    state.TagStrength *= tagDecay;
                    
                    if (state.TagStrength < 0.01f)
                        tagsToRemove.Add(kv.Key);
                }
                else
                {
                    // Consolidated tags decay very slowly
                    state.ConsolidatedStrength *= MathF.Pow(0.5f, dt / (TagHalfLifeSec * 10f));
                    
                    if (state.ConsolidatedStrength < 0.01f)
                        tagsToRemove.Add(kv.Key);
                }
            }
            
            // Remove expired tags
            foreach (var key in tagsToRemove)
            {
                _tags.Remove(key);
                _totalTags--;
            }
            
            // Decay PRP availability
            var prpKeys = _prpAvailability.Keys.ToList();
            foreach (var key in prpKeys)
            {
                _prpAvailability[key] *= prpDecay;
                if (_prpAvailability[key] < 0.01f)
                    _prpAvailability.Remove(key);
            }
            
            return new SynapticTaggingOutput(
                ActiveTags: _tags.Count,
                NeuronsWithPRP: _prpAvailability.Count,
                Consolidations: consolidations.ToArray());
        }
    }
    
    /// <summary>
    /// Check if a synapse has been consolidated (L-LTP).
    /// Used to modify decay resistance.
    /// </summary>
    public bool IsConsolidated(int preVoxel, int postVoxel)
    {
        lock (_gate)
        {
            var key = new SynapseKey(preVoxel, postVoxel);
            return _tags.TryGetValue(key, out var state) && state.IsConsolidated;
        }
    }
    
    /// <summary>
    /// Get decay resistance for a synapse (1.0 = normal, lower = more resistant).
    /// </summary>
    public float GetDecayResistance(int preVoxel, int postVoxel)
    {
        lock (_gate)
        {
            var key = new SynapseKey(preVoxel, postVoxel);
            if (_tags.TryGetValue(key, out var state) && state.IsConsolidated)
                return ConsolidatedDecayResistance * (1f - state.ConsolidatedStrength * 0.3f);
            return 1.0f;
        }
    }
    
    /// <summary>Get snapshot for monitoring.</summary>
    public SynapticTaggingSnapshot Snapshot()
    {
        lock (_gate)
        {
            int unconsolidated = 0;
            int consolidated = 0;
            float meanTagStrength = 0;
            float meanPRP = 0;
            
            foreach (var state in _tags.Values)
            {
                if (state.IsConsolidated)
                    consolidated++;
                else
                    unconsolidated++;
                meanTagStrength += state.TagStrength;
            }
            
            if (_tags.Count > 0)
                meanTagStrength /= _tags.Count;
            
            foreach (var prp in _prpAvailability.Values)
                meanPRP += prp;
            
            if (_prpAvailability.Count > 0)
                meanPRP /= _prpAvailability.Count;
            
            return new SynapticTaggingSnapshot(
                UnconsolidatedTags: unconsolidated,
                ConsolidatedTags: consolidated,
                NeuronsWithPRP: _prpAvailability.Count,
                MeanTagStrength: meanTagStrength,
                MeanPRPAvailability: meanPRP,
                TotalConsolidations: _totalConsolidated,
                RecentConsolidations: _recentConsolidations.ToArray());
        }
    }
    
    /// <summary>Reset all state.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            _tags.Clear();
            _prpAvailability.Clear();
            _recentConsolidations.Clear();
            _totalTags = 0;
            _totalConsolidated = 0;
            _currentStep = 0;
        }
    }
    
    // === INTERNAL TYPES ===
    
    private readonly record struct SynapseKey(int PreVoxel, int PostVoxel);
    
    private sealed class TagState
    {
        public float TagStrength;
        public long TagSetStep;
        public float OriginalWeight;
        public float PotentiatedWeight;
        public bool IsPositiveChange;
        public bool IsConsolidated;
        public float ConsolidatedStrength;
    }
}

/// <summary>Result of consolidation to apply to synapse.</summary>
public readonly record struct ConsolidationResult(
    int PreVoxel,
    int PostVoxel,
    float WeightBoost,
    float NewDecayResistance);

/// <summary>Per-step output from synaptic tagging system.</summary>
public readonly record struct SynapticTaggingOutput(
    int ActiveTags,
    int NeuronsWithPRP,
    ConsolidationResult[] Consolidations);

/// <summary>Event record for monitoring.</summary>
public readonly record struct ConsolidationEvent(
    long Step,
    int PreVoxel,
    int PostVoxel,
    float WeightChange,
    float Strength);

/// <summary>Snapshot for UI.</summary>
public readonly record struct SynapticTaggingSnapshot(
    int UnconsolidatedTags,
    int ConsolidatedTags,
    int NeuronsWithPRP,
    float MeanTagStrength,
    float MeanPRPAvailability,
    int TotalConsolidations,
    ConsolidationEvent[] RecentConsolidations);
