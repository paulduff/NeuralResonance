# Entry 055 - Embodied Learning Evaluation Roadmap

Date: 2026-07-12

## Purpose

Move the Neural Resonance Engine from a richly instrumented embodied simulation to a system that can make falsifiable claims about learning, memory, adaptation, and transfer.

The target is not to claim biological equivalence. The target is a reproducible anatomy-inspired embodied-agent research platform whose behaviour can be measured, compared, replayed, and improved.

## Guiding Rule

The world supplies observations and consequences. The avatar translates neural motor output into physical action. The brain selects action from perception, memory, internal state, and learned values.

The coordinator may schedule work, enforce safety limits, and expose telemetry. It must not silently choose the task solution on behalf of the brain.

## Phase 1 - Deterministic Survival Benchmark

Create a headless scenario runner for a small, repeatable survival task:

- varied terrain, food, water, shelter, darkness, and predators;
- an avatar spawned from a fixed seed and a known brain snapshot;
- a fixed episode duration or a terminal death/success condition;
- no operator intervention while the episode is being scored.

### Core metrics

- survival time and terminal reason;
- final and minimum health;
- food acquired and hunger reduction;
- shelter reached before night;
- injuries, predator contacts, collisions, and path stalls;
- terrain explored, useful locations revisited, and energy or effort spent;
- elapsed simulation ticks and wall-clock cost.

### Acceptance criteria

1. An episode can be replayed from its recorded seed and input trace.
2. The same build/configuration produces equivalent score bands across repeated runs.
3. The runner emits machine-readable JSON and a compact Markdown summary.

## Phase 2 - Reproducible Episode Records

For every benchmark episode, record:

- world seed, terrain configuration, and scenario version;
- control-program, structure-service, avatar, and world-simulator versions;
- performance profile and all relevant configuration values;
- initial network/synapse snapshot identifier;
- timestamped observation, body-state, action, reward/outcome, and terminal-event traces;
- final network/synapse snapshot identifier and metrics.

Store records as immutable episode artifacts. A replay must use the original inputs rather than attempting to infer them from narrative logs.

## Phase 3 - Establish Baselines

Run the benchmark over a fixed training seed set and a separate held-out seed set.

Compare at least:

- a no-learning baseline with frozen synapses;
- the current learning configuration;
- a simple rule-driven survival baseline, retained only as a comparison point;
- a random or minimally reactive action baseline.

Report median, mean, spread, survival rate, and confidence intervals. A single impressive run is an anecdote, not evidence.

## Phase 4 - Prove Mechanism Contribution With Ablations

Run the identical episode matrix while disabling one mechanism at a time:

- episodic and place memory;
- reward prediction error and dopaminergic teaching;
- synaptic plasticity;
- sleep consolidation and replay;
- visual input;
- body/interoceptive input;
- selected pathway groups, such as hippocampal navigation or basal-ganglia action gating.

A claimed mechanism must have a predicted and measurable behavioural effect when removed. For example, disabling place memory should reduce useful revisits or navigation transfer, not merely change a dashboard value.

## Phase 5 - Learning and Transfer Curriculum

Introduce tasks in this order:

1. Find visible food and consume it.
2. Avoid a predator after direct exposure.
3. Reach shelter as darkness increases.
4. Remember a productive food or shelter location.
5. Adapt when food moves or a route becomes blocked.
6. Transfer the learned policy to unseen terrain seeds.
7. Trade immediate reward against delayed survival, such as preparing for night before hunger becomes critical.

Advance only when the prior task has a stable baseline, a measurable improvement target, and a replayable failure mode.

## Phase 6 - Behavioural Interpretability

For notable decisions and failures, generate a linked trace that answers:

- what the avatar perceived;
- which bodily drives and threats were active;
- which memory, place, object, or prediction influenced the decision;
- which action signal was emitted;
- what consequence followed;
- how the reward, prediction-error, memory, or policy state changed.

The explanation must be derived from the recorded state and signals. Narrative text alone is not evidence.

## Phase 7 - Entity Language Integration

Entity, the separate tiny language model, may become the language-facing layer for the embodied system once it has its own stable evaluation and versioned interface.

Entity must not become an unobserved replacement for the brain, survival system, or motor policy. Its role is to propose language-level interpretations and utterances while DNNE remains authoritative for perception, body state, goals, memory, action selection, reward, and physical consequences.

### Proposed boundary

- Entity receives text and returns structured intent, semantic, question, or dialogue candidates.
- DNNE receives current brain state, verified memories, and communication intent, then requests utterance candidates from Entity.
- The control program validates every candidate against the avatar's current goals, safety state, attention, known memories, and world facts before it becomes an action or spoken statement.
- The world and avatar continue to supply ground truth; Entity is never treated as evidence that an event occurred.

### Acceptance criteria

1. The integration uses versioned request/response contracts and records Entity version, prompt/configuration, candidate output, and acceptance/rejection reason in each episode artifact.
2. An Entity outage or malformed response degrades to existing structured language behaviour without stopping the simulation.
3. Tests show that Entity cannot issue motor actions, alter reward values, write memories directly, or bypass survival constraints.
4. Spoken statements can be traced back to verified perceptions, memories, or explicitly marked hypotheses.

## Architecture Work Required

- Extract a headless world/physics core from the WPF presentation layer.
- Define shared scenario, action, observation, outcome, and episode-result contracts.
- Add snapshot/restore hooks for world state, avatar state, and brain/synapse state.
- Keep per-episode clocks, random sources, and input ordering deterministic.
- Provide an experiment runner that can launch the control program and required structures, apply a profile, execute seed matrices, and collect artifacts.
- Add CI smoke scenarios with a small seed count; run longer benchmark suites manually or on scheduled infrastructure.
- Add Entity only through the Phase 7 language contract, with an adapter owned by the control program rather than direct world or avatar access.

## First Deliverable

Build the **Survival Benchmark Harness** before adding another cognitive region or world feature.

It should run a small matrix of deterministic world episodes, export episode records, calculate the Phase 1 metrics, and compare a frozen-synapse baseline with the current learning configuration. This establishes the measurement foundation for every later claim about cognition.

## Definition of Progress

The system has made meaningful progress when it can demonstrate all of the following on held-out seeds:

1. Better survival than its frozen and reactive baselines.
2. Retention of useful behaviour after a restart and reload.
3. Adaptation when the world changes.
4. A measurable deficit when the relevant memory or learning mechanism is ablated.
5. A replay trace that connects perception, internal state, action, and outcome.
