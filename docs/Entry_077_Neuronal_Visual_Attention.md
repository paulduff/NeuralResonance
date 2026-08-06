# Entry 077 - Neuronal Visual Attention

Date: 2026-08-06

## Purpose

This rung removes the controller-owned visual winner-take-all state. Lateralized visual focus is now a read-only decode of bilateral neural populations and cannot be written by an API request or restored from a checkpoint.

## Neural Circuit

The decoder requires left and right populations for all four participating structures:

- posterior parietal cortex (PPC);
- prefrontal cortex (PFC);
- pulvinar;
- thalamic reticular nucleus (TRN).

PPC and pulvinar provide the largest terms in each field drive, PFC contributes recurrent top-down support, and the corresponding TRN population subtracts inhibitory drive. Retinotopy is contralateral: right-hemisphere activity represents the left visual field and left-hemisphere activity represents the right field.

The decoder fails closed to `neutral/M` when any required hemisphere is absent, neural activity is too weak, or the field drives tie. It does not reuse a prior winner. `SustainedSelectionTicks` only reports how long the same neural winner has remained observable.

## Sensory Boundary

This boundary was tightened further by Entries 094 and 095. The structured
visual endpoint and host-provided left/right salience no longer exist. Raw
pixel fields are transduced into retinotopic ON/OFF ganglion spikes:

- left-field saliency drives right-hemisphere visual populations;
- right-field saliency drives left-hemisphere visual populations.

Frame input cannot set focus. The next population snapshots determine whether
either field wins.

## Removed Legacy Authority

The following have been deleted:

- `VisualAttentionRuntime`;
- `SimulationState.RegisterVisualAttentionObservation`;
- `SimulationState.AdvanceVisualAttentionWta`;
- the controller-side `ComputeVisualHemifieldTopDown` routine;
- `POST /api/v1/admin/input/visual-attention`;
- `VisualAttentionInputRequest`;
- visual-attention checkpoint export/import state.

Old checkpoints may contain the removed JSON property, but it is ignored and cannot influence a new run.

## Telemetry

`GET /api/v1/neuronal-visual-attention` exposes the read-only decision and its evidence. The main state frame and editor now show neural field drive, bilateral TRN suppression, selection margin, circuit coverage, confidence, and observed persistence. The cognition-authority audit includes visual attention as its tenth neuronal domain.

## Causal Tests

Tests prove contralateral selection, fail-closed behavior for incomplete circuits and ties, immediate switching without a controller hold, contralateral sensory encoding, and the absence of writable or checkpoint-restorable legacy authority.

## Next Boundary

Visual attention is now neuronal. Other dormant symbolic state families still exist in `SimulationState` for historical telemetry and offline fixtures. The next deletion rung should remove controller-owned predictive-perception and persistent-percept state from causal paths, leaving percept identity and persistence to the distributed neuronal ensemble and synaptic-memory decoders.
