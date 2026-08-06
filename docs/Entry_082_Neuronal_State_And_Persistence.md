# Entry 082 - Neuronal State And Persistence

Date: 2026-08-06

## Purpose

This rung removes the checkpoint and diagnostics surfaces that preserved the
deleted symbolic cognition scaffold. A checkpoint may preserve neural,
synaptic, physiological, transport, curriculum, and audit state, but it may
not serialize a second rule-based mind beside the neuronal system.

## Removed State

The checkpoint schema no longer exports or imports scalar records for:

- planning, goals, motivation, language intent, and intentional action;
- cognitive language workspace, inner speech, narration, or speech intent;
- self-monitoring, identity, autobiographical and narrative self-models;
- room, place, attention-affordance, preference, and atmosphere models;
- promise, continuity-journal, maintenance, working-memory, and dream
  summaries;
- keyword-driven biological teaching state.

The corresponding fields are also absent from `/api/v1/state`. Older JSON
checkpoints remain loadable because their unknown legacy properties are
ignored, but those properties can no longer rebuild symbolic runtime state.

## Neuronal Dashboard

`brainBehavior` is now a compact read-only dashboard with authority
`MeasuredNeuronalDecoders`. It reports physical body state and sensory gates,
then exposes only measured neuronal visual attention, grounded language, and
motor decisions. Language output is visible only when the distributed
grounding circuit reports both a grounded reference and speech authorization.

This dashboard is an observation surface. It does not select goals, infer
intent, write memory, or dispatch action.

## Regression Boundary

Automated tests pin the absence of every deleted checkpoint and diagnostics
property. They also require the dashboard and language section to declare
their neuronal authorities and forbid legacy goal, motivation, and action
records inside `brainBehavior`.

Remaining internal scalar records are now isolated from persistence and public
authority. Subsequent compile-driven rungs remove their update roots, fields,
and types without restoring a compatibility fallback.
