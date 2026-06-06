namespace NRE.Core.Engine;

/// <summary>
/// Systems Consolidation: Sleep-Dependent Memory Transfer from Hippocampus to Cortex
/// 
/// BIOLOGICAL BASIS (McClelland et al. 1995, Frankland & Bontempi 2005, Diekelmann & Born 2010):
/// 
/// The Standard Model of Memory Consolidation:
/// 
/// 1. ENCODING: Hippocampus rapidly encodes episodic memories during waking.
///    These are initially hippocampus-dependent.
///    
/// 2. REPLAY: During NREM sleep, hippocampus replays recent experiences.
///    Sharp-wave ripples (150-200Hz) drive reactivation.
///    
/// 3. SLEEP TRIPLET: Optimal consolidation requires coordination of:
///    - Hippocampal sharp-wave ripples (SWR)
///    - Thalamic sleep spindles (12-14Hz)
///    - Cortical slow oscillations (0.5-1Hz)
///    
///    Timing: SWR during UP state of slow oscillation, 
///            followed by spindle → optimal cortical plasticity
///    
/// 4. CORTICAL LEARNING: Slow Hebbian learning in cortex during coordinated replay.
///    Gradual extraction of regularities across episodes.
///    
/// 5. HIPPOCAMPAL FORGETTING: As cortical representation strengthens,
///    hippocampal trace can decay (memory becomes hippocampus-independent).
/// 
/// Folded Archive Entry 028: P0 Implementation
/// </summary>
public sealed class SystemsConsolidation
{
    private readonly object _gate = new();
    
    // === SLEEP OSCILLATION STATE ===
    private SlowOscillationPhase _slowOscPhase = SlowOscillationPhase.Down;
    private float _slowOscTimer;
    private float _slowOscPeriod = 1.0f; // ~1Hz slow oscillation
    
    private bool _spindleActive;
    private float _spindleTimer;
    private float _spindleDuration = 0.25f; // 500ms spindle bursts
    
    private bool _rippleActive;
    private float _rippleTimer;
    
    // === CONSOLIDATION STATE ===
    private readonly List<ConsolidationTrace> _traces = new();
    private readonly int _maxTraces;
    
    // Episodes queued for consolidation (from hippocampus)
    private readonly Queue<EpisodeForConsolidation> _consolidationQueue = new();
    private const int MaxQueueSize = 32;
    
    // === CONFIGURATION ===
    private readonly float _corticalLearningRate;
    private readonly float _hippocampalDecayRate;
    private readonly float _consolidationThreshold;
    private readonly int _replaysForConsolidation;
    private readonly float _tripletBonus; // Bonus when all three oscillations align
    
    // === METRICS ===
    private int _totalReplays;
    private int _totalConsolidations;
    private int _totalTransfers; // Memories that became hippocampus-independent
    private float _currentConsolidationStrength;
    private int _tripletAlignments;
    
    public SystemsConsolidation(
        int maxTraces = 256,
        float corticalLearningRate = 0.01f,
        float hippocampalDecayRate = 0.001f,
        float consolidationThreshold = 0.7f,
        int replaysForConsolidation = 5,
        float tripletBonus = 2.0f)
    {
        _maxTraces = maxTraces;
        _corticalLearningRate = corticalLearningRate;
        _hippocampalDecayRate = hippocampalDecayRate;
        _consolidationThreshold = consolidationThreshold;
        _replaysForConsolidation = replaysForConsolidation;
        _tripletBonus = tripletBonus;
    }
    
    /// <summary>Get current state snapshot for monitoring.</summary>
    public SystemsConsolidationState Snapshot()
    {
        lock (_gate)
        {
            int hippoDependent = 0;
            int corticalOnly = 0;
            int transitional = 0;
            
            foreach (var trace in _traces)
            {
                if (trace.CorticalStrength >= _consolidationThreshold)
                    corticalOnly++;
                else if (trace.CorticalStrength < 0.2f)
                    hippoDependent++;
                else
                    transitional++;
            }
            
            return new SystemsConsolidationState(
                SlowOscPhase: _slowOscPhase.ToString(),
                SpindleActive: _spindleActive,
                RippleActive: _rippleActive,
                TotalTraces: _traces.Count,
                HippocampusDependent: hippoDependent,
                CorticalOnly: corticalOnly,
                Transitional: transitional,
                TotalReplays: _totalReplays,
                TotalConsolidations: _totalConsolidations,
                TotalTransfers: _totalTransfers,
                TripletAlignments: _tripletAlignments,
                ConsolidationQueueSize: _consolidationQueue.Count,
                CurrentConsolidationStrength: _currentConsolidationStrength);
        }
    }
    
    /// <summary>
    /// Main update step during sleep.
    /// Orchestrates slow oscillation, spindles, and ripples.
    /// </summary>
    /// <param name="dt">Time delta</param>
    /// <param name="sleepPhase">Current sleep phase from SleepController</param>
    /// <param name="hippocampus">Reference to hippocampus for replay</param>
    /// <param name="thalamus">Reference to thalamus for spindle coordination</param>
    public ConsolidationOutput Step(
        float dt,
        SleepPhase sleepPhase,
        Hippocampus? hippocampus,
        Thalamus? thalamus)
    {
        lock (_gate)
        {
            bool replayTriggered = false;
            float consolidationAmount = 0f;
            
            // Only consolidate during NREM sleep
            if (sleepPhase != SleepPhase.Nrem)
            {
                _currentConsolidationStrength = 0f;
                return new ConsolidationOutput(
                    ReplayTriggered: false,
                    ConsolidationAmount: 0f,
                    TripletAligned: false,
                    SlowOscPhase: _slowOscPhase);
            }
            
            // === 1) SLOW OSCILLATION DYNAMICS ===
            _slowOscTimer += dt;
            
            bool wasDown = _slowOscPhase == SlowOscillationPhase.Down;
            
            if (_slowOscTimer >= _slowOscPeriod / 2)
            {
                // Toggle phase
                _slowOscTimer = 0f;
                _slowOscPhase = _slowOscPhase == SlowOscillationPhase.Down 
                    ? SlowOscillationPhase.Up 
                    : SlowOscillationPhase.Down;
            }
            
            // === 2) SPINDLE DYNAMICS (coordinate with thalamus) ===
            bool thalamicSpindle = thalamus?.Snapshot().SpindleActive ?? false;
            
            if (thalamicSpindle && !_spindleActive)
            {
                // Spindle onset - best during DOWN→UP transition
                _spindleActive = true;
                _spindleTimer = 0f;
            }
            
            if (_spindleActive)
            {
                _spindleTimer += dt;
                if (_spindleTimer >= _spindleDuration)
                {
                    _spindleActive = false;
                }
            }
            
            // === 3) SHARP-WAVE RIPPLE TRIGGERING ===
            // Ripples occur during UP state of slow oscillation
            bool canRipple = _slowOscPhase == SlowOscillationPhase.Up && !_rippleActive;
            
            if (canRipple && _consolidationQueue.Count > 0)
            {
                // Trigger ripple for replay
                _rippleActive = true;
                _rippleTimer = 0f;
                replayTriggered = true;
                _totalReplays++;
            }
            
            if (_rippleActive)
            {
                _rippleTimer += dt;
                if (_rippleTimer >= 0.1f) // ~100ms ripple
                {
                    _rippleActive = false;
                }
            }
            
            // === 4) SLEEP TRIPLET DETECTION ===
            // Optimal window: UP state + spindle + ripple
            bool tripletAligned = _slowOscPhase == SlowOscillationPhase.Up 
                               && _spindleActive 
                               && _rippleActive;
            
            if (tripletAligned)
            {
                _tripletAlignments++;
            }
            
            // === 5) CONSOLIDATION LEARNING ===
            if (replayTriggered && _consolidationQueue.Count > 0)
            {
                var episode = _consolidationQueue.Dequeue();
                
                // Learning rate modulated by triplet alignment
                float lr = _corticalLearningRate;
                if (tripletAligned) lr *= _tripletBonus;
                
                consolidationAmount = ProcessConsolidation(episode, lr);
                _currentConsolidationStrength = consolidationAmount;
            }
            else
            {
                _currentConsolidationStrength *= 0.95f;
            }
            
            // === 6) DECAY OLD TRACES ===
            DecayTraces(dt);
            
            return new ConsolidationOutput(
                ReplayTriggered: replayTriggered,
                ConsolidationAmount: consolidationAmount,
                TripletAligned: tripletAligned,
                SlowOscPhase: _slowOscPhase);
        }
    }
    
    /// <summary>
    /// Queue an episode from hippocampus for consolidation.
    /// Called when hippocampus captures a salient episode.
    /// </summary>
    public void QueueForConsolidation(int episodeId, float[] pattern, float salience, string context = "")
    {
        lock (_gate)
        {
            if (_consolidationQueue.Count >= MaxQueueSize)
            {
                _consolidationQueue.Dequeue(); // Drop oldest
            }
            
            _consolidationQueue.Enqueue(new EpisodeForConsolidation(
                EpisodeId: episodeId,
                Pattern: (float[])pattern.Clone(),
                Salience: salience,
                Context: context,
                QueuedAt: DateTime.UtcNow));
        }
    }
    
    /// <summary>
    /// Query if a pattern has been consolidated to cortex.
    /// Returns cortical strength (0 = hippocampus-dependent, 1 = cortical-only).
    /// </summary>
    public float QueryCorticalStrength(float[] pattern, float matchThreshold = 0.7f)
    {
        lock (_gate)
        {
            float bestStrength = 0f;
            
            foreach (var trace in _traces)
            {
                float similarity = ComputeSimilarity(pattern, trace.Pattern);
                if (similarity >= matchThreshold)
                {
                    bestStrength = MathF.Max(bestStrength, trace.CorticalStrength);
                }
            }
            
            return bestStrength;
        }
    }
    
    /// <summary>
    /// Check if a memory can be retrieved without hippocampus.
    /// </summary>
    public bool IsHippocampusIndependent(int episodeId)
    {
        lock (_gate)
        {
            foreach (var trace in _traces)
            {
                if (trace.OriginalEpisodeId == episodeId)
                {
                    return trace.CorticalStrength >= _consolidationThreshold;
                }
            }
            return false;
        }
    }
    
    /// <summary>
    /// Get consolidated pattern for an episode (cortical retrieval).
    /// </summary>
    public float[]? RetrieveFromCortex(int episodeId)
    {
        lock (_gate)
        {
            foreach (var trace in _traces)
            {
                if (trace.OriginalEpisodeId == episodeId && trace.CorticalStrength >= 0.3f)
                {
                    return (float[])trace.Pattern.Clone();
                }
            }
            return null;
        }
    }
    
    /// <summary>
    /// Reset all consolidated traces.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _traces.Clear();
            _consolidationQueue.Clear();
            _slowOscPhase = SlowOscillationPhase.Down;
            _slowOscTimer = 0f;
            _spindleActive = false;
            _rippleActive = false;
            _totalReplays = 0;
            _totalConsolidations = 0;
            _totalTransfers = 0;
            _tripletAlignments = 0;
            _currentConsolidationStrength = 0f;
        }
    }
    
    // ==================== PRIVATE METHODS ====================
    
    private float ProcessConsolidation(EpisodeForConsolidation episode, float learningRate)
    {
        // Find existing trace or create new one
        ConsolidationTrace? existingTrace = null;
        int existingIndex = -1;
        float bestSimilarity = 0.6f; // Threshold for "same memory"
        
        for (int i = 0; i < _traces.Count; i++)
        {
            float sim = ComputeSimilarity(episode.Pattern, _traces[i].Pattern);
            if (sim > bestSimilarity)
            {
                bestSimilarity = sim;
                existingTrace = _traces[i];
                existingIndex = i;
            }
        }
        
        if (existingTrace != null)
        {
            // Update existing trace
            existingTrace.ReplayCount++;
            existingTrace.LastReplayAt = DateTime.UtcNow;
            
            // Strengthen cortical representation
            float boost = learningRate * episode.Salience;
            existingTrace.CorticalStrength += boost * (1f - existingTrace.CorticalStrength);
            
            // Blend patterns (cortical generalization)
            for (int i = 0; i < existingTrace.Pattern.Length && i < episode.Pattern.Length; i++)
            {
                existingTrace.Pattern[i] = existingTrace.Pattern[i] * 0.9f + episode.Pattern[i] * 0.1f;
            }
            
            // Check for full consolidation
            if (existingTrace.ReplayCount >= _replaysForConsolidation 
                && existingTrace.CorticalStrength >= _consolidationThreshold)
            {
                if (!existingTrace.FullyConsolidated)
                {
                    existingTrace.FullyConsolidated = true;
                    _totalConsolidations++;
                }
            }
            
            // Check for hippocampus independence
            if (existingTrace.CorticalStrength >= 0.9f && !existingTrace.HippocampusIndependent)
            {
                existingTrace.HippocampusIndependent = true;
                _totalTransfers++;
            }
            
            return boost;
        }
        else
        {
            // Create new trace
            var newTrace = new ConsolidationTrace
            {
                OriginalEpisodeId = episode.EpisodeId,
                Pattern = (float[])episode.Pattern.Clone(),
                Salience = episode.Salience,
                Context = episode.Context,
                CreatedAt = DateTime.UtcNow,
                CorticalStrength = learningRate * episode.Salience,
                ReplayCount = 1
            };
            
            _traces.Add(newTrace);
            
            // Prune if over limit
            while (_traces.Count > _maxTraces)
            {
                // Remove weakest non-consolidated trace
                int weakestIdx = -1;
                float weakestStrength = float.MaxValue;
                
                for (int i = 0; i < _traces.Count; i++)
                {
                    if (!_traces[i].FullyConsolidated && _traces[i].CorticalStrength < weakestStrength)
                    {
                        weakestStrength = _traces[i].CorticalStrength;
                        weakestIdx = i;
                    }
                }
                
                if (weakestIdx >= 0)
                    _traces.RemoveAt(weakestIdx);
                else
                    break;
            }
            
            return learningRate * episode.Salience;
        }
    }
    
    private void DecayTraces(float dt)
    {
        float decayAmount = _hippocampalDecayRate * dt;
        
        for (int i = _traces.Count - 1; i >= 0; i--)
        {
            var trace = _traces[i];
            
            // Only decay non-consolidated traces
            if (!trace.FullyConsolidated)
            {
                // Decay toward hippocampus-dependent state
                trace.CorticalStrength -= decayAmount;
                
                if (trace.CorticalStrength <= 0f)
                {
                    // Trace lost - would need hippocampus to retrieve
                    _traces.RemoveAt(i);
                }
            }
        }
    }
    
    private static float ComputeSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length || a.Length == 0) return 0f;
        
        float dot = 0f;
        float normA = 0f;
        float normB = 0f;
        
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        
        normA = MathF.Sqrt(normA);
        normB = MathF.Sqrt(normB);
        
        if (normA < 0.001f || normB < 0.001f) return 0f;
        
        return dot / (normA * normB);
    }
    
    // ==================== INTERNAL CLASSES ====================
    
    private sealed record EpisodeForConsolidation(
        int EpisodeId,
        float[] Pattern,
        float Salience,
        string Context,
        DateTime QueuedAt);
    
    private sealed class ConsolidationTrace
    {
        public int OriginalEpisodeId { get; init; }
        public float[] Pattern { get; init; } = Array.Empty<float>();
        public float Salience { get; init; }
        public string Context { get; init; } = "";
        public DateTime CreatedAt { get; init; }
        public DateTime LastReplayAt { get; set; }
        
        public float CorticalStrength { get; set; }
        public int ReplayCount { get; set; }
        public bool FullyConsolidated { get; set; }
        public bool HippocampusIndependent { get; set; }
    }
}

// ==================== PUBLIC DTOs ====================

/// <summary>State snapshot for monitoring.</summary>
public readonly record struct SystemsConsolidationState(
    string SlowOscPhase,
    bool SpindleActive,
    bool RippleActive,
    int TotalTraces,
    int HippocampusDependent,
    int CorticalOnly,
    int Transitional,
    int TotalReplays,
    int TotalConsolidations,
    int TotalTransfers,
    int TripletAlignments,
    int ConsolidationQueueSize,
    float CurrentConsolidationStrength);

/// <summary>Output from consolidation step.</summary>
public readonly record struct ConsolidationOutput(
    bool ReplayTriggered,
    float ConsolidationAmount,
    bool TripletAligned,
    SlowOscillationPhase SlowOscPhase);

/// <summary>Slow oscillation phase for external systems.</summary>
public enum SlowOscillationPhase { Up, Down }
