# Entry 073 - Strict Neuronal Actuation

Date: 2026-08-06

## Decision

DNNE no longer carries a selectable legacy motor authority. Live movement is authorized only by measured neuronal populations and emitted as numeric bilateral population codes.

The retired `Shadow`, `Assist`, promotion, rollback, and runtime mode-switch paths were deleted. The live decoder no longer accepts the symbolic intentional-action record. Its result depends on motor-circuit firing, basal-ganglia gating, action-channel state, cerebellar support, postural support, sleep, coverage, confidence, and prior neuronal output only.

## Avatar Boundary

The avatar discards every event from a motor structure unless it was generated from the current neuronal motor telemetry as a `population:` event. It does not parse locomotion words, explicit motor directives, or semantic tool commands.

This boundary fails closed:

- missing or stale neural telemetry produces no new motor population;
- sleeping output produces no movement;
- incomplete or low-confidence circuitry produces no movement;
- semantic motor or tool traffic is discarded;
- no symbolic route is restored when neuronal evidence is absent.

Tool use is temporarily unavailable. It will return only after manipulation, grip, reach, and tool-selection populations have a numeric body contract and causal tests.

## Retired Code

- motor-mode administration endpoint;
- runtime motor mode and generation state;
- symbolic-reference agreement and promotion counters;
- mode qualification command and PowerShell launcher;
- symbolic closed-loop and continuous-navigation benchmark executables and launchers;
- semantic locomotion parser in the avatar;
- semantic tool parser in the avatar nervous system.

Historical qualification documents and captured reports remain provenance. They are not executable authority.

## Verification

Tests pin bilateral advance, differential steering, basal-ganglia suppression, hemispheric ablation, sleep silencing, time-sliced population freshness, semantic-command rejection, low-confidence fail-closed behavior, and identical interpretation by the maze and world avatar boundary.

This rung completes neuronal-only actuation, not the entire cognition migration. Entry 074 completes the next affect/valuation authority rung. Planning and goal formation, manipulation and tool use, orienting and gesture, autobiographical continuity, and the learned language adapter remain.
