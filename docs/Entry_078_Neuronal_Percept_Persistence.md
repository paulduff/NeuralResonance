# Entry 078 - Neuronal Percept Persistence

Date: 2026-08-06

## Purpose

This rung removes controller-owned predictive perception and persistent object beliefs from the production boundary. Percept selection, confidence, novelty, and persistence are now reported only from distributed neuronal ensemble diagnostics.

## Neural Evidence

`NeuronalPerceptionDecoder` reads feature, binding, salience, familiarity, and hippocampal-index activity from the participating structures. Its persistence value is recurrent binding measured inside the structure populations; novelty and familiarity likewise come from structure-local neuronal traces. The controller observes these values but cannot write a winner or hold one after the circuit stops supporting it.

`GET /api/v1/neuronal-perception` now includes a read-only interpretation containing the winning ensemble, confidence, coverage, persistence, novelty, and an optional current-tick language annotation. Language labels remain metadata attached after neuronal binding. They cannot create a percept, create a memory, alter confidence, or survive into a later tick without fresh neuronal evidence and a fresh annotation.

## Removed Causal Paths

Auditory, collision, body-state, and outcome input handlers no longer call a scalar predictive model. Their responses no longer contain controller-computed `PredictiveSurprise` or `PredictiveCue` fields. Inputs encode receptor or feedback spikes; subsequent neuronal population activity determines perception.

The following legacy surfaces have been deleted:

- `SimulationState.ObservePredictivePerception`;
- `SimulationState.GetPredictivePerceptionSnapshot`;
- `SimulationState.GetPersistentPerceptsSnapshot`;
- `GET /api/v1/predictive-perception`;
- `GET /api/v1/persistent-percepts`;
- predictive-perception and persistent-percept fields in the main state diagnostics;
- persistent-percept checkpoint export and import.

Old checkpoint JSON may contain `PersistentPercepts`; the current serializer ignores that unknown property and cannot restore it.

## Causal Tests

Tests pin the absence of legacy writers, diagnostic fields, routes, and checkpoint properties. They also prove that a current neuronal winner can receive a read-only language annotation, that annotation cannot alter the underlying decision, and that stale annotation text is not carried into a later neural tick.

## Remaining Scaffold

The large historical `SimulationState` still contains dormant scalar cognition types used by offline compatibility benchmarks. Production ticks do not invoke the compatibility cognition harness. The next deletion rung should remove symbolic attention and global-workspace calculations from that harness and its tests, then continue through goal/planning and narration state until the compatibility model itself is gone.
