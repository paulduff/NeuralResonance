# Entry 087 - Delete Scalar Body, Affect, And Neuromod Overlays

Date: 2026-08-06

## Purpose

This rung removes the remaining host-authored descriptions that could be
mistaken for body cognition, emotion, attention, cerebellar prediction, or
brain-wide chemistry. DNNE retains the body and world as physical substrate,
but interpretation and action must emerge from neuronal populations,
synapses, local transmitters, and measured circuit activity.

## Deleted Runtime Models

The Control Program no longer defines or stores `BodySchemaRuntime`,
`InteroceptiveCoreRuntime`, `PainProtectionRuntime`, `BodyPresenceRuntime`,
`BiologicalAttentionRuntime`, `LimbicRuntimeState`, `EmotionRuntimeState`, or
`CerebellumRuntime`. Their update formulas, getters, routes, reset state,
checkpoint fields, restore paths, and public diagnostic projections are gone.

This specifically deletes host-authored semantic outputs such as dominant
needs, felt-state labels, pain motor directives, protective goal keys,
emotion names, attention winners, neuromodulator targets, and cerebellar
correction scores.

## Preserved Substrate

Raw body and environment measurements remain. Forward velocity, turn rate,
contact, directional touch, pain, motor drive, hunger, health, darkness,
shelter, and world outcomes are sensor or physiology facts. They are
transduced into spikes for S1, vestibular, hypothalamic, limbic, cerebellar,
and related populations; they do not directly choose an action.

Structure-produced body-schema, interoceptive, affect, attention, sleep,
cerebellar, and motor diagnostics remain because they measure actual
population and synaptic activity. Read-only decoders may summarize those
measurements for people and tools, but their results cannot be written back
as neural state or motor commands.

## Neuromodulation Boundary

The central `GlobalNeuromodState`, reward-prediction-error property, update
method, sleep neuromodulator override, and checkpoint fields were deleted.
Sensory, outcome, body, language, and spontaneous spikes no longer carry a
host-generated modulation context. Each structure derives modulation and
plasticity from locally received transmitter release and receptor state.

## Functional Diagnostics

The functional circuit audit now marks body schema, interoception, affect,
and cerebellar prediction from recent activity in their required neuronal
structures. Affect support may use the read-only neuronal valuation decoder;
attention inhibition uses measured TRN competition from the neuronal
attention workspace. No deleted scalar record can make a circuit appear
active.

## Compatibility Policy

The five legacy body/cognition routes were removed. Old checkpoint JSON can
still be deserialized because unknown fields are ignored, but those fields
have no destination and cannot recreate the deleted models. Obsolete motor
intent compatibility fields were also removed from language responses.

## Verification

- Control Program Release build: passed with zero warnings.
- WPF editor Release build: passed with zero warnings.
- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Tests: 319 passed, zero failed.
- Cortical functional benchmark: PASS, 100% overall and in all categories.

The full solution build wrapper exceeded its three-minute timeout while
traversing the large structure project set. The affected projects and shared
structure runtime were covered by the focused builds and test build above.

## Remaining Boundary

Sleep ATP and pressure remain modeled as physical metabolism, while measured
neuronal sleep circuitry alone authorizes state transitions. Curriculum and
human-readable audit labels remain external training and inspection tools.
The next audit must inspect those remaining host services and the tick
contract itself for obsolete compatibility fields, without deleting genuine
physiology, clocks, sensors, or test instrumentation.
