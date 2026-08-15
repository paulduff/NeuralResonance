# Folded Archive Entry 125: Rung 6 Live Embodied Test

## Purpose

Test the complete neuronal DNNE after retiring the aggregate umbrella services.
The run used the authoritative Blazor WorldSim with seed 317 and the full local
bilateral brain on the 16 GB laptop.

## Topology and startup

- 119 concrete structure types registered successfully.
- 238 bilateral structure instances entered the local runtime.
- All 119 registered structure types remained healthy with zero non-OK services.
- The first launch exposed stale build output for both `NucleusBasalis`
  hemispheres. The incomplete run was stopped, the renamed project was rebuilt,
  and the clean restart reached full health.
- Initial startup was slow under memory and scheduling pressure. After JIT and
  service warm-up, a measured ten-second interval reached 25.14 brain ticks per
  second and 30.3 world ticks per second under automatic load shedding.

## Final captured state

Captured at `2026-08-12T15:03:21.7946192Z` before graceful shutdown.

| Measure | Result |
| --- | ---: |
| Brain tick | 77,396 |
| World tick | 91,112 |
| World elapsed time | 3,104.2 seconds |
| Healthy registered structures | 119 / 119 |
| Non-OK services | 0 |
| Snapshot age | 8 ticks |
| Distance travelled | 584.806 world units |
| Terrain cells visited | 300 |
| Neuronal motor dispatches | 70,931 |
| Locomotor dispatches | 43,135 |
| Manipulator dispatches | 27,796 |
| Interaction attempts | 49 |
| Successful interactions | 0 |
| Collision contacts | 12,888 |
| Physical deaths | 2 |
| World tick failures | 0 |
| ControlProgram stderr bytes | 0 |
| Blazor editor stderr bytes | 0 |

The avatar was viable after the second automatic respawn. At capture it had
full tissue integrity, 3,208,394 J stored energy, and 56.7 percent hydration.

## Observations

The embodied loop remained intact:

`brain -> avatar -> world -> avatar -> brain`

Retinal, cochlear, physical-body, and somatic frames reached the brain. The
brain initially remained behaviorally quiet while neuronal confidence and
competition accumulated. Manipulation became the first selected behavior, and
the body attempted to interact with a target that was out of reach. Locomotion
later won the competition and produced sustained grounded movement with no
air-walking or vertical-velocity fault.

The run therefore demonstrates delayed emergent action selection rather than a
disconnected or scripted world. The brain/world transport stayed connected,
the neuronal motor boundary became active, and no symbolic or ML policy was
used to authorize behavior.

## Findings

1. Rung 6 is runtime viable. Explicit neuronal populations can replace the
   retired umbrella services without breaking startup, telemetry, or embodied
   dispatch.
2. Warm-up matters. Early quiescence was not a stall; useful competition and
   behavior appeared only after sustained neuronal activity.
3. Action selection can become persistent. Manipulation continued while its
   target was out of reach, and all 49 recorded interaction attempts failed.
4. Locomotion works, but collision avoidance is inadequate. The avatar accrued
   12,888 collision contacts and suffered two physical deaths during the run.
5. Automatic physical respawn worked and did not reset or destabilize the
   brain. Tissue returned to full integrity while the same world and neuronal
   runtime continued.
6. The laptop is at its practical resource limit for the full local topology.
   Automatic load shedding kept the system stable, but a cluster or the RTX
   workstation will provide more simultaneous structure updates per brain tick.

## Next corrective rung

- Feed failed out-of-reach manipulation back into neuronal action competition
  as short-term inhibitory evidence for the same action channel.
- Strengthen locomotor approach and orienting circuits when an attended target
  is outside interaction range.
- Strengthen collision, nociceptive, vestibular, and threat feedback so repeated
  impacts suppress the current motor pattern and recruit avoidance.
- Distinguish predator damage from terrain collision damage in the persistent
  world event record.
- Re-run seed 317 and require successful target approach, at least one useful
  interaction, fewer collisions per unit distance, and survival without an
  automatic respawn.
