# Entry 139: Unilateral Sole Support and Lateral Fall

## Observation

The Entry 138 observation showed both feet carrying support for almost the
entire run, even while the body spent 77.3 percent of its measured time in the
`righting` phase. Code inspection found that the articulated body assigned
load to both feet whenever the avatar root was marked `grounded`.

That global flag says that some part of the body is supported by the world. It
does not prove that both soles touch the support plane. Consequently, lifting
one leg laterally widened the support polygon with a non-existent second foot
and could prevent the sideways fall that the measured center of mass required.

## Repair

Each sole now earns ground reaction independently from the resolved articulated
collider geometry. A sole is supporting only when its lowest skin surface is
within 25 mm of the local support plane. The standing, crouching, and blocked
reaction budget is distributed only across those measured supporting soles.

When one sole is raised:

- its plantar load and pressure field become zero;
- it contributes no probes to the support polygon;
- the contacting sole receives the available plantar reaction;
- the existing two-axis balance dynamics evaluate the center of mass against
  that unilateral support polygon.

When the raised sole returns to the plane, bilateral support is restored by
geometry rather than by a posture label.

## Balance consequence

The balance model already represents the horizontal center of mass and support
polygon in both lateral and fore-aft axes. A lateral mass displacement outside
single-sole support therefore produces signed roll acceleration and a sideways
fall. Recontact can enlarge the polygon; opposing limb, trunk, and ankle
activity can move the mass or pressure center. Those actions must be produced
by neuronal motor pathways.

No host-authored counterbalance, pose correction, gait policy, or ML controller
was added.

## Regression coverage

- Unilateral hip abduction raises the selected sole more than 80 mm while the
  opposite sole remains on the support plane.
- The raised sole receives zero load and emits no ground-contact probe.
- The opposite sole carries the available plantar load.
- Raised-leg mass with single-sole support commits a real lateral fall and
  develops non-zero roll.
- Returning the articulation to neutral returns both soles to the support
  plane.
- Existing ankle, crouch, lying, collision, and neuronal righting tests remain
  valid under measured support semantics.
- The complete Release suite passes 696 of 696 tests.

## Next observation

Run the predators-suspended full stack and exercise a sustained unilateral hip
abduction. Acceptance requires the lifted sole load to fall to zero, the
support polygon to collapse onto the stance foot, and one of three physical
outcomes: neuronal counterbalance, sole recontact, or a lateral fall. The run
report should not show bilateral foot support while one sole is visibly clear
of the terrain.
