# Entry 080 - Remove The Symbolic Cognition Harness

Date: 2026-08-06

## Purpose

This rung deletes the controller-owned cognition rehearsal path that could
manufacture goal, motivation, planning, emotion, narration, self-model, and
world-model state without activity flowing through the neuronal services.
DNNE no longer carries a deterministic symbolic survival simulation beside its
neuronal brain.

## Deleted Runtime Surface

`SimulationState.ObserveCognitiveRuntime` is deleted. It was an offline
compatibility method that called a long sequence of scalar update routines in a
fixed order. Production ticks did not invoke it, but tests and the deterministic
survival benchmark could still use it to produce apparently cognitive behavior
without neuronal evidence.

The deterministic survival benchmark, Dyad replay adapter, route mapping, and
their tests are deleted. The following legacy telemetry routes are also gone:

- `/api/v1/narration`;
- `/api/v1/speech-intention`;
- `/api/v1/cognitive-language-workspace`;
- `/api/v1/inner-speech-loop`;
- `/api/v1/intentional-action-loop`;
- `/api/v1/self-monitoring-loop`;
- `/api/v1/autobiographical-self`;
- `/api/v1/autobiographical-continuity`;
- `/api/v1/narrative-self-model`;
- `/api/v1/identity-boundary`;
- `/api/v1/room-state`;
- `/api/v1/inhabitance`.

Tests now pin both route absence and the absence of the synthetic cognition
entry point. Old scalar behavior tests were removed with their only driver;
keeping them would require recreating the authority path this migration exists
to eliminate.

## Retained Neuronal Evidence

The neuronal perception, attention/workspace, memory, sleep consolidation,
language grounding, affect valuation, executive, and motor endpoints remain.
The cortical functional benchmark remains the primary compact qualification
gate, alongside the full unit and integration suite and embodied world runs.

## Next Deletion Boundary

Several conventional records and private update methods still compile because
checkpoint, diagnostics, language-observation, or UI compatibility code reads
them. They no longer have a general synthetic tick driver or public legacy
route. The next rung removes those call roots and stored records in dependency
order, migrating any useful display to read-only neuronal decoder output.

