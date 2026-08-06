# Entry 085 - Delete Conventional Memory And Replay Runtime

Date: 2026-08-06

## Purpose

This rung physically removes the conventional memory, world-model, schema,
counterfactual, and central replay implementation isolated by Entry 084. The
runtime can no longer construct a second, dictionary-backed account of what
the distributed neuronal structures have learned.

## Deleted Runtime

The Control Program no longer contains central engram and schema banks,
episodic or semantic trace stores, object and place memories, action-value and
dopamine-learning dictionaries, learned world transitions, dream
consolidation, counterfactual evaluation, or their update and snapshot code.
Their record types, checkpoint fields, administrative routes, dispatch replay
keys, delivery callbacks, and transport counters are also gone.

The one-argument sleep-homeostasis overload was deleted. The remaining
production method requires a `NeuronalSleepConsolidationDecision`; incomplete
or missing neuronal evidence cannot enter or exit sleep through a scalar
threshold fallback.

## Neuronal Memory Contract

Durable learning lives in structure-local synapses. Memory diagnostics decode
synaptic tags, eligibility traces, potentiation, and distributed ensemble
strength from structure snapshots. Sleep replay is generated within structure
topology and coordinated by the neuronal sleep circuit. The host observes and
modulates transport during sleep but does not select semantic memories or
dispatch a central replay batch.

The term `EngramStrength` remains only for a measured synaptic-ensemble value.
It is not an object in a host-owned memory bank.

## Compatibility

Old checkpoint JSON can still be read because unknown properties are ignored,
but removed fields cannot recreate conventional state. New checkpoints contain
the neuronal sleep state and persisted structure/synapse state only. This is an
intentional one-way architectural migration.

The editor now reports distributed neuronal memory and structure-local replay
instead of zero-valued central engram, schema, and replay-dispatch counters.

## Regression Boundary

Tests require the retired methods and route handlers to be physically absent.
The removed HTTP routes return `404`, and reflection checks pin the sole sleep
entry point to the two-argument neuronal-authority signature. The retained
neuronal memory and sleep tests exercise synaptic learning, replay topology,
lesion behavior, and the no-fallback authority boundary.

Verification on the migration rung completed with zero build warnings, all 317
tests passing, and a 100% cortical functional benchmark score across stream
separation, learning, persistence, and adaptive output gating.
