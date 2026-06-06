namespace NRE.Core.Engine;

/// <summary>
/// Sleep Controller: REM/Wake State Management (spec.md Section II.2.A - Pons)
/// 
/// OPTIMIZED: Caches phase-dependent values, reduces lock contention.
/// </summary>
public sealed class SleepController
{
    private readonly object _gate = new();
    
    private SleepPhase _currentPhase = SleepPhase.Awake;
    private float _phaseTimer;
    private float _sleepPressure;

    // Hysteresis / dwell timers so we don't get stuck asleep or bounce immediately back to sleep.
    private float _awakeDwellTimer;
    private float _sleepEpisodeTimer;
    
    // Cached phase-dependent values (updated on phase transition)
    private float _cachedThresholdMod;
    private float _cachedLeakMod;
    private bool _cachedSensorsConnected = true;
    private bool _cachedIsDreaming;
    
    // === Sleep onset thresholds ===
    /// <summary>ATP below this triggers sleep onset (if awake long enough).</summary>
    public float SleepTriggerAtp { get; set; } = 0.40f;
    
    /// <summary>Sleep pressure above this triggers sleep onset.</summary>
    public float SleepTriggerPressure { get; set; } = 0.50f;

    // === Wake thresholds ===
    /// <summary>ATP above this allows waking (if pressure is also low enough).</summary>
    public float WakeTriggerAtp { get; set; } = 0.70f;
    
    /// <summary>Sleep pressure below this triggers wake (primary wake condition).</summary>
    public float WakeTriggerPressure { get; set; } = 0.08f;

    // === Timing constraints ===
    /// <summary>Minimum awake time before sleep can re-trigger.</summary>
    public float MinAwakeDwellSeconds { get; set; } = 1.5f;
    
    /// <summary>Minimum sleep time before wake can trigger (prevents micro-sleeps).</summary>
    public float MinSleepDwellSeconds { get; set; } = 3.0f;  // Must complete at least one full NREM+REM

    /// <summary>Maximum continuous sleep episode (hard cap).</summary>
    public float MaxSleepEpisodeSeconds { get; set; } = 15.0f;
    
    // === Pressure dynamics (tuned for visible demo cycles) ===
    /// <summary>Sleep pressure accumulation rate while awake.</summary>
    public float SleepPressureRate { get; set; } = 0.025f;  // Reach 0.50 in ~20 seconds
    
    /// <summary>Sleep pressure recovery rate while asleep.</summary>
    public float SleepRecoveryRate { get; set; } = 0.012f;  // Slow recovery to allow full cycles
    
    // === Cycle timing (demo-friendly: faster cycles) ===
    public float RemCycleDurationSeconds { get; set; } = 2.5f;  // Total cycle time
    public float NremToRemRatio { get; set; } = 1.5f;  // 3s NREM, 2s REM
    
    public float DreamNoiseIntensity { get; set; } = 0.08f;
    public float ReplayProbability { get; set; } = 0.15f;
    
    private int _consolidationCycles;
    private int _replaysTriggered;
    
    // Lock-free reads for frequently accessed state
    public SleepPhase CurrentPhase => _currentPhase;
    public bool SensorsConnected => _cachedSensorsConnected;
    public bool IsDreaming => _cachedIsDreaming;
    
    public void IncrementReplays() { lock (_gate) _replaysTriggered++; }
    
    public SleepState Snapshot()
    {
        lock (_gate)
        {
            return new SleepState(
                Phase: _currentPhase,
                PhaseTimerSeconds: _phaseTimer,
                SleepPressure: _sleepPressure,
                ConsolidationCycles: _consolidationCycles,
                ReplaysTriggered: _replaysTriggered,
                IsDreaming: _cachedIsDreaming,
                SensorsConnected: _cachedSensorsConnected);
        }
    }
    
    /// <summary>
    /// Update sleep state based on current ATP levels.
    /// </summary>
    public SleepOutput Step(float dt, float globalAtp01, float spikeActivity01)
    {
        lock (_gate)
        {
            _phaseTimer += dt;

            // Track awake/asleep dwell times
            if (_currentPhase == SleepPhase.Awake)
            {
                _awakeDwellTimer += dt;
                _sleepEpisodeTimer = 0f;
            }
            else
            {
                _sleepEpisodeTimer += dt;
                _awakeDwellTimer = 0f;
            }
            
            // Update sleep pressure
            if (_currentPhase == SleepPhase.Awake)
            {
                // Pressure accumulates while awake (faster when active)
                _sleepPressure += SleepPressureRate * dt * (1f + spikeActivity01);
                _sleepPressure = MathF.Min(1f, _sleepPressure);
            }
            else
            {
                // Pressure recovers while asleep
                _sleepPressure -= SleepRecoveryRate * dt;
                _sleepPressure = MathF.Max(0f, _sleepPressure);
            }
            
            var previousPhase = _currentPhase;
            
            // === STATE MACHINE ===
            // Key insight: During sleep, phase transitions (NREM→REM→NREM) take priority.
            // Wake only happens when pressure is truly depleted OR we've been asleep too long.
            switch (_currentPhase)
            {
                case SleepPhase.Awake:
                    // Sleep onset: requires dwell time AND (low ATP OR high pressure)
                    if (_awakeDwellTimer >= MinAwakeDwellSeconds)
                    {
                        if (globalAtp01 < SleepTriggerAtp || _sleepPressure > SleepTriggerPressure)
                            TransitionTo(SleepPhase.Nrem);
                    }
                    break;
                    
                case SleepPhase.Nrem:
                    {
                        // First check: have we completed this NREM phase?
                        float nremDuration = RemCycleDurationSeconds * NremToRemRatio / (1f + NremToRemRatio);
                        if (_phaseTimer >= nremDuration)
                        {
                            // NREM complete → transition to REM (don't wake yet!)
                            TransitionTo(SleepPhase.Rem);
                        }
                        else
                        {
                            // Only check for wake if we've slept minimum time AND pressure is very low
                            bool canWake = _sleepEpisodeTimer >= MinSleepDwellSeconds;
                            bool shouldWake = _sleepPressure <= WakeTriggerPressure;
                            
                            if (canWake && shouldWake)
                                TransitionTo(SleepPhase.Awake);
                        }
                    }
                    break;
                    
                case SleepPhase.Rem:
                    {
                        float remDuration = RemCycleDurationSeconds / (1f + NremToRemRatio);
                        if (_phaseTimer >= remDuration)
                        {
                            _consolidationCycles++;
                            
                            // After REM, decide: continue sleeping or wake?
                            bool pressureDepleted = _sleepPressure <= WakeTriggerPressure;
                            bool atpRecovered = globalAtp01 > WakeTriggerAtp && _sleepPressure < 0.25f;
                            
                            if (pressureDepleted || atpRecovered)
                                TransitionTo(SleepPhase.Awake);
                            else
                                TransitionTo(SleepPhase.Nrem); // Continue sleep cycling
                        }
                    }
                    break;
            }

            // Hard cap: force wake after max sleep duration
            if (_currentPhase != SleepPhase.Awake && _sleepEpisodeTimer >= MaxSleepEpisodeSeconds)
                TransitionTo(SleepPhase.Awake);
            
            // Dream replay logic
            bool shouldReplay = _cachedIsDreaming && 
                               Random.Shared.NextDouble() < ReplayProbability * dt;
            
            if (shouldReplay)
                _replaysTriggered++;
            
            return new SleepOutput(
                Phase: _currentPhase,
                SensorsEnabled: _cachedSensorsConnected,
                DreamNoiseLevel: _cachedIsDreaming ? DreamNoiseIntensity : 0f,
                TriggerReplay: shouldReplay,
                ThresholdModifier: _cachedThresholdMod,
                LeakModifier: _cachedLeakMod,
                PhaseChanged: _currentPhase != previousPhase);
        }
    }
    
    /// <summary>Force a specific sleep phase (for testing/UI).</summary>
    public void ForcePhase(SleepPhase phase)
    {
        lock (_gate)
        {
            TransitionTo(phase);

            // Set appropriate pressure and timers for the forced phase
            switch (phase)
            {
                case SleepPhase.Awake:
                    // If forcing awake, start dwell timer and reset sleep timers
                    _awakeDwellTimer = MathF.Max(_awakeDwellTimer, MinAwakeDwellSeconds);
                    _sleepEpisodeTimer = 0f;
                    // Keep pressure as-is (user might be testing wake from various pressure levels)
                    break;
                    
                case SleepPhase.Nrem:
                    // Set pressure high enough to complete a full sleep cycle without waking
                    _sleepPressure = MathF.Max(_sleepPressure, 0.45f);
                    _sleepEpisodeTimer = 0.1f;  // Just started sleeping
                    _awakeDwellTimer = 0f;
                    break;
                    
                case SleepPhase.Rem:
                    // Set pressure high enough to stay in REM for full duration
                    _sleepPressure = MathF.Max(_sleepPressure, 0.35f);
                    _sleepEpisodeTimer = 3.5f;  // Just completed NREM
                    _awakeDwellTimer = 0f;
                    break;
            }
        }
    }
    
    /// <summary>Reset sleep statistics.</summary>
    public void Reset()
    {
        lock (_gate)
        {
            TransitionTo(SleepPhase.Awake);
            _sleepPressure = 0f;
            _awakeDwellTimer = 0f;
            _sleepEpisodeTimer = 0f;
            _consolidationCycles = 0;
            _replaysTriggered = 0;
        }
    }
    
    /// <summary>Artificially build sleep pressure (for testing/demo).</summary>
    public void BuildPressure(float amount = 0.3f)
    {
        lock (_gate)
        {
            _sleepPressure = MathF.Min(1f, _sleepPressure + amount);
            // Also ensure awake dwell timer is satisfied
            _awakeDwellTimer = MathF.Max(_awakeDwellTimer, MinAwakeDwellSeconds);
        }
    }
    
    /// <summary>Get current sleep pressure (for UI display).</summary>
    public float GetPressure()
    {
        lock (_gate) return _sleepPressure;
    }
    
    private void TransitionTo(SleepPhase newPhase)
    {
        _currentPhase = newPhase;
        _phaseTimer = 0f;
        
        // Update cached values
        _cachedSensorsConnected = newPhase == SleepPhase.Awake;
        _cachedIsDreaming = newPhase == SleepPhase.Rem;
        
        (_cachedThresholdMod, _cachedLeakMod) = newPhase switch
        {
            SleepPhase.Awake => (0f, 0f),
            SleepPhase.Nrem => (0.15f, 0.03f),
            SleepPhase.Rem => (-0.05f, -0.01f),
            _ => (0f, 0f)
        };
    }
}

public enum SleepPhase
{
    Awake,
    Nrem,
    Rem
}

public readonly record struct SleepState(
    SleepPhase Phase,
    float PhaseTimerSeconds,
    float SleepPressure,
    int ConsolidationCycles,
    int ReplaysTriggered,
    bool IsDreaming,
    bool SensorsConnected);

public readonly record struct SleepOutput(
    SleepPhase Phase,
    bool SensorsEnabled,
    float DreamNoiseLevel,
    bool TriggerReplay,
    float ThresholdModifier,
    float LeakModifier,
    bool PhaseChanged);
