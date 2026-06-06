# Folded Archive Entry 022: Predictive Coding

## Summary

Hierarchical predictive coding implements the dominant computational framework for cortical function. Higher regions generate predictions about lower regions; mismatches produce precision-weighted error signals that propagate up the hierarchy. This creates attention, learning, and inference.

---

## Biological Evidence

### Laminar Architecture

| Layer | Cell Type | Direction | Signal |
|-------|-----------|-----------|--------|
| L2/3 | Superficial pyramidal | Feedforward ↑ | Prediction errors |
| L4 | Granular stellate | Input | Driving input |
| L5 | Deep pyramidal (large) | Feedback ↓ | Predictions |
| L6 | Deep pyramidal (small) | Thalamic | Modulation |

### Receptor Evidence
- **AMPA receptors**: Fast, carry driving input (errors)
- **NMDA receptors**: Slow, voltage-gated, carry predictions

### Experimental Evidence

1. **Mismatch Negativity (MMN)**: Deviant stimuli evoke large error responses
2. **Repetition Suppression**: Repeated stimuli → reduced response (prediction satisfied)
3. **Omission Responses**: Neurons fire when expected stimulus is absent
4. **Predictive Remapping**: Receptive fields shift before saccades complete

---

## Implementation

### Cortical Hierarchy

```
Level 3: PFC (13) - executive, goals, context
           ↑↓
Level 2: Motor (11), Somatosensory (12), BG (3), Hippocampus (5), Amygdala (4)
           ↑↓
Level 1: Visual (9), Auditory (10), Cerebellum (6)
           ↑↓
Level 0: Thalamus (1) - relay, gating
```

### Prediction Flow (Top-Down)

| Source | Targets |
|--------|---------|
| PFC (13) | Motor, Sensory, Limbic, Thalamus |
| Motor (11) | Somatosensory, Cerebellum, BG |
| Hippocampus (5) | Sensory cortices, PFC |
| Amygdala (4) | Sensory, Hippocampus, PFC |
| Sensory cortices | Thalamus |
| Cerebellum (6) | Motor |
| BG (3) | Thalamus |

### Precision Modulation

Precision weights prediction errors. High precision = pay attention to errors.

**Neuromodulator effects:**
- **Noradrenaline**: ↑ sensory precision (bottom-up attention)
- **Dopamine**: ↑ reward-context precision
- **Serotonin**: ↓ precision (relax updating)

### Integration Points

- **Thalamus**: Gates error propagation during 40Hz binding windows
- **Amygdala**: High prediction error triggers salience/noradrenaline
- **Hippocampus**: Contextual predictions; high error = novel = encode
- **Cerebellum**: Motor forward model; already predictive

---

## Options

```csharp
// Entry 022: Predictive Coding
public bool EnablePredictiveCoding { get; init; } = true;
public float PredictionLearningRate { get; init; } = 0.02f;
public float PredictionErrorThresholdGain { get; init; } = 0.06f;
public float PredictionDecayRate { get; init; } = 0.001f;
public float BasePrecision { get; init; } = 0.5f;
public float NoradrenalinePrecisionGain { get; init; } = 0.4f;
public float DopaminePrecisionGain { get; init; } = 0.25f;
```

---

## Edge Budgets for Complete Circuit Display

Each neural circuit has a dedicated edge budget to ensure visibility:

| Circuit | Budget | Kind |
|---------|--------|------|
| Left Local | 3000 | 0 |
| Right Local | 3000 | 0 |
| Callosal | 1500 | 1 |
| Thalamo-cortical | 1000 | 2 |
| Cortico-thalamic | 800 | 3 |
| Hippocampal | 600 | 4 |
| Amygdala | 500 | 5 |
| Basal Ganglia | 600 | 6 |
| Cerebellar | 400 | 7 |
| Brainstem/Pons | 400 | 8 |
| Cortico-cortical | 1200 | 9 |
| Feedback (top-down) | 1000 | 9 |
| **Total** | **~14,000** | |

---

## References

1. Rao, R. P., & Ballard, D. H. (1999). Predictive coding in the visual cortex.
2. Friston, K. (2005). A theory of cortical responses.
3. Bastos, A. M., et al. (2012). Canonical microcircuits for predictive coding.
4. Keller, G. B., & Mrsic-Flogel, T. D. (2018). Predictive processing: A canonical cortical computation.
