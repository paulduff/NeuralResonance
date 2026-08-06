# Entry 076 - Neutral Global Scalar Wire

Date: 2026-08-06

## Purpose

This rung removes the remaining live controller calculations that synthesized global limbic state, biological attention, neuromodulator levels, and reward-prediction error from central formulas. These values are no longer computed during a production tick.

## Attention At The Substrate Boundary

Spontaneous input generation needs a bounded sensory distribution so it can decide where neutral background activity enters the connectome. That distribution now comes directly from the first four numeric lanes of `NeuronalAttentionWorkspaceDecision`:

- lane `0` supplies visual weight;
- lane `1` supplies auditory weight;
- lane `2` supplies somatosensory weight;
- lane `3` supplies interoceptive weight.

The neuronal winner receives a small gain before normalization. If the neuronal attention circuit is absent or unable to select, the substrate uses an equal distribution across the four receptor classes. It does not restore a previous or legacy winner.

Memory, language, and motor attention lanes are not converted into synthetic sensory input. They remain internal neural activity.

## Removed Production Work

The tick coordinator no longer:

- computes `LimbicRuntimeState` from world scalars and firing summaries;
- computes `BiologicalAttentionRuntime` from semantic and scalar state;
- updates `SimulationState.LimbicState` or `SimulationState.AttentionState`;
- writes central neuromodulator targets or scalar reward-prediction error;
- blends sensory input with `GlobalAttentionBias` from a checkpoint;
- runs the redundant TRN scalar modality selector.

Brain snapshots now write neutral compatibility values for global neuromodulation and reward-prediction error. Local neuronal receptor currents and routed transmitter spikes remain available in structure snapshots and are the only causal neuromodulation path.

## Retired Endpoints

The following legacy endpoints have been removed:

- `/api/v1/limbic`;
- `/api/v1/emotion`;
- `/api/v1/attention`;
- `/api/v1/goal-intent`;
- `/api/v1/motivation-arbitration`.

Their neuronal replacements remain:

- `/api/v1/neuronal-affect-valuation`;
- `/api/v1/neuronal-attention-workspace`;
- `/api/v1/neuronal-executive`;
- `/api/v1/neuronal-motor`;
- `/api/v1/cognition-authority`.

## Causal Tests

Tests verify that visual and auditory neural winners produce corresponding sensory input weights, that the weights remain normalized, and that missing neuronal evidence produces an equal non-semantic distribution. Existing tests continue to prove that incomplete neuronal attention cannot fall back to a legacy winner.

## Remaining Boundary

Historical scalar methods and checkpoint fields remain compiled for offline fixture compatibility but are not called by production ticks. The next deletion pass will migrate the editor and checkpoint schema away from those fields so their dormant implementation can be removed safely. Lateralized visual focus is still decoded at the input boundary from PFC, PPC, pulvinar, and TRN population rates; that decoder must be replaced by an explicitly population-coded left/right attention circuit before the fully neuronal migration can be declared complete.
