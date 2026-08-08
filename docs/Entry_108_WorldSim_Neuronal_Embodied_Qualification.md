# Folded Archive Entry 108: WorldSim Neuronal Embodied Qualification

## Decision

WorldSim is the complete live environment for DNNE qualification. MazeSim
remains useful for controlled navigation experiments, but a maze does not test
the complete body, survival loop, physical manipulation, mixed sensory field,
or persistent open-world consequences.

## Fifth Action Lane

The shared action topology now contains five numeric channels. Four channels
retain bilateral locomotion; the fifth is a physical manipulator drive. The
Control Program selects and smooths that population exactly like the movement
lanes, the avatar transports it as excitatory or inhibitory SpinalCordMotor
events, and movement decoding deliberately maps it to zero translation.

The effector boundary accepts only numeric descriptors and a signed drive. It
contains no tool name, object class, intended outcome, target coordinate, or
semantic action. Missing or stale neuronal evidence decays to no interaction.

## Physical World Consequences

WorldSim resolves manipulator output against current geometry. A latched drive
can collect a reachable pickup, drink from nearby water, or discharge a carried
device at a predator inside a bounded range and view cone. The environment
owns charge consumption, hydration, energy, tissue damage, collision, and
contact physics. It reports physical somatic contacts back through the avatar
instead of telling the brain what the event means.

Walking over an item no longer collects it automatically. The simulator also
does not choose when to use a weapon or which object the brain should value.
Failure to act is observable evidence, not permission for host assistance.

## Inspectable State Stream

WorldSim writes an atomic `dnne.worldsim.state.v1` JSON snapshot. It records:

- process, session, seed, freshness, and Control Program connectivity;
- pose, distance travelled, terrain coverage, and separate locomotor and
  manipulator population dispatch counts;
- left, right, and manipulator drive;
- interaction attempts and physical successes;
- accepted retinal, cochlear, body, and somatic frames;
- energy, hydration, tissue integrity, shelter, sleep, collisions, and tick
  failures.

The stream is instrumentation only. It cannot inject a motor decision or alter
the simulation.

## Live Gate

`tools/burnin-worldsim.ps1` starts the visible WorldSim if necessary and watches
one uninterrupted session. A passing run requires fresh brain telemetry,
neuronal motor dispatch, displacement or terrain exploration, all four physical
sensory return paths, at least one manipulator attempt, and zero new tick
failures. It preserves samples and a human-readable summary while leaving the
world visible for observation.

The top-level `tools/run-neuronal-only-qualification.ps1 -Mode Live` now uses
this WorldSim gate and emits protocol `dnne.neuronal-only-qualification.v2`.
Offline preflight still cannot claim embodied qualification.

## Scientific Limit

A passing gate establishes that the implemented neuronal outputs can traverse
the avatar, affect a physical world, and receive raw consequences. It does not
prove intelligent exploration, successful survival, object understanding,
consciousness, or generalization. Interaction successes and survival trends are
recorded for later behavioral qualification; they are not manufactured by a
fallback policy.

## Laptop Live Evidence

On 2026-08-08 the complete 90-structure bilateral brain, editor, and visible
WorldSim were started on the 4-core/8-thread laptop. All structures remained
healthy and WorldSim reported no simulation tick failures. With the editor
closed to release rendering pressure, the three-minute observation produced
fresh auditory, physical-body, and somatic traffic, neuronal motor traffic,
one physical interaction attempt, and 0.032 metres of displacement.

The run did not qualify embodied behaviour. Rendered visual frames frequently
arrived 265-950 ms after capture under full CPU saturation, and the former
250 ms preview warning threshold incorrectly rejected them as brain input.
Preview warning freshness and neural sensory usability are now separate: the
UI still marks frames over 250 ms stale, while the neural path accepts frames
up to the existing one-second hard drop limit. The burn-in harness was also
made compatible with Windows PowerShell 5.1 and now identifies the actual WPF
application process rather than its `dotnet run` parent.

Motor evidence from that run could not distinguish locomotor population events
from the fifth manipulator lane. Runtime state and both qualification layers now
record those populations independently and require actual locomotor output.
The low displacement remains behavioral evidence for future training; body
gain was not increased to manufacture a passing result.
