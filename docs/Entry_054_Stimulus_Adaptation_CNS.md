# Entry 054 - Stimulus Adaptation CNS Loop

Date: 2026-06-13

## Purpose

Add a named stimulus-adaptation circuit so the avatar can reduce response to repeated harmless input, increase response to surprising input, and open memory encoding for genuinely new stimuli.

## Biological Rule

- Adaptation must be represented through nervous-system structures and neuromodulatory state, not simulator autopilot.
- Cortex-facing sensory adaptation remains part of sensory cortex.
- Inner modulatory and memory circuits must stay in their biological equivalents: thalamic gating, basal forebrain acetylcholine, locus coeruleus norepinephrine, ACC conflict, hippocampal novelty, and dopaminergic teaching.
- The correct connectome is mandatory: sensory cortex and thalamus detect and gate stimulus reliability, basal forebrain modulates cortical gain, LC raises arousal for surprise, hippocampus encodes novelty, ACC tracks conflict/error, and VTA/SNc provide teaching pressure.

## Actual Neural Areas

- V1, A1, and S1: sensory cortical adaptation and repetition suppression.
- Pulvinar and TRN: thalamic relay and attention gate for repeated or salient stimuli.
- Basal forebrain: cholinergic gain for learning-ready cortical state.
- Locus coeruleus: noradrenergic sensitization to unexpected or urgent stimuli.
- ACC: conflict and mismatch pressure.
- Amygdala: affective salience coupling.
- Entorhinal cortex, CA3, and CA1: hippocampal novelty binding and pattern comparison.
- VTA and SNc: dopaminergic teaching signal for changed stimulus value/action relevance.

## First Slice

- DONE: Added stimulus-adaptation runtime state to predictive perception: habituation gate, sensitization gate, repetition suppression, novelty-encoding drive, adaptation gain, mode, and evidence text.
- DONE: Added a named `stimulus_adaptation` brain-function audit row with the biological structures required for support.
- DONE: Added diagnostics coverage so repeated cues build habituation/repetition suppression and novel cues engage novelty/sensitization.

## Next Steps

- Tune per-modality adaptation constants after watching longer world-sim runs.
- Feed adaptation gain into sensory dispatch priority only after the visual system dispatch timeouts are stable.
