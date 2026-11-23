# Named muscle paths in native Avatar — 23 November 2026

The full-body force owner previously required 38 synthetic joint-specific
flexor/extensor paths. A human muscle can span several joints and must retain
one identity, one state and one tendon tension. This candidate admits an
explicitly declared named inventory through `named-avatar-tendon-wire-v1`.
The existing `full-avatar-tendon-wire-v3` keeps its 38-path requirement and
native joint-profile checks; configuration serialization fields are unchanged.

Named paths require unique, ordered muscle names, attachment provenance,
distinct ordered point identities, finite coordinates and at least two body
frames. Unity checks that all frames exist and all points are representable
before claiming the body. It uses the original polyline/tension evaluator,
including both reactions at via points. No muscle is split into independent
copies to satisfy a joint list, and no torque shortcut is introduced.

Every request and receipt must retain its selected protocol and the complete
configuration-bound inventory. The host validates named geometry version,
original anchor coordinate bits, point order, body identity and provenance,
then reconstructs each canonical path fingerprint against the actual native
geometry hash. Uncertain commands still fault without a retry. Observations
retain the same physical step owner. No biological or learning equation
changes, and the new mode is not selected by the live editor/brain connection.

## Completed prerequisite

The lower-leg frame study `581e1f` passed all 288 tests, eight initial-equilibrium
references and 44 coordinate comparisons at 80/110 digits. Both 2,852-file
copies, frozen source, binaries and runtime were reviewed. Its ten named
paths and original 44 points are frozen inputs, not recalculated or manually
adjusted for the new test. Foot scale remains a declared shin-ratio proxy;
anatomical fit and omitted wrapping remain open.

## One independent PowerShell study

`fresh_motor_epoch_study.py prepare --named-muscles` freezes 28 new interface
cases plus 331 existing host/body/head/motor regressions. New cases cover
inventories of 1, 3, 10 and 80 muscles, a three-body path with both via-point
reactions, altered or missing commands, rehashed but foreign anatomical
receipts, protocol mixing, lost acknowledgments and the original causal plant's
wait for every declared motor source. A protocol peer is used for these checks;
they do not qualify Unity physics.

After those 359 cases pass, the same visible window builds a separate Unity
player and runs twelve actual native cases: quiet, each of the ten mapped
lower-leg muscles at a prescribed 40 N tension, and quiet with extra endpoint
inspections. Each case retains the original full fifteen-link body, uses the
reviewed no-joint-friction setting, starts paused, takes one passive interval,
then seven force intervals at the original 0.005-second native float step.
No DNNE/live editor connection or old saves are used. These are test forces;
no neuronal or muscle-cell activity is claimed.

Before any force, the new body must match the preserved original bone geometry,
gravity and entire initial pose. All requests, replies, applications and three
original head observations per endpoint are saved. The declared total is
96 physical steps, 108 endpoints, 324 head receipts and 18 extra inspections.

The independent saved comparison checks all declarations against the reviewed
mapped points and all geometry against the old body. Only the explicit geometry
version/path set and process-local native object IDs may differ. It independently
reconstructs actual new body/path hashes rather than treating a normalized
comparison view as the live identity. Quiet motion must match all nine original
quiet states exactly, and extra inspections must preserve quiet motion.

For all 3,696 point applications, it checks point/body identity, each mapped
local point against the preceding native pose, path length, both adjacent
segment reactions and the exact final float force cast. Prospective bounds are
2e-6 m for native transformed point coordinates, 2e-12 m for path-length
arithmetic and 1e-10 N for force arithmetic. The position bound accommodates
the declared float transform boundary; it is not an anatomical error allowance.
Each named force case must produce more than 1e-6 rad of joint response relative
to quiet; all measured responses remain in the report. These bounds are fixed
before execution and failures preserve all evidence.

The host/test authoring builds passed with zero warnings/errors. All eight
suite inventories and Python/PowerShell syntax were checked without model or
test execution. Unity compilation, the 359 cases and actual native outcomes
remain pending the independent window. This advances real attachment/force
delivery, not calibrated strength, neuronal authority, coupled trajectory
accuracy or standing. Identified motor-unit populations and full muscle/tendon
geometry must follow before attempting those broader claims.

Launched `D:/DNNE-build-work/named-muscle-native-20261123-afc890`, frozen at
`a7fd7ce734552cf3aaf69be0185da65e989936d0`. Both initial 2,281-file copies plus
manifest and preflight verified. Interactive task
`DNNE-Named-Muscle-Native-e710b2963054` started the independent visible
PowerShell window. No postlaunch process/output/status inspected; this is the
only pending report. The prerequisite mapping and equilibrium runs are reviewed.
