# Entry 090 - Delete Simulator Steering Authority

Date: 2026-08-06

## Purpose

This rung makes the rendered maze and world passive environments around the
neuronal brain. A simulator may constrain a body through geometry, expose
receptor facts, and apply physical consequences. It may not choose where the
body should turn, decide that the brain is stuck, inject an escape action, or
use privileged map knowledge to drive motor populations.

## Deleted Maze Authority

The maze no longer contains its retired spatial-navigation client, timer,
session state, checkbox, or `/api/v1/navigation/decision` request. The removed
path sent complete map coordinates and a goal coordinate to a host navigator,
then converted its semantic directive into M1 spikes.

The host orienting reflex is also physically deleted. Wall-contact history,
no-progress timers, collision-burst thresholds, escape aggressiveness,
cooldowns, hemisphere latching, and automatic recovery stimuli are gone.

A wall impact now produces only:

- collision rejection or physical sliding along the struck surface;
- physical health and score consequences used by the qualification world;
- raw touch, pain, hunger, health, motion, and motor receptor telemetry;
- a collision spike packet whose hemisphere identifies the physically struck
  side, or both hemispheres for a frontal contact.

The side is no longer inverted into a host-selected escape direction.

## Deleted World Authority

The world no longer contains:

- reactive wall-avoidance steering;
- clearance-scored navigation filtering;
- corner sidestep and obstacle-probe movement;
- no-progress detection, stuck episodes, stuck damage, or forced death;
- automatic escape, hard-unstuck, and path-recovery counters or controls;
- semantic about-face escape;
- host target selection and orienting lock for food, shelter, or weapons;
- command-text scanning used to bias turn direction;
- exploration novelty rewards generated from the simulator's hidden terrain
  map.

Clear-spawn search remains only in world initialization. It cannot run during
an avatar lifetime and cannot alter an embodied action.

## Unified Motor Rule

Both rendered worlds now use the same causal path:

1. numeric neuronal spikes update bilateral motor drive;
2. `AvatarService.PublishActionOutput` projects that drive through body
   kinematics;
3. forward speed changes position subject to collision physics;
4. neuronal differential drive changes body heading directly, including a
   turn in place;
5. the environment reports resulting sensory facts and consequences.

The host no longer suppresses a stationary neuronal turn by converting it
into a head-only animation. Missing neuronal motor evidence therefore means
stillness; valid differential motor evidence means a body turn.

## Preserved World Functions

The following remain outside the brain because they are substrate rather than
agency:

- terrain, walls, hazards, pickups, and collision geometry;
- swept movement and collision sliding;
- ray and pixel generation for receptor input;
- health effects caused by actual hazards or impacts;
- explicit human reset and initial spawn validation;
- qualification score, trail, and learning telemetry;
- transport, rendering, clocks, and process lifecycle.

These functions may describe what happened. They cannot select an action,
goal, target, turn sign, or recovery strategy.

Entry 091 subsequently deletes the semantic outcome channel itself. Damage,
effort, progress, novelty, safety, threat, and reward labels are no longer
accepted as neural input.

## Authority Tests

`SimulatorAuthorityBoundaryTests` pins physical absence of the navigator,
semantic steering, no-progress recovery, escape-side chooser, and target-lock
methods. It also verifies that both rendered worlds pass neuronal turn rate
directly to body heading and that shared wall sensing exposes only physical
proximity transduction.

## Population Size

No circuit population was resized. The problem was duplicate host authority,
not evidence of insufficient neuronal capacity. Population changes remain
dependent on firing, collision, capacity, lesion, and embodied qualification
measurements.

## Verification

- Maze simulator Release build: passed with zero warnings.
- World simulator Release build: passed with zero warnings.
- Tests: 304 passed, zero failed, zero skipped.
- Simulator authority source audit: no retired steering or recovery symbols.
- Cortical functional benchmark: PASS, 100% overall and in every category.
