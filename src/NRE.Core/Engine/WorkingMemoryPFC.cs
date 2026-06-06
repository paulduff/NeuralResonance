namespace NRE.Core.Engine;

/// <summary>
/// Working Memory System: Sustained Activity in PFC via Attractor Dynamics
/// 
/// BIOLOGICAL BASIS (Goldman-Rakic 1995, Wang 2001, Compte et al. 2000):
/// 
/// Working memory emerges from persistent neural activity in prefrontal cortex.
/// Key mechanisms:
/// 
/// 1. RECURRENT EXCITATION: Strong NMDA-mediated connections between PFC neurons
///    create positive feedback loops that sustain activity after stimulus offset.
///    
/// 2. BISTABILITY: Network has two stable states:
///    - DOWN state: Low activity, waiting for input
///    - UP state: High activity, maintaining item
///    
/// 3. ATTRACTOR DYNAMICS: Each working memory "slot" is a bump attractor.
///    Items are maintained as stable patterns of activity.
///    
/// 4. DOPAMINE GATING: Tonic dopamine stabilizes representations.
///    Phasic dopamine (from VTA RPE) enables updating:
///    - High phasic DA: Gate opens, new items can enter WM
///    - Low phasic DA: Gate closed, current contents maintained
///    
/// 5. CAPACITY LIMITS: Lateral inhibition between attractors limits how many
///    items can be simultaneously maintained (~7±2 emerges naturally).
/// 
/// Folded Archive Entry 028: P0 Implementation
/// </summary>
public sealed class WorkingMemoryPFC
{
    private readonly object _gate = new();
    
    // === ATTRACTOR NETWORK ===
    private readonly int _numSlots;
    private readonly int _patternSize;
    private readonly AttractorSlot[] _slots;
    
    // === GATING STATE ===
    private float _gateOpenness;
    private float _updateThreshold;
    private float _currentDopamine;
    
    // === CONFIGURATION ===
    private readonly float _recurrentStrength;    // NMDA-like recurrence
    private readonly float _lateralInhibition;    // Between slots
    private readonly float _decayRate;            // Passive decay
    private readonly float _nmda_tau;             // NMDA time constant
    private readonly float _gatingThreshold;      // DA level to open gate
    private readonly float _maintenanceDA;        // DA for stable maintenance
    
    // === METRICS ===
    private int _activeSlots;
    private float _meanPersistence;
    private int _totalUpdates;
    private int _totalEvictions;
    
    // === BUFFER FOR PATTERN MATCHING ===
    private readonly float[] _inputBuffer;
    private readonly float[] _queryBuffer;
    
    public WorkingMemoryPFC(
        int numSlots = 7,
        int patternSize = 64,
        float recurrentStrength = 0.85f,
        float lateralInhibition = 0.25f,
        float decayRate = 0.02f,
        float nmda_tau = 100f,
        float gatingThreshold = 0.4f,
        float maintenanceDA = 0.15f)
    {
        _numSlots = numSlots;
        _patternSize = patternSize;
        _recurrentStrength = recurrentStrength;
        _lateralInhibition = lateralInhibition;
        _decayRate = decayRate;
        _nmda_tau = nmda_tau;
        _gatingThreshold = gatingThreshold;
        _maintenanceDA = maintenanceDA;
        
        _slots = new AttractorSlot[numSlots];
        for (int i = 0; i < numSlots; i++)
        {
            _slots[i] = new AttractorSlot(patternSize);
        }
        
        _inputBuffer = new float[patternSize];
        _queryBuffer = new float[patternSize];
        _gateOpenness = 0f;
        _updateThreshold = 0.5f;
    }
    
    /// <summary>Get current state snapshot for monitoring.</summary>
    public WorkingMemoryState Snapshot()
    {
        lock (_gate)
        {
            var slotStates = new SlotState[_numSlots];
            for (int i = 0; i < _numSlots; i++)
            {
                slotStates[i] = new SlotState(
                    IsActive: _slots[i].IsActive,
                    Strength: _slots[i].Strength,
                    Age: _slots[i].Age,
                    Label: _slots[i].Label);
            }
            
            return new WorkingMemoryState(
                ActiveSlots: _activeSlots,
                GateOpenness: _gateOpenness,
                CurrentDopamine: _currentDopamine,
                MeanPersistence: _meanPersistence,
                TotalUpdates: _totalUpdates,
                TotalEvictions: _totalEvictions,
                Slots: slotStates);
        }
    }
    
    /// <summary>
    /// Main update step. Maintains attractor dynamics and decay.
    /// </summary>
    public void Step(float dt, float dopamineLevel)
    {
        lock (_gate)
        {
            _currentDopamine = dopamineLevel;
            
            // Update gate based on dopamine
            // High dopamine (phasic) opens gate for updating
            // Moderate dopamine (tonic) keeps gate closed for maintenance
            float targetGate = dopamineLevel > _gatingThreshold ? 
                (dopamineLevel - _gatingThreshold) / (1f - _gatingThreshold) : 0f;
            _gateOpenness = _gateOpenness * 0.7f + targetGate * 0.3f;
            
            // Update threshold based on inverse of maintenance DA
            // When DA is at maintenance level, threshold is normal
            // Low DA raises threshold (harder to maintain)
            _updateThreshold = 0.5f - (dopamineLevel - _maintenanceDA) * 0.3f;
            _updateThreshold = Math.Clamp(_updateThreshold, 0.2f, 0.8f);
            
            _activeSlots = 0;
            float totalPersistence = 0f;
            
            // Step each attractor slot
            for (int i = 0; i < _numSlots; i++)
            {
                var slot = _slots[i];
                
                if (slot.IsActive)
                {
                    // NMDA-like recurrent dynamics
                    // Self-excitation maintains activity
                    float selfExcitation = slot.Strength * _recurrentStrength;
                    
                    // Lateral inhibition from other active slots
                    float lateralInhib = 0f;
                    for (int j = 0; j < _numSlots; j++)
                    {
                        if (i != j && _slots[j].IsActive)
                        {
                            lateralInhib += _slots[j].Strength * _lateralInhibition;
                        }
                    }
                    
                    // DA modulates maintenance stability
                    float daStability = 1.0f + (dopamineLevel - _maintenanceDA) * 0.5f;
                    daStability = Math.Clamp(daStability, 0.5f, 1.5f);
                    
                    // Compute new strength
                    float drive = selfExcitation * daStability - lateralInhib;
                    float decay = _decayRate * (1f + lateralInhib);
                    
                    // NMDA-like temporal integration
                    float tau = _nmda_tau * daStability;
                    slot.Strength += (drive - slot.Strength - decay) * dt / tau;
                    slot.Strength = Math.Clamp(slot.Strength, 0f, 1f);
                    
                    // Check if slot falls below threshold (eviction)
                    if (slot.Strength < 0.1f)
                    {
                        slot.Clear();
                        _totalEvictions++;
                    }
                    else
                    {
                        slot.Age += dt;
                        _activeSlots++;
                        totalPersistence += slot.Age;
                    }
                }
            }
            
            _meanPersistence = _activeSlots > 0 ? totalPersistence / _activeSlots : 0f;
        }
    }
    
    /// <summary>
    /// Attempt to encode a new item into working memory.
    /// Returns the slot index if successful, -1 if gate is closed.
    /// </summary>
    public int Encode(float[] pattern, string label = "")
    {
        lock (_gate)
        {
            if (pattern.Length != _patternSize)
                throw new ArgumentException($"Pattern must be {_patternSize} elements");
            
            // Check if gate allows encoding
            if (_gateOpenness < 0.2f && _activeSlots > 0)
            {
                // Gate closed - check if this pattern matches existing item
                int matchSlot = FindMatchingSlot(pattern, 0.8f);
                if (matchSlot >= 0)
                {
                    // Refresh existing item
                    _slots[matchSlot].Refresh(pattern);
                    return matchSlot;
                }
                return -1; // Gate closed, no match
            }
            
            // Find best slot for encoding
            int targetSlot = -1;
            float lowestStrength = float.MaxValue;
            
            // First, look for empty slot
            for (int i = 0; i < _numSlots; i++)
            {
                if (!_slots[i].IsActive)
                {
                    targetSlot = i;
                    break;
                }
            }
            
            // If no empty slot, find weakest active slot
            if (targetSlot < 0)
            {
                for (int i = 0; i < _numSlots; i++)
                {
                    if (_slots[i].Strength < lowestStrength)
                    {
                        lowestStrength = _slots[i].Strength;
                        targetSlot = i;
                    }
                }
                
                // Only replace if new item has sufficient novelty
                if (targetSlot >= 0 && lowestStrength > 0.5f)
                {
                    // Strong items resist replacement unless gate is very open
                    if (_gateOpenness < 0.6f)
                        return -1;
                }
            }
            
            if (targetSlot >= 0)
            {
                _slots[targetSlot].Encode(pattern, label);
                _totalUpdates++;
                return targetSlot;
            }
            
            return -1;
        }
    }
    
    /// <summary>
    /// Query working memory for a matching pattern.
    /// Returns similarity to best match and the slot index.
    /// </summary>
    public (float similarity, int slot) Query(float[] pattern)
    {
        lock (_gate)
        {
            if (pattern.Length != _patternSize)
                throw new ArgumentException($"Pattern must be {_patternSize} elements");
            
            float bestSim = 0f;
            int bestSlot = -1;
            
            for (int i = 0; i < _numSlots; i++)
            {
                if (!_slots[i].IsActive) continue;
                
                float sim = _slots[i].ComputeSimilarity(pattern);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestSlot = i;
                }
            }
            
            return (bestSim, bestSlot);
        }
    }
    
    /// <summary>
    /// Retrieve pattern from a specific slot.
    /// </summary>
    public float[]? GetSlotPattern(int slot)
    {
        lock (_gate)
        {
            if (slot < 0 || slot >= _numSlots) return null;
            if (!_slots[slot].IsActive) return null;
            return _slots[slot].GetPatternCopy();
        }
    }
    
    /// <summary>
    /// Get all active patterns (for replay, consolidation).
    /// </summary>
    public WorkingMemoryItem[] GetActiveItems()
    {
        lock (_gate)
        {
            var items = new List<WorkingMemoryItem>();
            for (int i = 0; i < _numSlots; i++)
            {
                if (_slots[i].IsActive)
                {
                    items.Add(new WorkingMemoryItem(
                        Slot: i,
                        Pattern: _slots[i].GetPatternCopy(),
                        Strength: _slots[i].Strength,
                        Age: _slots[i].Age,
                        Label: _slots[i].Label));
                }
            }
            return items.ToArray();
        }
    }
    
    /// <summary>
    /// Clear a specific slot.
    /// </summary>
    public void ClearSlot(int slot)
    {
        lock (_gate)
        {
            if (slot >= 0 && slot < _numSlots)
            {
                _slots[slot].Clear();
            }
        }
    }
    
    /// <summary>
    /// Clear all slots.
    /// </summary>
    public void ClearAll()
    {
        lock (_gate)
        {
            for (int i = 0; i < _numSlots; i++)
            {
                _slots[i].Clear();
            }
            _activeSlots = 0;
        }
    }
    
    /// <summary>
    /// Force gate open (for testing/debugging).
    /// </summary>
    public void ForceGateOpen(float openness = 1.0f)
    {
        lock (_gate)
        {
            _gateOpenness = Math.Clamp(openness, 0f, 1f);
        }
    }
    
    /// <summary>
    /// Boost a slot's strength (e.g., due to attention).
    /// </summary>
    public void BoostSlot(int slot, float amount = 0.2f)
    {
        lock (_gate)
        {
            if (slot >= 0 && slot < _numSlots && _slots[slot].IsActive)
            {
                _slots[slot].Strength = Math.Min(1f, _slots[slot].Strength + amount);
            }
        }
    }
    
    // ==================== PRIVATE METHODS ====================
    
    private int FindMatchingSlot(float[] pattern, float threshold)
    {
        float bestSim = threshold;
        int bestSlot = -1;
        
        for (int i = 0; i < _numSlots; i++)
        {
            if (!_slots[i].IsActive) continue;
            
            float sim = _slots[i].ComputeSimilarity(pattern);
            if (sim > bestSim)
            {
                bestSim = sim;
                bestSlot = i;
            }
        }
        
        return bestSlot;
    }
    
    // ==================== INTERNAL CLASSES ====================
    
    private sealed class AttractorSlot
    {
        private readonly int _size;
        private readonly float[] _pattern;
        private float _patternNorm;
        
        public bool IsActive { get; private set; }
        public float Strength { get; set; }
        public float Age { get; set; }
        public string Label { get; private set; } = "";
        
        public AttractorSlot(int size)
        {
            _size = size;
            _pattern = new float[size];
        }
        
        public void Encode(float[] pattern, string label)
        {
            Array.Copy(pattern, _pattern, _size);
            _patternNorm = ComputeNorm(pattern);
            IsActive = true;
            Strength = 1.0f;
            Age = 0f;
            Label = label;
        }
        
        public void Refresh(float[] pattern)
        {
            // Blend with existing pattern
            for (int i = 0; i < _size; i++)
            {
                _pattern[i] = _pattern[i] * 0.7f + pattern[i] * 0.3f;
            }
            _patternNorm = ComputeNorm(_pattern);
            Strength = Math.Min(1f, Strength + 0.2f);
            Age = 0f;
        }
        
        public void Clear()
        {
            Array.Clear(_pattern, 0, _size);
            _patternNorm = 0f;
            IsActive = false;
            Strength = 0f;
            Age = 0f;
            Label = "";
        }
        
        public float ComputeSimilarity(float[] other)
        {
            if (!IsActive || _patternNorm < 0.001f) return 0f;
            
            float dot = 0f;
            float otherNorm = 0f;
            
            for (int i = 0; i < _size; i++)
            {
                dot += _pattern[i] * other[i];
                otherNorm += other[i] * other[i];
            }
            
            otherNorm = MathF.Sqrt(otherNorm);
            if (otherNorm < 0.001f) return 0f;
            
            // Cosine similarity
            return dot / (_patternNorm * otherNorm);
        }
        
        public float[] GetPatternCopy()
        {
            return (float[])_pattern.Clone();
        }
        
        private static float ComputeNorm(float[] v)
        {
            float sum = 0f;
            for (int i = 0; i < v.Length; i++)
                sum += v[i] * v[i];
            return MathF.Sqrt(sum);
        }
    }
}

// ==================== PUBLIC DTOs ====================

/// <summary>State snapshot for monitoring.</summary>
public readonly record struct WorkingMemoryState(
    int ActiveSlots,
    float GateOpenness,
    float CurrentDopamine,
    float MeanPersistence,
    int TotalUpdates,
    int TotalEvictions,
    SlotState[] Slots);

/// <summary>Individual slot state.</summary>
public readonly record struct SlotState(
    bool IsActive,
    float Strength,
    float Age,
    string Label);

/// <summary>Working memory item for retrieval.</summary>
public readonly record struct WorkingMemoryItem(
    int Slot,
    float[] Pattern,
    float Strength,
    float Age,
    string Label);
