# Entry 066 - Rendered Maze Navigation Bridge

Date: 2026-08-05

## Purpose

This rung transfers the proven continuous hippocampal navigator from the headless evaluation into the rendered WPF maze while preserving Dyad's intended embodied flow:

`rendered world observation -> brain navigation session -> M1 motor spikes -> avatar nervous system -> rendered world movement -> new observation`

The WPF simulator does not receive a path or inspect the hidden maze graph. It reports only its current cell, heading, normalized within-cell position, local forward/left/right/rear openings, goal-relative bearing and distance, collision count, and whether it is near the center of its current place cell.

## Shared World Contract

All embodied worlds now use `NavigationCoordinateFrame` from `Shared.Contracts`.

- quarter `0`: positive Z and increasing row;
- quarter `1`: positive X and increasing column;
- quarter `2`: negative Z and decreasing row;
- quarter `3`: negative X and decreasing column;
- positive relative bearing: turn left;
- negative relative bearing: turn right.

The request, observation, decision, M1 descriptor, and response records also live in `Shared.Contracts`. This prevents a simulator and the brain from silently interpreting the same values differently. Protocol `dnne.navigation-control.v2` adds normalized X/Z offsets inside the observed cell. The headless maze and rendered maze both use the same heading quantization, rotation, direction deltas, bearing normalization, and target-center bearing calculation.

## Brain-Side Session

The Control Program exposes:

`POST /api/v1/navigation/decision`

`HippocampalNavigationSessionManager` retains one learned topological map and active target per session and maze. A target decision is held while the avatar turns and travels, then advanced only when the expected target cell center is reached. Unexpected teleportation or departure invalidates the active edge without exposing the hidden world layout.

Each response contains an explicit motor phase and a burst of M1 spike descriptors. The motor phase turns until heading error is within eight degrees, then advances. Heading error points to the exact target-cell center rather than only its cardinal direction, so small angular errors cannot accumulate into corridor-wall drift. Reset requests create a fresh spatial memory for the selected world.

## Rendered Maze Integration

The WPF maze now has a live `Spatial navigator` mode and status line. In that mode it:

1. continues polling normal brain telemetry;
2. builds a local observation through the shared coordinate frame;
3. requests a stateful brain-side navigation decision;
4. translates only returned M1 descriptors into `AvatarDispatchSpike` values;
5. feeds those spikes through the production `AvatarService`;
6. applies the resulting movement through the existing rendered maze physics.

Turn directives use signed differential motor drive with in-place forward cancellation, matching the headless benchmark. The rendered body is allowed to rotate in place for an explicit navigation turn while ordinary simulator behavior remains unchanged when spatial navigation is disabled.

Navigation decisions run on an independent 80 ms dispatcher timer. Full brain telemetry may be slower, but it can no longer delay motor-phase updates long enough for the rendered avatar to overshoot a place-cell center.

Avatar reset and Control Program reconnect both request a fresh navigation session. Disabling spatial navigation clears residual motor drive and restores the normal raw Control Program dispatch stream.

Explicit command-line configuration is reapplied after JSON configuration in the Control Program. This restores the normal precedence rule required for lightweight demonstrations and cluster overrides, where a launch argument such as `--StructureProcessHost:AutoStartEnabled=false` must take precedence over the workstation defaults in `appsettings.json`.

## Verification

The focused navigation suite contains eleven passing tests covering:

- all four canonical heading quarters and row/column deltas;
- positive and negative relative-bearing meaning;
- within-cell displacement correction toward the exact target center;
- deterministic and distinct seeded worlds;
- plan retention until the target place center is reached;
- session reset behavior;
- rejection of impossible non-goal observations;
- M1-only motor descriptors;
- dead-end CA3-style parent replay;
- successful unseen-maze traversal through the production avatar with no collision.

## Boundary

This rung establishes one consistent navigation language across headless and rendered maze worlds. A live rendered run reached 70 place decisions with zero wall impacts and zero recoveries, including while the workstation was under test-suite load. It does not yet standardize every sensory channel, reward scale, clock rate, object ontology, or physics unit across every future world. Those should be added to the same shared world contract before a new environment is allowed to teach the brain.
