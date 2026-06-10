namespace NRE.Core.Engine;

/// <summary>
/// Reward Prediction Error System: VTA Dopamine Signaling
/// 
/// BIOLOGICAL BASIS (Schultz et al. 1997, Montague et al. 1996):
/// 
/// Dopamine neurons in the VTA (ventral tegmental area) encode reward prediction errors:
/// - BURST firing: Unexpected reward (positive RPE)
/// - PAUSE firing: Expected reward omitted (negative RPE)  
/// - TONIC firing: Expected reward received (zero RPE)
/// 
/// This signal is critical for:
/// 1. Updating action values in striatum (reinforcement learning)
/// 2. Updating predictions in PFC (goal updating)
/// 3. Modulating hippocampal encoding (memory for rewarding events)
/// 4. Driving approach behavior (motivation)
/// 
/// Implements temporal difference (TD) learning:
///   RPE = R + γV(s') - V(s)
///   
/// Where R is reward, V is value estimate, γ is discount factor.
/// 
/// Folded Archive Entry 028: P0 Implementation
/// </summary>
public sealed class RewardPredictionSystem
{
    private readonly object _gate = new();
    
    // === VALUE ESTIMATES ===
    // State values learned through experience
    private readonly Dictionary<int, float> _stateValues = new();
    private readonly Dictionary<int, float> _stateVisitCounts = new();
    
    // Action values (Q-values) per state-action pair
    private readonly Dictionary<(int state, int action), float> _actionValues = new();
    
    // === VTA STATE ===
    private float _tonicDopamine;
    private float _phasicDopamine;
    private float _lastRPE;
    private float _currentReward;
    private int _currentState;
    private int _previousState;
    private int _lastAction;
    
    // === CONFIGURATION ===
    private readonly float _discountFactor;     // γ: future reward discounting
    private readonly float _learningRate;       // α: value update rate
    private readonly float _tonicSetpoint;      // Baseline dopamine level
    private readonly float _phasicDecayRate;    // How fast phasic signal decays
    private readonly float _burstMagnitude;     // Max phasic increase for positive RPE
    private readonly float _pauseMagnitude;     // Max phasic decrease for negative RPE
    
    // === METRICS ===
    private int _totalRewards;
    private float _cumulativeReward;
    private float _meanRPE;
    private float _rpeVariance;
    private readonly Queue<float> _rpeHistory = new(100);
    
    public RewardPredictionSystem(
        float discountFactor = 0.95f,
        float learningRate = 0.1f,
        float tonicSetpoint = 0.15f,
        float phasicDecayRate = 2.0f,
        float burstMagnitude = 0.6f,
        float pauseMagnitude = 0.4f)
    {
        _discountFactor = discountFactor;
        _learningRate = learningRate;
        _tonicSetpoint = tonicSetpoint;
        _phasicDecayRate = phasicDecayRate;
        _burstMagnitude = burstMagnitude;
        _pauseMagnitude = pauseMagnitude;
        
        _tonicDopamine = tonicSetpoint;
        _phasicDopamine = 0f;
    }
    
    /// <summary>Get current state snapshot for monitoring.</summary>
    public RewardPredictionState Snapshot()
    {
        lock (_gate)
        {
            return new RewardPredictionState(
                TonicDopamine: _tonicDopamine,
                PhasicDopamine: _phasicDopamine,
                EffectiveDopamine: GetEffectiveDopamine(),
                LastRPE: _lastRPE,
                CurrentStateValue: GetStateValue(_currentState),
                TotalRewards: _totalRewards,
                CumulativeReward: _cumulativeReward,
                MeanRPE: _meanRPE,
                RPEVariance: _rpeVariance,
                LearnedStates: _stateValues.Count,
                LearnedActions: _actionValues.Count);
        }
    }
    
    /// <summary>
    /// Main update step. Called each simulation tick.
    /// Decays phasic dopamine and maintains tonic baseline.
    /// </summary>
    public void Step(float dt)
    {
        lock (_gate)
        {
            // Phasic dopamine decays toward zero
            _phasicDopamine *= MathF.Exp(-_phasicDecayRate * dt);
            
            // Tonic dopamine slowly returns to setpoint
            float tonicDelta = (_tonicSetpoint - _tonicDopamine) * 0.5f * dt;
            _tonicDopamine += tonicDelta;
            _tonicDopamine = Math.Clamp(_tonicDopamine, 0f, 0.5f);
        }
    }
    
    /// <summary>
    /// Process a state transition and compute reward prediction error.
    /// This is the core TD learning computation.
    /// </summary>
    /// <param name="newState">New state hash/identifier</param>
    /// <param name="reward">Received reward (can be negative for punishment)</param>
    /// <param name="action">Action that was taken (for Q-learning)</param>
    /// <param name="isTerminal">Whether this is an episode-ending state</param>
    /// <returns>Reward prediction error and dopamine output</returns>
    public RPEOutput ProcessTransition(
        int newState,
        float reward,
        int action = -1,
        bool isTerminal = false)
    {
        lock (_gate)
        {
            _previousState = _currentState;
            _currentState = newState;
            _currentReward = reward;
            _lastAction = action;
            
            // Get value estimates
            float previousValue = GetStateValue(_previousState);
            float currentValue = isTerminal ? 0f : GetStateValue(_currentState);
            
            // Compute TD error (reward prediction error)
            // RPE = R + γV(s') - V(s)
            _lastRPE = reward + _discountFactor * currentValue - previousValue;
            
            // Update value estimates
            UpdateStateValue(_previousState, _lastRPE);
            
            if (action >= 0)
            {
                UpdateActionValue(_previousState, action, _lastRPE);
            }
            
            // Convert RPE to dopamine signal
            ComputeDopamineResponse(_lastRPE);
            
            // Track metrics
            if (reward != 0)
            {
                _totalRewards++;
                _cumulativeReward += reward;
            }
            
            UpdateRPEStatistics(_lastRPE);
            
            // Track state visits
            _stateVisitCounts.TryGetValue(_currentState, out float visits);
            _stateVisitCounts[_currentState] = visits + 1;
            
            return new RPEOutput(
                RPE: _lastRPE,
                TonicDopamine: _tonicDopamine,
                PhasicDopamine: _phasicDopamine,
                EffectiveDopamine: GetEffectiveDopamine(),
                StateValue: currentValue,
                Surprise: MathF.Abs(_lastRPE),
                IsNovel: visits < 3);
        }
    }
    
    /// <summary>
    /// Process reward without state transition (e.g., unexpected reward).
    /// </summary>
    public RPEOutput ProcessReward(float reward)
    {
        lock (_gate)
        {
            _currentReward = reward;
            
            // Pure reward without state change = surprise
            // RPE is just the reward minus expected baseline
            float expectedReward = _tonicDopamine * 0.5f; // Rough expectation
            _lastRPE = reward - expectedReward;
            
            ComputeDopamineResponse(_lastRPE);
            
            if (reward != 0)
            {
                _totalRewards++;
                _cumulativeReward += reward;
            }
            
            UpdateRPEStatistics(_lastRPE);
            
            return new RPEOutput(
                RPE: _lastRPE,
                TonicDopamine: _tonicDopamine,
                PhasicDopamine: _phasicDopamine,
                EffectiveDopamine: GetEffectiveDopamine(),
                StateValue: GetStateValue(_currentState),
                Surprise: MathF.Abs(_lastRPE),
                IsNovel: false);
        }
    }
    
    /// <summary>
    /// Get the best action for a given state (policy extraction).
    /// </summary>
    public int GetBestAction(int state, int numActions)
    {
        lock (_gate)
        {
            int bestAction = 0;
            float bestValue = float.NegativeInfinity;
            
            for (int a = 0; a < numActions; a++)
            {
                float value = GetActionValue(state, a);
                if (value > bestValue)
                {
                    bestValue = value;
                    bestAction = a;
                }
            }
            
            return bestAction;
        }
    }
    
    /// <summary>
    /// Get action values for all actions in a state (for softmax selection).
    /// </summary>
    public float[] GetActionValues(int state, int numActions)
    {
        lock (_gate)
        {
            var values = new float[numActions];
            for (int a = 0; a < numActions; a++)
            {
                values[a] = GetActionValue(state, a);
            }
            return values;
        }
    }
    
    /// <summary>
    /// Get effective dopamine level (tonic + phasic).
    /// This is what downstream systems should use.
    /// </summary>
    public float GetEffectiveDopamine()
    {
        return Math.Clamp(_tonicDopamine + _phasicDopamine, 0f, 1f);
    }
    
    /// <summary>
    /// Get current phasic signal (for transient modulation).
    /// </summary>
    public float GetPhasicSignal() => _phasicDopamine;
    
    /// <summary>
    /// Get the last computed RPE (for external systems like basal ganglia).
    /// </summary>
    public float GetLastRPE() => _lastRPE;
    
    /// <summary>
    /// Inject external reward signal (e.g., from amygdala salience).
    /// </summary>
    public void InjectReward(float reward)
    {
        lock (_gate)
        {
            _currentReward += reward;
        }
    }
    
    /// <summary>
    /// Set tonic dopamine directly (for external modulation).
    /// </summary>
    public void SetTonicLevel(float level)
    {
        lock (_gate)
        {
            _tonicDopamine = Math.Clamp(level, 0f, 0.5f);
        }
    }
    
    /// <summary>
    /// Reset all learned values.
    /// </summary>
    public void Reset()
    {
        lock (_gate)
        {
            _stateValues.Clear();
            _stateVisitCounts.Clear();
            _actionValues.Clear();
            
            _tonicDopamine = _tonicSetpoint;
            _phasicDopamine = 0f;
            _lastRPE = 0f;
            _currentReward = 0f;
            _currentState = 0;
            _previousState = 0;
            _lastAction = -1;
            
            _totalRewards = 0;
            _cumulativeReward = 0f;
            _meanRPE = 0f;
            _rpeVariance = 0f;
            _rpeHistory.Clear();
        }
    }
    
    // ==================== PRIVATE METHODS ====================
    
    private float GetStateValue(int state)
    {
        _stateValues.TryGetValue(state, out float value);
        return value;
    }
    
    private void UpdateStateValue(int state, float rpe)
    {
        _stateValues.TryGetValue(state, out float currentValue);
        float newValue = currentValue + _learningRate * rpe;
        _stateValues[state] = Math.Clamp(newValue, -10f, 10f);
        
        // Limit dictionary size
        if (_stateValues.Count > 10000)
        {
            PruneOldStates();
        }
    }
    
    private float GetActionValue(int state, int action)
    {
        var key = (state, action);
        _actionValues.TryGetValue(key, out float value);
        return value;
    }
    
    private void UpdateActionValue(int state, int action, float rpe)
    {
        var key = (state, action);
        _actionValues.TryGetValue(key, out float currentValue);
        float newValue = currentValue + _learningRate * rpe;
        _actionValues[key] = Math.Clamp(newValue, -10f, 10f);
        
        // Limit dictionary size
        if (_actionValues.Count > 50000)
        {
            PruneOldActions();
        }
    }
    
    private void ComputeDopamineResponse(float rpe)
    {
        if (rpe > 0)
        {
            // Positive RPE → dopamine burst
            // Magnitude scaled by RPE, saturating for large values
            float burstSize = _burstMagnitude * MathF.Tanh(rpe * 2f);
            _phasicDopamine = Math.Max(_phasicDopamine, burstSize);
        }
        else if (rpe < 0)
        {
            // Negative RPE → dopamine pause (dip below tonic)
            float pauseSize = _pauseMagnitude * MathF.Tanh(-rpe * 2f);
            _phasicDopamine = -pauseSize;
        }
        // Near-zero RPE → no phasic change (expected outcome)
    }
    
    private void UpdateRPEStatistics(float rpe)
    {
        _rpeHistory.Enqueue(rpe);
        while (_rpeHistory.Count > 100)
            _rpeHistory.Dequeue();
        
        if (_rpeHistory.Count > 0)
        {
            float sum = 0f;
            float sumSq = 0f;
            foreach (float r in _rpeHistory)
            {
                sum += r;
                sumSq += r * r;
            }
            _meanRPE = sum / _rpeHistory.Count;
            _rpeVariance = sumSq / _rpeHistory.Count - _meanRPE * _meanRPE;
        }
    }
    
    private void PruneOldStates()
    {
        // Remove states with fewest visits
        var toRemove = _stateValues.Keys
            .OrderBy(s => _stateVisitCounts.TryGetValue(s, out float v) ? v : 0f)
            .Take(_stateValues.Count / 4)
            .ToList();
        
        foreach (var s in toRemove)
        {
            _stateValues.Remove(s);
            _stateVisitCounts.Remove(s);
        }
    }
    
    private void PruneOldActions()
    {
        // First pass: remove action values for states no longer tracked.
        var validStates = new HashSet<int>(_stateValues.Keys);
        var toRemove = _actionValues.Keys
            .Where(k => !validStates.Contains(k.state))
            .ToList();

        foreach (var k in toRemove)
            _actionValues.Remove(k);

        if (_actionValues.Count <= 50000)
            return;

        // Fallback: every state is still live but we are over budget. Evict the
        // actions belonging to the least-visited states until back under the cap,
        // so the prune always makes progress (otherwise the O(n) scan would rerun
        // on every subsequent insert).
        var coldestStates = _actionValues.Keys
            .Select(k => k.state)
            .Distinct()
            .OrderBy(s => _stateVisitCounts.TryGetValue(s, out float v) ? v : 0f)
            .ToList();

        foreach (var state in coldestStates)
        {
            if (_actionValues.Count <= 50000)
                break;

            foreach (var k in _actionValues.Keys.Where(k => k.state == state).ToList())
                _actionValues.Remove(k);
        }
    }
}

// ==================== PUBLIC DTOs ====================

/// <summary>State snapshot for monitoring.</summary>
public readonly record struct RewardPredictionState(
    float TonicDopamine,
    float PhasicDopamine,
    float EffectiveDopamine,
    float LastRPE,
    float CurrentStateValue,
    int TotalRewards,
    float CumulativeReward,
    float MeanRPE,
    float RPEVariance,
    int LearnedStates,
    int LearnedActions);

/// <summary>Output from RPE computation.</summary>
public readonly record struct RPEOutput(
    float RPE,
    float TonicDopamine,
    float PhasicDopamine,
    float EffectiveDopamine,
    float StateValue,
    float Surprise,
    bool IsNovel);
