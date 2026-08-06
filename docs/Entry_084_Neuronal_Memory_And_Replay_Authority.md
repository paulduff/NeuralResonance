# Entry 084 - Neuronal Memory And Replay Authority

Date: 2026-08-06

## Purpose

This rung removes public and persisted authority from the conventional memory,
world-model, reinforcement, and offline dream stores. DNNE already has
distributed neuronal perception, synaptic memory, sleep consolidation, affect
valuation, action selection, and executive decoders. The older dictionaries
must therefore be unable to survive a restart or influence live chemistry.

## Removed Surfaces

The action-memory, world-learning-map, place-memory, episodic-memory,
unified-event-memory, semantic-memory, dopamine-learning, and
dream-consolidation endpoints are removed. Their records are also absent from
`/api/v1/state`, the functional circuit audit, exported checkpoints, and
checkpoint restoration.

Checkpoint persistence no longer includes central engrams, relational schemas,
object memories, learned world transitions, action traces, map entries,
episodic traces, semantic traces, or dopamine-value traces. Structure snapshots
and their synaptic state remain the durable memory substrate.

## Live Authority

Conventional dopamine-value estimates no longer alter the global neuromodulator
wire during normal updates, sleep-profile changes, or checkpoint import.
Handcrafted offline dream consolidation is no longer called. Sleep state and
replay authority remain with `DistributedNeuronalSleepCircuit`; recall remains
with `PersistedSynapticRecallEnsembles`.

The internal legacy stores are cleared whenever a checkpoint is imported so
stale process-local data cannot accompany restored neuronal state. Their
physical fields, writers, and record types are isolated and scheduled for the
next compile-driven deletion rung.

## Regression Boundary

Tests require every removed diagnostics and checkpoint property to remain
absent, including trace collections. Older checkpoint JSON can still be read;
unknown legacy fields are ignored and cannot rebuild the deleted authority.
