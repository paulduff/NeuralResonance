# Entry 026: Cortical Microcircuit - Neuron Types and Functional Operations

## Overview

The cortical microcircuit implements biologically-realistic neuron type diversity with distinct functional operations for each type. This creates emergent dynamics like lateral inhibition, gain control, and disinhibitory gating that are fundamental to cortical computation.

---

## Neuron Types

### 1. Pyramidal Neurons (80% of cortical neurons)

**Structure:**
- Large cell body with apical dendrite extending toward cortical surface
- Basal dendrites receiving local input
- Long axon projecting to distant targets

**Functional Operation:**
```
Threshold:     Standard (1.0x modifier)
Leak Rate:     Standard (1.0x)
Refractory:    2.0 ms (normal recovery)
Synaptic Sign: +1.0 (EXCITATORY)
Adaptation:    Moderate (0.15) - reduces firing rate over sustained input
Burst Mode:    Yes (0.3 propensity) - can fire bursts for salient input
```

**Computational Role:**
- Primary information carriers (projection neurons)
- Integrate inputs from all sources (feedforward, feedback, lateral)
- Long-range communication between regions
- Form the "output" of cortical processing

**Code Implementation:**
```csharp
// Pyramidal spike → positive effect on targets
eff *= synapticSign;  // synapticSign = +1.0
dstVol.EnqueueSignal(tx, ty, tz, new DelayedSignal(delay, eff));  // eff > 0
```

---

### 2. PV+ Interneurons (10% of cortical neurons)

**Structure:**
- Fast-spiking basket cells
- Dense axonal arbor targeting pyramidal cell bodies (perisomatic)
- Chandelier cells targeting axon initial segments

**Functional Operation:**
```
Threshold:     Low (0.85x) - fires easily, first responder
Leak Rate:     Fast (1.3x) - rapid membrane dynamics
Refractory:    0.5 ms (VERY SHORT - can fire at high rates)
Synaptic Sign: -1.0 (INHIBITORY)
Adaptation:    Minimal (0.02) - maintains firing rate
Burst Mode:    None (0.0) - regular spiking only
```

**Computational Role:**
- **Feedforward inhibition**: Sharpens timing of excitatory responses
- **Gain control**: Divisive normalization of pyramidal activity
- **Gamma oscillations**: Fast synchronization (30-80 Hz)
- **Winner-take-all**: Competition between representations

**Code Implementation:**
```csharp
// PV fires → strong perisomatic inhibition
if (isInterneuron && !isVIP) {
    float interneuronInhibStrength = inhibStrength * 1.5f;  // 50% stronger
    ApplyLateralInhibition(volSelf, x, y, z, radius, interneuronInhibStrength);
}

// PV spike → negative effect on targets  
eff *= synapticSign;  // synapticSign = -1.0
// eff < 0 → reduces target Vm
```

---

### 3. SOM+ Interneurons (7% of cortical neurons)

**Structure:**
- Martinotti cells with ascending axon
- Target distal dendrites of pyramidal cells
- Receive facilitating synapses from pyramidals

**Functional Operation:**
```
Threshold:     Moderate (0.90x) - slightly easier to fire
Leak Rate:     Slow (0.95x) - integrates over longer time
Refractory:    1.5 ms (intermediate)
Synaptic Sign: -1.0 (INHIBITORY)
Adaptation:    Strong (0.25) - fires less over time
Burst Mode:    Yes (0.4) - low-threshold calcium spikes
```

**Computational Role:**
- **Dendritic inhibition**: Reduces gain of excitatory inputs
- **Feedback inhibition**: Activated by local pyramidals, limits runaway excitation
- **Surround suppression**: Sharpens tuning curves
- **Input gating**: Controls which inputs reach the soma

**Code Implementation:**
```csharp
// SOM adaptation makes it fire less over sustained input
adaptationStrength = 0.25f;  // Strong adaptation
_adaptation[x, y, z] += adaptationStrength;  // Increases effective threshold

// SOM targets dendrites → reduces input gain (same lateral inhibition path)
ApplyLateralInhibition(volSelf, x, y, z, radius, interneuronInhibStrength);
```

---

### 4. VIP+ Interneurons (3% of cortical neurons)

**Structure:**
- Bipolar cells with vertically oriented dendrites
- **Specifically target OTHER interneurons (PV and SOM)**
- Receive cholinergic and serotonergic modulation

**Functional Operation:**
```
Threshold:     Slightly low (0.95x)
Leak Rate:     Fast (1.1x)
Refractory:    1.0 ms
Synaptic Sign: -1.0 (INHIBITORY - but targets interneurons!)
Adaptation:    Low (0.10)
Burst Mode:    Low (0.1)
Targets:       OTHER INTERNEURONS (disinhibition circuit)
```

**Computational Role:**
- **DISINHIBITION**: Inhibits inhibitors → net excitation of pyramidals
- **Attentional gating**: Top-down signals activate VIP → release from inhibition
- **Behavioral state control**: Modulated by ACh during attention
- **Surround facilitation**: Opposite of surround suppression

**Code Implementation:**
```csharp
// VIP disinhibition - the key circuit
private void ApplyVIPDisinhibition(NeuralVolume vol, int cx, int cy, int cz, int radius, float strength) {
    var targetType = Microcircuit.GetNeuronType(x, y, z);
    
    if (targetType == NeuronType.Pyramidal) {
        // Net effect: BOOST pyramidal Vm (disinhibition = excitation)
        vol.Vm[x, y, z] += boost;
    }
    else if (targetType == NeuronType.PVInterneuron || 
             targetType == NeuronType.SOMInterneuron) {
        // VIP directly INHIBITS other interneurons
        vol.Vm[x, y, z] -= inhib;
    }
}

// VIP preferentially targets interneurons in synaptic propagation
if (isVIP) {
    if (targetType == PVInterneuron || targetType == SOMInterneuron) {
        eff *= 1.5f;  // Strong inhibition of interneurons
    } else if (targetType == Pyramidal) {
        eff *= 0.3f;  // Weak direct effect on pyramidals
    }
}
```

---

### 5. Subcortical Neurons (non-cortical regions)

**Structure:**
- Various types depending on region (thalamic relay, striatal medium spiny, etc.)
- Default dynamics for non-cortical regions

**Functional Operation:**
```
Threshold:     Standard (1.0x)
Leak Rate:     Standard (1.0x)
Refractory:    2.0 ms
Synaptic Sign: +1.0 (mostly excitatory projection neurons)
Adaptation:    Moderate (0.10)
Burst Mode:    Some (0.2)
```

---

## Canonical Microcircuit Dynamics

### The Basic E-I Loop

```
Input → Pyramidal (E) → PV/SOM (I) → Pyramidal (E)
         ↑__________________|
         (feedback inhibition)
```

1. Input arrives and excites pyramidal cells
2. Pyramidals excite local interneurons
3. Interneurons inhibit nearby pyramidals
4. Result: Sparse, winner-take-all activity pattern

### VIP Disinhibition Circuit

```
Top-down attention signal
         ↓
        VIP (I) ─────inhibits────→ PV/SOM (I)
                                       |
                                   inhibits
                                       ↓
                                  Pyramidal (E) ← now MORE active!
```

1. Attention/cholinergic signal activates VIP cells
2. VIP inhibits PV and SOM interneurons
3. Less inhibition on pyramidals = more activity
4. Result: Selected representations are enhanced

### Temporal Dynamics

| Type | First Spike | Sustained Firing | Recovery |
|------|-------------|------------------|----------|
| PV   | Fastest (0.85x threshold) | Highest (minimal adaptation) | Fastest (0.5ms) |
| Pyramidal | Normal | Moderate (adapts) | Normal (2ms) |
| SOM  | Moderate | Decreases (strong adaptation) | Moderate (1.5ms) |
| VIP  | Normal | Moderate | Normal (1ms) |

---

## Implementation Integration

### Threshold Modification

```csharp
// Per-neuron threshold includes:
// 1. Base type modifier (PV=0.85, SOM=0.90, etc.)
// 2. Adaptation (increases after spiking)
// 3. Refractory period (very high during refractory)
float neuronThresholdMod = Microcircuit.GetEffectiveThresholdMod(x, y, z);
theta *= neuronThresholdMod;
```

### Leak Rate Modification

```csharp
// PV neurons have faster membrane dynamics
float neuronLeakMod = nparams.LeakMod;  // PV=1.3, SOM=0.95, etc.
float leak = baseLeak * neuronLeakMod;
```

### Spike-Frequency Adaptation

```csharp
// On each spike:
Microcircuit.OnSpike(x, y, z, dt);
// Increases adaptation state, which increases effective threshold
// SOM adapts strongly (0.25), PV minimally (0.02)
```

### Synaptic Sign in Propagation

```csharp
// Excitatory vs inhibitory effect
eff *= synapticSign;  // +1.0 or -1.0
// Positive eff → increases target Vm
// Negative eff → decreases target Vm
```

---

## Emergent Properties

These neuron-specific dynamics create emergent computational properties:

1. **Sparse coding**: E-I balance maintains low average activity
2. **Temporal precision**: PV fast-spiking sharpens spike timing
3. **Gain control**: SOM dendritic inhibition normalizes responses
4. **Selective attention**: VIP disinhibition gates information flow
5. **Oscillations**: E-I interactions generate gamma (PV) and theta (SOM) rhythms
6. **Working memory**: Adaptation creates temporal dynamics for maintenance

---

## References

- Markram et al. 2004: "Interneurons of the neocortical inhibitory system"
- Tremblay et al. 2016: "GABAergic interneurons in the neocortex"
- Pfeffer et al. 2013: "Inhibition of inhibition in visual cortex"
- Kepecs & Fishell 2014: "Interneuron cell types are fit to function"
- Pi et al. 2013: "Cortical interneurons that specialize in disinhibitory control"
