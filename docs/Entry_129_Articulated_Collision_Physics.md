# Entry 129: Articulated Collision Physics

## Decision

The headless WorldSim now uses BEPUphysics 2 as a collision-query engine around Avatar's neuronal musculoskeletal plant. This is a physical boundary, not a second controller. DNNE neurons still select all motor activity and muscles still produce every attempted movement. Physics only determines how far that attempted articulated pose can move before skin meets a solid surface.

No machine-learning policy, scripted navigation, collision avoidance, target selection, or corrective behaviour was added.

## Graceful transition

The previous live stack was shut down before changing the physical world:

- World report: `C:\Users\User\AppData\Local\NeuralResonanceEngine\world-runs\world-run-1bd9e08398654e49be7d19ab63b45fb7-0000000410-stopped-20260815T102217025Z.json`
- Brain checkpoint: `C:\Users\User\AppData\Local\NeuralResonanceEngine\checkpoints\last-graceful-network-state.json`
- ControlProgram accepted quiesce and shutdown.
- No DNNE listener or simulation process remained active during implementation.

The stack remains stopped after this rung.

## Physical world

The physics scene shares the generated world's dimensions rather than maintaining a second approximation:

- Vertical voxel-terrain faces prevent entry into cliffs and raised ground.
- Shelter walls, lintels, roofs, and the central core are solid.
- Tree trunks use cylindrical colliders with the rendered dimensions.
- Rocks retain their generated size and orientation for collision.
- World limits are closed by physical boundary walls.
- Water remains a non-supporting environment boundary rather than becoming a solid floor.
- Existing terrain support continues to solve the grounded floor surface, avoiding a competing root-motion controller.

## Articulated skin boundary

Avatar is represented by 16 body-attached volumes:

- Pelvis, chest, neck, and head.
- Bilateral upper arms, forearms, and hands.
- Bilateral thighs, shins, and feet.

The volumes follow the measured hip, knee, ankle, shoulder, elbow, trunk, posture, and body-height state. They use the same bone lengths and joint signs as the Blazor renderer.

Every volume is swept continuously from the last accepted pose to the newly attempted pose. Root translation is resolved iteratively at time of impact: motion into the surface normal is removed while legal tangential motion is preserved. Corners can contribute more than one independent contact normal. Heading, axial posture, and the left arm, right arm, left leg, and right leg are then resolved separately. A blocked shoulder therefore cannot freeze a retreating root or the opposite side of the body.

This catches fast movement through thin obstacles as well as a stationary body extending only one arm into a wall. Hands are now physical participants rather than non-load-bearing display probes.

The musculoskeletal plant may continue trying to contract after contact. The externally accepted body stays at the surface while the internal effort remains measurable. Releasing or reversing the effort permits the limb to withdraw.

## Touch and pain

Contacts are coalesced by anatomical region and surface-normal sector before entering the sensory stream. Each retained contact contains:

- Anatomical region and kinematic chain.
- Body-local location and surface normal.
- Force, impulse, tangential speed, contact area, and physical skin tolerance.
- Stable input-source identity and accumulated contact duration.

Impact force is derived from segment mass and closing speed. Continued force is derived from the actual force reported by muscles in the blocked chain. Contact is routed through the existing Merkel, Meissner, Pacinian, Ruffini, and free-nerve-ending transducer populations.

Anatomical region names now preserve hand, foot, limb, head, and general-skin receptor identity when real metre-scale collider coordinates are used. Moderate pressure becomes more nociceptive when it is sustained; greater continuing muscular force produces stronger local mechanonociceptor activity. The resulting pain is spatial and contralateral, not a global punishment signal.

Rejected somatic and physical-body frames are now counted separately. The last rejected payload, including source, body-local position, normal, force, impulse, penetration, tangential speed, area, duration, HTTP status, and validation response, is preserved in the world snapshot and graceful-shutdown report. A single malformed contact can no longer abort delivery of the remaining contacts and proprioceptive body frame for that sampling cycle.

## First live observation and correction

The first run after introducing the articulated skin boundary lasted 2,564.4 seconds and completed 76,826 world ticks with no tick failures. The brain remained connected and all 119 services remained healthy. No penetration was observed, but the avatar travelled only 9.63 metres while accumulating 27,563 collision ticks. At the shelter entrance it remained seated with 21.8% balance error, 30.5 degrees of trunk pitch, a strong lie command, and saturated abdominal effort.

The cause was mechanical rather than neuronal. The first implementation applied the earliest collider hit to the interpolation fraction for the entire pose. One shoulder or trunk contact therefore cancelled root translation, turning, and every unrelated joint. Repeated motor output produced sustained contact instead of leaving a mechanically available route of withdrawal.

The corrected solver now:

1. Advances root motion to the earliest safe time of impact.
2. Projects the remaining root displacement onto the contact plane and iterates through corners.
3. Resolves heading independently from translation.
4. Resolves axial posture and all four limb chains independently.
5. Probes zero-time contacts in the attempted direction so separating motion is accepted.
6. Carries muscle, load, and proprioceptive measurements even when geometry is constrained.
7. Reports root progress separately from joint constraints so a hand contact does not erase locomotor momentum.

These are physical constraints only. No escape action, avoidance policy, navigation rule, or other scripted behavioural authority was added.

## Verification

- Focused world-physics, runtime, and somatic suite: 32 passed.
- Full DNNE suite: 562 passed, 0 failed, 0 skipped.
- Build completed with 0 warnings and 0 errors.
- Continuous test sweep stopped a two-metre motion at a four-centimetre wall.
- Oblique root motion stopped at the wall normal while continuing along its tangent.
- A left-arm-only collision constrained that arm without freezing the right arm or root.
- A limb and root could withdraw from a previously accepted contact pose.
- Two perpendicular walls resolved as a corner without penetration or invalid coordinates.
- Increased left-arm muscle force increased the measured left-arm contact force.
- Articulated contact sources retained the correct anatomical receptor fields.

## Next live observation

Restart the full stack with predators still suspended. Observe quiet standing, walking beside shelter walls, reaching toward a wall, withdrawing the hand, entering the shelter, and approaching terrain steps. Confirm that:

1. No visible body part crosses a solid surface.
2. A blocked limb stops without freezing unrelated motion unnecessarily.
3. Touch appears at the correct side and body region.
4. Continued pressure raises local nociceptive activity with muscle effort.
5. Releasing pressure clears the contact duration and permits withdrawal.
6. World ticks and body-frame delivery remain stable on the laptop.
