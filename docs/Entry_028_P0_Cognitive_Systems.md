# Folded Archive Entry 028: P0 Cognitive Systems Implementation

## Summary

This entry implements the four highest-priority improvements identified in the Entry 028 roadmap:

1. **Basal Ganglia Circuit** - Action selection via direct/indirect pathways
2. **Reward Prediction Error System** - VTA dopamine RPE signaling  
3. **Working Memory PFC** - Sustained activity via attractor dynamics
4. **Systems Consolidation** - Sleep-dependent hippocampus-to-cortex transfer

Together, these systems form a coherent learning architecture that enables autonomous knowledge acquisition, maintenance, and consolidation.

---

## 1. BASAL GANGLIA CIRCUIT

### Anatomical Basis
Frank 2005, Bogacz & Gurney 2007

### Implementation

```
Cortex (motor, PFC) → STRIATUM
                      ↓
      ┌───────────────┴───────────────┐
      ↓ (D1 receptors)                ↓ (D2 receptors)
DIRECT PATHWAY                  INDIRECT PATHWAY
(Go, facilitation)              (NoGo, suppression)
      ↓                               ↓
   SNr/GPi ←─────────────────── GPe → STN
      ↓                               ↑
   Thalamus ← ← ← ← ← ← ← ← ← ← ← ← ─┘
      ↓                          (hyperdirect)
   Motor Cortex
```

### Key Features

- **D1/D2 MSN Populations**: Striatal medium spiny neurons with distinct dopamine receptor types
- **Direct Pathway (Go)**: D1-expressing MSNs, inhibit SNr → disinhibit thalamus → enable action
- **Indirect Pathway (NoGo)**: D2-expressing MSNs, complex GPe→STN→SNr cascade → inhibit thalamus
- **Hyperdirect Pathway**: Cortex → STN → SNr, fast global brake
- **Dopamine Modulation**: High DA enhances D1 (Go), suppresses D2 (NoGo)
- **Action Value Learning**: RPE updates action values via Hebbian-like plasticity

### Configuration Options

```csharp
EnableBasalGangliaCircuit = true
BasalGangliaChannels = 8         // Action channels
DirectPathwayStrength = 0.8f     // Go pathway gain
IndirectPathwayStrength = 0.6f   // NoGo pathway gain
HyperdirectPathwayStrength = 0.5f // Global brake gain
```

### API

```csharp
// Get selection state
var state = BasalGanglia.Snapshot();

// Process cortical input
var output = BasalGanglia.Step(corticalInput, dopamine, urgency, dt);

// Apply reward feedback
BasalGanglia.ApplyRewardPredictionError(rpe, learningRate);
```

---

## 2. REWARD PREDICTION ERROR SYSTEM

### Anatomical Basis
Schultz et al. 1997, Montague et al. 1996

### Implementation

VTA dopamine neurons encode **reward prediction errors** (RPE):

```
RPE = R + γV(s') - V(s)

Where:
  R = received reward
  γ = discount factor (0.95)
  V(s) = value estimate of current state
  V(s') = value estimate of next state
```

### Dopamine Response Patterns

| Event | RPE | Dopamine Response |
|-------|-----|-------------------|
| Unexpected reward | Positive | Phasic BURST |
| Expected reward | ~Zero | Tonic (baseline) |
| Reward omission | Negative | Phasic PAUSE |

### Key Features

- **Tonic Dopamine**: Baseline level maintained at setpoint (~0.15)
- **Phasic Bursts**: Rapid increases for positive RPE, up to +0.6
- **Phasic Pauses**: Dips below baseline for negative RPE, down to -0.4
- **State Value Learning**: TD learning updates value estimates
- **Action Value Learning**: Q-learning for state-action pairs

### Configuration Options

```csharp
EnableRewardPrediction = true
RPEDiscountFactor = 0.95f       // Future reward discounting
RPELearningRate = 0.1f          // Value update rate
RPETonicDopamineSetpoint = 0.15f
RPEPhasicDecayRate = 2.0f       // Phasic signal decay
RPEBurstMagnitude = 0.6f        // Max positive phasic
RPEPauseMagnitude = 0.4f        // Max negative phasic
```

### API

```csharp
// Step dynamics (call each tick)
RewardPrediction.Step(dt);

// Process state transition
var output = RewardPrediction.ProcessTransition(newState, reward, action);

// Get effective dopamine for downstream systems
float dopamine = RewardPrediction.GetEffectiveDopamine();

// Get last RPE for learning
float rpe = RewardPrediction.GetLastRPE();
```

---

## 3. WORKING MEMORY PFC

### Anatomical Basis
Goldman-Rakic 1995, Wang 2001, Compte et al. 2000

### Implementation

Working memory emerges from **bistable attractor dynamics** in PFC:

```
┌─────────────────────────────────────────┐
│            ATTRACTOR NETWORK            │
│                                         │
│   Slot 1    Slot 2    Slot 3   ...     │
│   [====]    [====]    [    ]           │
│   active    active    empty            │
│                                         │
│   ←── Lateral Inhibition ──→           │
│                                         │
│         ↑ Dopamine Gating ↑            │
│   (high DA = gate open for update)     │
└─────────────────────────────────────────┘
```

### Key Features

- **NMDA-like Recurrence**: Self-sustaining activity via strong recurrent excitation
- **Bistable Dynamics**: DOWN state (empty) vs UP state (maintaining item)
- **Lateral Inhibition**: Limits capacity to ~7±2 items naturally
- **Dopamine Gating**: Phasic DA opens gate for new encodings
- **Decay/Eviction**: Items fade without rehearsal or DA maintenance

### Configuration Options

```csharp
EnableWorkingMemory = true
WorkingMemorySlots = 7            // Capacity slots
WorkingMemoryPatternSize = 64     // Pattern dimensionality
WorkingMemoryRecurrentStrength = 0.85f
WorkingMemoryLateralInhibition = 0.25f
WorkingMemoryDecayRate = 0.02f
WorkingMemoryGatingThreshold = 0.4f  // DA threshold to open gate
```

### API

```csharp
// Step dynamics
WorkingMemory.Step(dt, dopamineLevel);

// Encode new item
int slot = WorkingMemory.Encode(pattern, label);

// Query for matching pattern
var (similarity, slot) = WorkingMemory.Query(pattern);

// Get all active items
var items = WorkingMemory.GetActiveItems();
```

---

## 4. SYSTEMS CONSOLIDATION

### Anatomical Basis
McClelland et al. 1995, Frankland & Bontempi 2005, Diekelmann & Born 2010

### Implementation

Memory consolidation requires coordination of three sleep oscillations (the "sleep triplet"):

```
                    NREM SLEEP
                        │
    ┌───────────────────┼───────────────────┐
    ↓                   ↓                   ↓
SLOW OSCILLATION   SLEEP SPINDLE    SHARP-WAVE RIPPLE
  (0.5-1 Hz)        (12-14 Hz)       (150-200 Hz)
  Cortical          Thalamic         Hippocampal
    │                   │                   │
    └───────────────────┴───────────────────┘
                        │
                  OPTIMAL TIMING:
              Ripple during UP state,
              followed by spindle
                        │
                        ↓
              CORTICAL PLASTICITY
              (slow Hebbian learning)
```

### Key Features

- **Slow Oscillation Tracking**: UP/DOWN state cycling at ~1Hz
- **Spindle Coordination**: Synchronizes with thalamic spindles
- **Ripple Triggering**: Sharp-wave ripples during UP state
- **Triplet Detection**: Bonus learning when all three align
- **Gradual Transfer**: Hippocampal traces → cortical representations
- **Independence Tracking**: Memories become hippocampus-independent

### Memory States

| State | Cortical Strength | Description |
|-------|-------------------|-------------|
| Hippocampus-dependent | < 0.2 | Requires hippocampus for retrieval |
| Transitional | 0.2 - 0.7 | Partially consolidated |
| Cortical-only | > 0.7 | Can retrieve without hippocampus |

### Configuration Options

```csharp
EnableSystemsConsolidation = true
ConsolidationMaxTraces = 256
ConsolidationCorticalLearningRate = 0.01f
ConsolidationHippocampalDecayRate = 0.001f
ConsolidationThreshold = 0.7f        // Full consolidation threshold
ConsolidationReplaysRequired = 5     // Replays for consolidation
ConsolidationTripletBonus = 2.0f     // Learning boost for aligned triplet
```

### API

```csharp
// Step during sleep
var output = Consolidation.Step(dt, sleepPhase, hippocampus, thalamus);

// Queue episode for consolidation
Consolidation.QueueForConsolidation(episodeId, pattern, salience);

// Check if memory is consolidated
float strength = Consolidation.QueryCorticalStrength(pattern);
bool independent = Consolidation.IsHippocampusIndependent(episodeId);
```

---

## 5. INTEGRATION

### Execution Order in NreEngine.Step()

```
1-13) [Existing systems: Thalamus, Sleep, Hemispheres, Amygdala, etc.]

14.1) Reward Prediction System
      - Step dopamine dynamics
      - Process salience as reward signal
      - Update neuromodulator field

14.2) Working Memory PFC
      - Step attractor dynamics with DA gating
      - Encode salient episodes

14.3) Basal Ganglia Circuit
      - Build cortical input from motor/PFC
      - Get DA for D1/D2 modulation
      - Perform action selection
      - Apply RPE for learning

14.4) Systems Consolidation
      - Step oscillation dynamics during NREM
      - Queue strong episodes
      - Process replay and cortical learning

15) [Continue with resonance, etc.]
```

### Cross-System Interactions

```
                    ┌─────────────────┐
                    │   AMYGDALA      │
                    │   (Salience)    │
                    └────────┬────────┘
                             ↓
┌─────────────┐         ┌────────────┐
│  HIPPOCAMPUS├────────→│   REWARD   │
│  (Episodes) │         │ PREDICTION │
└──────┬──────┘         │   (VTA)    │
       │                └─────┬──────┘
       │                      │ Dopamine
       ↓                      ↓
┌──────────────┐        ┌─────────────┐
│   SYSTEMS    │        │   WORKING   │
│ CONSOLIDATION│        │   MEMORY    │
│   (Sleep)    │        │   (PFC)     │
└──────────────┘        └──────┬──────┘
                               │
                               ↓
                        ┌─────────────┐
                        │   BASAL     │
                        │  GANGLIA    │
                        │  (Action)   │
                        └─────────────┘
```

---

## 6. FILES ADDED/MODIFIED

### New Files
1. **BasalGangliaCircuit.cs** - Complete direct/indirect pathway implementation
2. **RewardPredictionSystem.cs** - VTA dopamine RPE signaling
3. **WorkingMemoryPFC.cs** - Attractor-based working memory
4. **SystemsConsolidation.cs** - Sleep-dependent memory consolidation

### Modified Files
1. **NreEngineOptions.cs** - Added configuration options for all four systems
2. **Dtos.cs** - Added status DTOs for monitoring
3. **NreEngine.cs** - Integrated systems into main loop, added helper methods

---

## 7. VERIFICATION

The implementation can be verified by observing:

1. **Basal Ganglia**: 
   - SelectedChannel changes based on cortical input
   - D1/D2 activation shifts with dopamine level
   - ThalamicGating opens for selected actions

2. **Reward Prediction**:
   - PhasicDopamine bursts on unexpected rewards
   - LearnedStates/LearnedActions increase over time
   - EffectiveDopamine reflects tonic + phasic

3. **Working Memory**:
   - ActiveSlots count 0-7 based on encoding
   - GateOpenness responds to dopamine
   - Slot.Strength decays without DA maintenance

4. **Systems Consolidation**:
   - SlowOscPhase toggles Up/Down during NREM
   - TripletAlignments increment when all three oscillations align
   - CorticalOnly count increases over repeated sleep cycles

---

## 8. REFERENCES

- Bogacz & Gurney (2007). The basal ganglia and cortex implement optimal decision making. Neural Computation.
- Compte et al. (2000). Synaptic mechanisms and network dynamics underlying spatial working memory. Cerebral Cortex.
- Diekelmann & Born (2010). The memory function of sleep. Nature Reviews Neuroscience.
- Frank (2005). Dynamic dopamine modulation in the basal ganglia. Journal of Cognitive Neuroscience.
- Frankland & Bontempi (2005). The organization of recent and remote memories. Nature Reviews Neuroscience.
- Goldman-Rakic (1995). Cellular basis of working memory. Neuron.
- McClelland et al. (1995). Why there are complementary learning systems. Psychological Review.
- Montague et al. (1996). A framework for mesencephalic dopamine systems. Journal of Neuroscience.
- Schultz et al. (1997). A neural substrate of prediction and reward. Science.
- Wang (2001). Synaptic reverberation underlying mnemonic persistent activity. Trends in Neurosciences.

---

**Document Prepared By:** Claude (Anthropic)  
**Date:** February 6, 2026  
**Version:** NRE v12
