# Entry 075 - Neuronal Executive Control

Date: 2026-08-06

## Purpose

This rung removes the writable symbolic planning workspace from live DNNE control. Executive state is now observed from recurrent prefrontal-thalamic-striatal activity, while the winning action remains the result of the neuronal basal-ganglia circuit implemented in the earlier action-selection rung.

The conventional controller cannot inject a goal, choose a plan, change exploration temperature, bias dopamine, alter an inhibitory gate, or override an action channel.

## Circuit Boundary

The executive observer requires measured activity from:

- PFC persistent populations;
- mediodorsal thalamic recurrent support;
- posterior parietal context populations;
- striatal gating populations;
- ACC conflict-monitoring populations.

Temporal-association activity may contribute semantic context, but it cannot supply missing core executive circuitry. Human-readable `ControlMode` labels are ignored.

The observer also receives the already-decoded neuronal attention state and neuronal motor/action state. Its selected action channel must equal the basal-ganglia action winner exactly. If the action circuit is absent, incomplete, or has no winner, executive control fails closed and reports no selected action.

## Temporal Persistence

The runtime counts how many consecutive observed ticks retain the same neuronal action winner. This is a read-only measurement of recurrent persistence. It is not a plan buffer and is never routed back into neurons, synapses, attention, action selection, or motor output.

## Removed Legacy Authority

This rung removes:

- `GET` and `POST /api/v1/admin/reasoning/planning`;
- the writable `PlanningWorkspaceControlRequest` path;
- the entire legacy scalar cognition observer from production ticks;
- the editor's text goal, horizon, branching, exploration, dopamine-bias, inhibitory-gate, and apply-planning controls.

`/api/v1/neuronal-executive` is the read-only replacement. Its snapshot explicitly reports:

- `ReadOnlyMonitor=true`;
- `CanInjectGoals=false`;
- `CanOverrideActionSelection=false`;
- `LegacyPlanningEnabled=false`.

The cognition-authority audit now contains nine neuronal domains, including `executive-control`.

## Causal Tests

Tests pin the following invariants:

- a complete PFC-mediodorsal-parietal-striatal-ACC circuit can expose executive state;
- removing a required structure causes a fail-closed result;
- executive state cannot select an action without an observed neuronal action circuit;
- the reported action always equals the neuronal action winner;
- changing diagnostic text cannot change the decoded result;
- temporal persistence only counts consecutive observations of the same neuronal winner;
- the runtime has no goal-injection or action-override capability.

## Remaining Deletion Work

The large historical `SimulationState` cognition model still contains checkpoint records and an obsolete offline harness for tests that describe old goals, narration, semantic summaries, self models, and intentional-action state. Production ticks do not call that harness, and it cannot authorize movement. The next deletion rung will migrate or remove its historical tests, endpoints, and serialized fields, then delete the dormant implementation. Substrate functions such as transport, persistence, rendering, physics, body chemistry, and service supervision remain conventional software by design.
