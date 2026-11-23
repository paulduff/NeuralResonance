# Curved muscle paths for knee support

The native population run `b3cf65` remains pending and has not been inspected.
This work addresses a separate requirement for standing: retaining the geometry
and force reactions of muscle paths that wrap around a bone.

## Existing Avatar and source evidence

`AvatarArticulatedBody` already has bilateral Iliopsoas/GluteusMaximus,
GluteusMedius/AdductorGroup, Hamstrings/Quadriceps and ankle antagonists.
The existing quadriceps joint uses a declared 0.042 m moment arm. Preserve these
groups; their existence does not qualify the newer named native path mapping.
The original native tendon fixtures use opposing straight lines around each
joint and explicitly identify themselves as diagnostic rather than anatomical.

The pinned Rajagopal source at OpenSim models revision
`e0cee6cbcf56f74731e841668ef668556d140ce9` includes a left femoral knee-extensor
`WrapCylinder` named `KnExt_at_fem_l`, radius 0.025 m, length 0.1 m, with an
explicit femoral pose. Its quadriceps paths also include moving patellar
attachments and constrained patellofemoral kinematics. A cylinder alone does
not reproduce that complete source mechanism. The protected source XML,
catalog and provenance are retained in the new study package.

OpenSim documents wrapping surfaces and automatically calculated wrap contacts
in its [Muscle Editor documentation](https://opensimconfluence.atlassian.net/wiki/spaces/OpenSim/pages/53090145/Muscle%20Editor).
Its [MomentArmSolver](https://github.com/opensim-org/opensim-core/blob/main/OpenSim/Simulation/MomentArmSolver.cpp)
uses applied path forces to calculate generalized force. Our implementation
below is an independent analytic lateral-cylinder reduction; it does not copy
or claim numerical equivalence to OpenSim's wrapping implementation.

## New geometric component

`AvatarCylinderMusclePath` measures a frictionless, uniform-tension path composed
of an entry tangent, a helical surface arc and an exit tangent. Unequal axial
endpoint positions are retained. Unrolling the lateral surface gives horizontal
path distance H and axial displacement z, with length sqrt(H² + z²).

The caller supplies continuous endpoint azimuths from its declared kinematics.
These specify a winding branch and must agree with the actual observed radial
positions. The component never chooses a shorter side, silently unwraps a seam
or clamps a point onto a cylinder. When that declared route disengages smoothly
from the surface, the original straight-line geometry applies. Endpoints on or
inside the surface, inconsistent route phases, full revolutions, nonfinite
geometry and surface contacts beyond the finite width are rejected. End caps
are not modeled.

Each tendon endpoint receives its tangent tension. The guide receives two
equivalent surface-resultant forces at the tangent contacts, giving the total
surface force, moment and power. These are equivalent rigid-body loads, not
local contact pressures. Contact velocities are the rigid guide's material
velocities, not velocities of the sliding tangent locations. Thus the tendon
cannot gain free work from a moving or rotating guide. The complete local guide
pose, radius, width, route direction, endpoint declarations, geometry identity
and provenance participate in the immutable fingerprint.

The native adapter must eventually transform the declared guide/endpoints and
authenticate sample ownership. The geometry class alone does not authenticate
body poses or a physical clock. No live owner selects this component yet.
The existing straight-path, muscle, neuronal, learning and transport equations
are unchanged. A generated namespace-only Unity copy is included and checked;
no editor presentation or live body selection changes in this component stage.

## Qualification protocol

The independent visible PowerShell window runs 52 new geometry controls and
75 existing straight-path, dynamic-muscle, causal-plant and solution-manifest
regressions: exactly 127 managed cases. Controls cover closed-form planar/skew
paths, total force/moment, rigid-motion work, zero tension, declaration identity,
fixed winding, smooth engagement, invalid input and exact generated Unity
parity.

Twenty-four cases preserve complete inputs and outputs for a separate
80/110-digit mpmath calculation. That calculation differentiates total path
length with respect to endpoint translations, guide translation and guide
rotation; it does not reuse the native tangent-force formulas. Both endpoint
forces, guide force/moment and length rate are compared. Both route directions,
two declared radii, straight and curved paths, near-surface endpoints, axial
offsets and moving/rotating guides are included.

Prospective limits: length 2e-12 m, length rate 2e-12 m/s, force 1e-10 N,
moment 1e-10 N m; independent precision agreement 1e-60. All records and original
bounds are retained on failure. Author builds and syntax checks do not execute
these tests or references. Package preparation preserves clean pushed source,
exact method multiplicities, Python/mpmath and managed runtime identities.
The reference uses a separate environment at
`D:/DNNE-build-work/cylinder-reference-env-20261123`, with the explicitly pinned
[mpmath 1.3.0 wheel](https://pypi.org/project/mpmath/1.3.0/), SHA256
`a0b2b9fe80bbcd81a6647ff13108738cfb482d481d826cc0e02f5b35e5c88d2c`.
The wheel, PyPI metadata and provenance have a verified D: backup. Preflight
matches every installed Python source against that wheel. The existing numerical
environment used by prior/pending studies is unchanged.

Even a pass establishes geometric force/work accuracy only. Actual native
guide-force delivery, anatomical fit, patella coupling, muscle population
calibration, neuronal authority and standing remain unqualified. The next body
integration must use the guide reactions, not substitute an extra joint torque.

## Remaining standing work

Use the completed population/native result when Paul reports it; extend source
hip/knee geometry with explicit guide ownership and patella coupling; assign
appropriate muscle populations rather than duplicating TA everywhere; then
close the original neuronal sensory/motor loop in the observable, paused-on-open
world. This component is one required mechanical step toward that unchanged goal.
