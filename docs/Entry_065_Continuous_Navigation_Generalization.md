# Entry 065 - Continuous Navigation Generalization

Date: 2026-08-05

## Purpose

This rung extends the embodied loop from two discrete choices into sustained sensorimotor navigation. Dyad must discover and traverse hidden seeded mazes through the intended path:

`brain spatial policy -> M1 spikes -> avatar -> continuous maze physics -> body/place/object/outcome feedback -> brain spatial policy`

The benchmark does not provide the maze layout or shortest path to the navigator. The hidden layout is used only by the world for collision physics and post-run scoring.

## Architecture

The reusable `AvatarMazeEnvironment` generates deterministic odd-sized mazes and owns continuous position, heading, wall collision, goal detection, and local sensory observations. Each observation contains current pose, four egocentric open/blocked probes, goal-relative bearing and distance, and collision state.

The `HippocampalNavigationRuntime` builds its own topological graph one place at a time. It models:

- hippocampal place binding through visited-cell traces;
- CA3-like sequence retention through parent links;
- retrosplenial transformation between egocentric probes and allocentric heading;
- prefrontal preference for novel passages biased toward the goal;
- replay-based backtracking when a local branch is exhausted;
- basal-ganglia-style selection of explicit turn and forward motor directives.

Every directive is emitted as M1 spikes and integrated by the production `AvatarService`. Movement is then applied through continuous kinematics and circle-versus-wall collision physics. The resulting body, object, place, and outcome signals return through the avatar before the next spatial decision.

Run the benchmark with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\run-continuous-navigation-benchmark.ps1"
```

Timestamped JSON and Markdown reports are written under `artifacts/continuous-navigation`.

## Initial Baseline

The first complete run passed with 100.0% generalization across three unseen seeded mazes:

| Seed | Shortest path | Actual transitions | Efficiency | Backtracks | Explored places | Collisions |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 317 | 40 | 40 | 100.0% | 0 | 40 | 0 |
| 911 | 56 | 74 | 75.7% | 9 | 65 | 0 |
| 2027 | 28 | 60 | 46.7% | 11 | 49 | 0 |

Aggregate measurements:

- mean path efficiency: 74.1%;
- mean distance efficiency: 74.1%;
- collisions: 0;
- brain M1 spikes: 3,680;
- avatar motor events: 3,680;
- learned backtracks: 20;
- every scenario formed both hippocampal navigation traces and persistent avatar place memories.

Verified baseline report: `artifacts/continuous-navigation/continuous-navigation-20260805-204646.md`.

## Interpretation

Seed 317 happened to align the novelty and goal-distance biases with the shortest route. Seeds 911 and 2027 forced the system into dead ends, after which parent-sequence replay returned it to earlier junctions and allowed exploration to continue. This is useful evidence that the benchmark exercises adaptive recovery rather than a fixed directional script.

The zero-collision result shows that local wall probes, heading transformation, explicit motor selection, avatar integration, and continuous world collision boundaries agree geometrically.

## Boundary

This benchmark proves local-map construction, dead-end recovery, continuous collision-free movement, place-memory growth, and generalization across several unseen static mazes. It does not yet prove navigation with moving hazards, noisy or missing sensors, changing goals, delayed feedback, memory consolidation across process restarts, or transfer into the rendered WPF maze. Those are subsequent rungs.
