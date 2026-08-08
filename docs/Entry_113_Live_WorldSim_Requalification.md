# Folded Archive Entry 113: Live WorldSim Requalification

Date: 2026-08-08

## Purpose

Run the complete local DNNE brain with the visible WorldSim after the neuronal
runtime, WorldSim, deployment, and audit rungs. The editor and MazeSim remained
closed so the laptop could devote its available resources to the brain and
embodied world.

## Preflight

- Entity tests: 142 passed.
- DNNE tests: 409 passed.
- Targeted neuronal authority tests: 96 passed.
- Circuit audit: 90 of 90 structures `OK`.
- Seeded cortical benchmark: 100% overall.
- Release and Debug solution builds: zero warnings and zero errors.
- Dyad wire contract SHA-256:
  `32619bdec2e9a9b9c3a0a4dd0088bfb868511f5bf89fa701d713006596252b0c`.

## Live Evidence

The launcher reached strict readiness with all 90 registered structures and
180 bilateral service instances online. At the start of observation the
ControlProgram reported zero non-OK services and fresh telemetry. Startup tick
time was approximately 1.66 seconds while processes settled, then recovered to
approximately 37 milliseconds during the run.

The visible three-minute WorldSim qualification produced:

| Measure | Result |
| --- | ---: |
| Neuronal motor dispatches | 81 |
| Locomotor dispatches | 52 |
| Manipulator dispatches | 29 |
| Distance travelled | 0.026 m |
| Newly visited terrain cells | 1 |
| Physical interaction attempts | 2 |
| Physical interaction successes | 0 |
| Accepted retinal frames | 76 |
| Accepted cochlear frames | 103 |
| Accepted physical-body frames | 89 |
| Accepted somatic frames | 89 |
| WorldSim tick failures | 0 |

## Result

The strict embodied qualification result is `FAIL` because movement did not
reach either the one-metre distance threshold or the two-new-cell exploration
threshold. Every required sensory stream was accepted, neuronal motor and
locomotor output reached the world, interaction attempts occurred, all brain
services remained healthy, and WorldSim recorded no simulation failures.

This is a useful partial result. The brain-avatar-world loop is connected and
causal, but sustained action selection or locomotor output strength remains too
weak for qualification. The gate must remain unchanged. The next engineering
work should diagnose motor persistence, selected-action dwell time, output
inhibition, and avatar force integration using the recorded samples rather
than treating laptop load or transport failure as the primary cause.

The visible WorldSim was left running after evidence capture for direct
observation.
