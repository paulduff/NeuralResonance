# Folded Archive Entry 025: Enhanced Neural Systems

## Summary

Five major enhancements to make the Neural Resonance Engine more biologically realistic:

1. **Cortical Microcircuits** - E/I neuron populations with interneuron subtypes
2. **Enhanced Neuromodulators** - Receptor dynamics, desensitization, and reuptake
3. **Synaptic Tagging and Capture** - Long-term memory consolidation
4. **Hierarchical Sensory Processing** - V1→IT visual, A1→Parabelt auditory pathways
5. **Attention Mechanisms** - Biased competition and selective amplification

---

## 1. Cortical Microcircuits (CorticalMicrocircuit.cs)

### Biological Basis

Real cortex contains ~80% excitatory pyramidal neurons and ~20% inhibitory interneurons. The interneurons have distinct subtypes:

- **PV+ (Parvalbumin)**: Fast-spiking, perisomatic inhibition. ~50% of interneurons.
- **SOM+ (Somatostatin)**: Low-threshold spiking, dendritic inhibition. ~35% of interneurons.
- **VIP+ (Vasoactive Intestinal Peptide)**: Disinhibitory (inhibits other interneurons). ~15% of interneurons.

### Implementation

```csharp
public enum NeuronType : byte
{
    Pyramidal = 0,      // Excitatory
    PVInterneuron = 1,  // Fast-spiking inhibition
    SOMInterneuron = 2, // Dendritic inhibition
    VIPInterneuron = 3, // Disinhibition
    Subcortical = 4     // Non-cortical
}
```

Each type has distinct parameters:
- **Threshold modifier**: PV fires easily (0.85), SOM moderate (0.90)
- **Refractory period**: PV very fast (0.5ms), pyramidal slow (2ms)
- **Adaptation**: SOM adapts strongly, PV adapts little
- **Synaptic sign**: Pyramidal +1 (excitatory), interneurons -1 (inhibitory)

### Key Feature: VIP Disinhibition

VIP interneurons target OTHER interneurons, creating disinhibition circuits. When VIP fires, it inhibits PV/SOM, which reduces inhibition on pyramidal cells, effectively exciting them indirectly.

---

## 2. Enhanced Neuromodulator System (NeuromodulatorSystem.cs)

### Biological Basis

Neuromodulators don't just have "levels" - they have:
- **Tonic** (baseline) vs **Phasic** (burst) release
- **Multiple receptor subtypes** with different effects
- **Receptor desensitization** under sustained activation
- **Active reuptake** and enzymatic degradation

### Four Neuromodulator Systems

| System | Source | Tonic Effect | Phasic Effect |
|--------|--------|--------------|---------------|
| **Dopamine** | VTA/SNc | Motivation, vigor | Reward prediction error |
| **Norepinephrine** | Locus Coeruleus | Arousal, vigilance | Alerting, attention capture |
| **Serotonin** | Raphe Nuclei | Mood, behavioral inhibition | Aversive signals, patience |
| **Acetylcholine** | Basal Forebrain | Attention mode | Cue detection, memory encoding |

### Receptor Subtypes

```csharp
// Dopamine
D1Sensitivity  // Excitatory, Go pathway
D2Sensitivity  // Inhibitory, NoGo pathway

// Norepinephrine
Alpha2Sensitivity  // Presynaptic autoreceptor (regulates release)
BetaSensitivity    // Postsynaptic, gain modulation

// Serotonin
HT1ASensitivity  // Inhibitory, anxiolytic
HT2ASensitivity  // Excitatory, cognitive flexibility

// Acetylcholine
MuscarinicSensitivity  // Slow, modulatory
NicotinicSensitivity   // Fast, attention
```

### Receptor Desensitization

Sustained high neuromodulator levels reduce receptor sensitivity:

```csharp
if (daTotal > 0.3f)
{
    D1Sensitivity -= desensRate * (daTotal - 0.3f);
    D2Sensitivity -= desensRate * (daTotal - 0.3f) * 0.8f;
}
else
{
    // Resensitize when levels are low
    D1Sensitivity += resensRate * (1f - D1Sensitivity);
}
```

---

## 3. Synaptic Tagging and Capture (SynapticTagging.cs)

### Biological Basis

Memory consolidation occurs in two phases:
- **Early-LTP (E-LTP)**: Short-lasting (~1-3 hours), protein synthesis independent
- **Late-LTP (L-LTP)**: Long-lasting (hours to lifetime), requires protein synthesis

The **Synaptic Tagging and Capture (STC)** hypothesis:
1. Strong synaptic activation sets a "tag" at that synapse
2. Strong cell activation triggers synthesis of plasticity-related proteins (PRPs)
3. Tagged synapses can "capture" PRPs to convert E-LTP to L-LTP
4. Weak inputs can be consolidated if they arrive near strong inputs (behavioral tagging)

### Implementation

```csharp
// On Hebbian weight change
if (weightChange > TagThreshold)
    SynapticTags.SetTag(preVoxel, postVoxel, weightChange, currentWeight);

// On strong activation
if (activationStrength > PRPSynthesisThreshold)
    SynapticTags.TriggerPRPSynthesis(voxelIndex, activationStrength);

// During Step(): Check for capture
if (taggedSynapse && postNeuronHasPRP)
{
    // Convert E-LTP to L-LTP!
    synapse.IsConsolidated = true;
    synapse.Weight += ConsolidationBoost;
}
```

### Sleep-Dependent Consolidation

Consolidation is enhanced during sleep:
- **NREM**: 1.5x consolidation boost
- **REM**: 1.3x replay boost

---

## 4. Hierarchical Sensory Processing (SensoryHierarchy.cs)

### Visual Pathway (Ventral "What" Stream)

| Level | Function | Resolution | Channels |
|-------|----------|------------|----------|
| **V1** | Edge/orientation detection | 16×16 | 8 (orientations) |
| **V2** | Texture, simple shapes | 8×8 | 16 |
| **V4** | Color, complex shapes | 4×4 | 32 |
| **IT** | Object identity | 4×4 | 64 |

Each level:
- Pools from the level below (max/avg pooling)
- Has increasing receptive fields
- Shows increasing position/size invariance
- Has temporal smoothing (working memory)

### Auditory Pathway

| Level | Function | Resolution | Channels |
|-------|----------|------------|----------|
| **A1** | Tonotopic frequency bands | 16 | 16 |
| **Belt** | Spectrotemporal features | 8 | 24 |
| **Parabelt** | Complex sound categories | 4 | 32 |

### Top-Down Attention

Higher levels can modulate lower levels:

```csharp
SensoryHierarchy.ApplyTopDownVisualAttention(focusX, focusY, attentionStrength);
// Boosts V1 processing at focused location
```

---

## 5. Attention System (AttentionSystem.cs)

### Two Types of Attention

| Type | Name | Driver | Duration | IOR |
|------|------|--------|----------|-----|
| **Exogenous** | Bottom-up | Stimulus salience | Brief | Yes |
| **Endogenous** | Top-down | Goals, expectations | Sustained | No |

### Biased Competition

Multiple stimuli compete for representation. Attention biases the competition:

```csharp
// Without attention: winner-take-all based on stimulus strength
// With attention: attended stimulus gets multiplicative boost

float[] result = ApplyBiasedCompetition(activities, attentionWeights);
// attended items win even if slightly weaker
```

### Priority Map

3D attention weights computed from:
1. Bottom-up salience (local contrast, feature discontinuities)
2. Top-down focus points (Gaussian attention fields)
3. Inhibition of return (reduced priority at recently attended locations)

### Feature-Based Attention

Enhance all locations with target feature:

```csharp
Attention.SetFeatureAttention(featureIndex: 5, weight: 1.5f);
// All locations with feature #5 get 1.5x gain
```

### Gain Modulation

```csharp
float gain = Attention.GetGainModulation(x, y, z, featureIndex);
// Returns 0.3 (suppressed) to 2.0 (enhanced)
// Neural responses are multiplied by this gain
```

---

## Configuration Options

All new systems can be enabled/disabled and tuned via NreEngineOptions:

```csharp
// Cortical Microcircuit
EnableCorticalMicrocircuit = true
PyramidalFraction = 0.80f
PVFraction = 0.10f

// Enhanced Neuromodulators
EnableEnhancedNeuromodulators = true
ReceptorDesensitizationRate = 0.15f

// Synaptic Tagging
EnableSynapticTagging = true
TagHalfLifeSec = 120f  // 2 minutes for demo
ConsolidationBoost = 2.5f

// Sensory Hierarchy
EnableSensoryHierarchy = true
SensoryVisualWidth = 16

// Attention
EnableAttentionSystem = true
AttentionSpatialSigma = 3.0f
AttentionTopDownGain = 0.6f
```

---

## Files Added

| File | LOC | Description |
|------|-----|-------------|
| CorticalMicrocircuit.cs | ~280 | E/I populations with interneuron subtypes |
| NeuromodulatorSystem.cs | ~350 | Receptor dynamics, desensitization, reuptake |
| SynapticTagging.cs | ~320 | Tag-and-capture memory consolidation |
| SensoryHierarchy.cs | ~420 | V1→IT visual, A1→Parabelt auditory |
| AttentionSystem.cs | ~450 | Biased competition, priority maps |

---

## References

### Cortical Microcircuits
- Markram et al. 2004: Interneurons of the neocortical inhibitory system
- Tremblay et al. 2016: GABAergic interneuron subtypes
- Pfeffer et al. 2013: VIP disinhibitory circuits

### Neuromodulators
- Grace et al. 2007: Tonic/phasic dopamine
- Aston-Jones & Cohen 2005: LC-NE adaptive gain
- Cools et al. 2008: 5-HT and behavioral flexibility

### Synaptic Tagging
- Frey & Morris 1997: Synaptic tagging and LTP
- Redondo & Morris 2011: Making memories last

### Sensory Processing
- Felleman & Van Essen 1991: Visual hierarchy
- DiCarlo et al. 2012: Ventral stream transformations
- Rauschecker & Scott 2009: Auditory streams

### Attention
- Desimone & Duncan 1995: Biased competition
- Corbetta & Shulman 2002: Attention networks
- Reynolds & Heeger 2009: Normalization model
