# Entry 142 - Crossed-Leg Capture Point

## Observation

During the 18 August 2026 motor-training run, both legs crossed beyond their
normal lateral ordering while both feet retained load. The editor reported a
stable 114 mm support margin even though forward locomotion from that pose
should have produced a forward fall.

The static support hull was geometrically broad because the crossed feet had
passed to opposite sides. Two missing physical facts made that state appear
safer than it was:

1. Body-local collider coordinates omitted root-forward velocity from the
   extrapolated centre of mass.
2. The normal dynamic stability allowance assumed that an unobstructed capture
   step remained available.

## Mechanical correction

The balance plant now measures lateral clearance and anatomical ordering of
the two loaded feet. As the legs close and cross:

- achieved forward root velocity contributes to the extrapolated centre of
  mass;
- the dynamic next-step reserve decays to zero; and
- momentum-driven fall torque follows the extrapolated capture point rather
  than an opposing static centre-of-mass lever; and
- sustained forward locomotion commits a gravitational fall when the capture
  point passes beyond support.

Standing still does not create fictitious momentum, and ordinary uncrossed
gait retains its bounded dynamic allowance. This is a mechanical body rule;
it neither selects an action nor scripts a recovery for the neuronal system.

## Evidence

- Live run report:
  `world-run-1705956816fa43cb8b5c5b1214a37fb0-0000014148-stopped-20260818T162959049Z.json`
- Crossed-leg forward-motion regression commits a forward fall.
- Crossed legs at rest do not invent movement.
- Existing normal-gait dynamic-balance regression remains in force.
