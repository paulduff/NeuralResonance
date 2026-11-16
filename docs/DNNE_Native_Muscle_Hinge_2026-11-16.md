# Native muscle path on a constrained hinge

The preceding free-root study verifies native point velocities, force/torque
application and per-step force clearing. It does not establish that the same
path forces drive a constrained joint correctly. This candidate tests that
boundary before connecting the qualified dynamic muscle to a native hinge.
It does not change the live Avatar, brain, World scene or any saves.

## Mechanical fixture

The isolated scene contains one immovable root and one free revolute child,
with zero gravity, drive stiffness/damping, joint friction and body damping.
The child has 2 kg mass, isotropic central inertia 0.15 kg m² and a centre of
mass 0.3 m from the x-axis hinge. Actual represented float values are retained.
Its generalized inertia is `I_x + mass * centreOffset²`, not central inertia
alone. Small separated sphere colliders avoid contact interference.

The root's attachment is `(0, 0.3, ±0.25)` metres. The child's local attachment
is `(0, 0.4, 0)`. Every applied force comes from the already qualified path
geometry; no extra joint torque or moment-arm multiplier is added. The native
joint has exactly one degree of freedom and zero drive force limit.

The 24 cases combine initial angles -0.6, 0 and +0.6 radians, both root attachment
sides, 1 and 0.125 ms physical steps, and zero or 10 N path tension. Each fresh
fixture receives a declared 0.25 rad/s initial joint velocity and one unforced
registration step. This pose initialization is confined to the synthetic fixture.
It is not a permitted production reset or balance controller.

Each case then takes one forced step and one unforced step. The three steps are
counted explicitly: 72 physical steps for the complete matrix. Only the fixture
owns `Physics.Simulate`; component-registration waits do not step physics.

## Prospective acceptance

With native joint angle `q`, child attachment radius `r` and measured path length
`L`, the analytic child point is `(0, r*cos(q), r*sin(q))`. The derivative is
`(0, -r*sin(q), r*cos(q))`. The path derivative determines the moment arm and
torque: `tau = -T*dL/dq`. Power is `tau*qdot`.

Checks require:

- Native attachment, centre-of-mass and pivot poses to agree with joint angle.
- Native angular, centre and point velocities to agree with `qdot`.
- Geometric moment arm, torque and power to agree with the analytic hinge.
- Joint speed after the force to agree with the actual rounded native torque
  impulse divided by the generalized inertia.
- The subsequent unforced step to preserve joint speed and advance angle.
- The fixed root to retain its position, orientation and zero velocities.

Bounds are fixed before executing the study: 2e-6 m in geometry, 2e-6 m/s in
linear/point velocities, 2e-6 rad/s in angular/joint speeds, 3e-6 N m in torque,
2e-5 W in power, and 2e-6 in the fixed-root quaternion comparison. Different
units are stored and tested separately. Force conversion is independently checked
within 3e-6 N by the saved-data analysis.

Joint angle has a 2e-6 rad roundoff allowance. For the forced step only, its
exact constant-acceleration angle is compared with an additional prospective
first-order position-integration envelope `0.5*abs(tau/I)*dt²`. This permits the
expected single-step truncation of a first-order position integrator; it is not
a long-trajectory convergence claim. The unforced angle check receives only the
roundoff allowance. All native states must remain finite.

Initial, forced and final body states and joint coordinates are preserved,
including cases that fail a criterion. `analyse-native-muscle-hinge.py`
independently recomputes the geometric, torque, velocity, angle and fixed-root
checks from the saved values, verifies every matrix member and requires the
original bounds. Recomputed versus recorded arithmetic has a 1e-12 reproduction
bound; that does not replace any physical acceptance bound.

## Frozen execution and next boundary

`prepare-native-muscle-hinge.py` requires the verified free-root review, its D:
copy, unchanged qualified geometry/muscle/coupling source, clean pushed Git
source, Unity 6000.3.19f1 and the declared Python executable. The visible
`start-native-muscle-hinge-window.ps1` runs 75 geometry/motor regressions, builds
a separate player using `MuscleHingeBuild.BuildWindows`, then runs the native
matrix and independent saved-data analysis. Source, binaries, imported project
inputs, logs and results are preserved and backed up to D:. No live monitoring.

Authoring compilation against the installed Unity assemblies and Python/
PowerShell syntax checks pass. No native hinge outcome follows from compilation.
Consult the launch record before identifying pending work.

After this boundary passes, the next step is a clock-owned native hinge adapter
for the existing causal neuronal-event/muscle plant, with complete geometry
observations and exact effort receipts. That requires a coupled trajectory
comparison and feedback delivery before live Avatar promotion. Calibrated
anatomical paths, fiber/tendon parameters, afferent physiology and whole-body
recovery remain open; synthetic hinge success cannot substitute for them.

## Launch record

`native-muscle-hinge-20261116-de7da9`, frozen revision
`301fbfd4fc37f9ee1add8abb1176fc5dd4058b5e`, was launched in independent visible
PowerShell task `DNNE-Native-Muscle-Hinge-c0f66b95c7c7`. All 1,598 initial files
plus manifest were verified on D: and rechecked at launch. The preparer verified
the reviewed native point-force prerequisite and unchanged qualified cores.
This is the only pending operator completion report. No test/build/native
outcome has been inspected; do not monitor or infer qualification from launch.

## First build failure and metadata repair

The operator reported `de7da9` FAILED. All 75 regression cases passed, but Unity
ignored `MuscleHingeBuild.cs`: its `.meta` GUID contained 33 characters rather
than 32. The build log explicitly rejects that metadata and subsequently cannot
find the execute-method class. The saved editor compiler response file includes
the older build entries and omits this one. The player build entry and hinge
physics never ran; this is not a failed mechanical criterion.

`review-native-hinge-build-20261116.py` verifies all 1,989 C:/D: files
(94,447,393 bytes), 1,594 Git files, test binaries and complete 75-case method
coverage. Its derived review preserves the build-workspace compiler response,
compilation graph, editor/runtime assemblies and original bad metadata. Those
seven files plus manifest have a verified D: copy at
`artifacts/native-hinge-build-review-20261116`. Original capture manifest:
`C0D4B50F9D5AC843FDDF46E040A09D81C95BC5DF37CC49A2F9672F3D12ADA376`.

The GUID is corrected to `1c5e2ff46336504cab0c6e2cfcbe5b11`.
`unity_metadata.py` now checks nonzero, unique 32-character hexadecimal GUIDs
and stable metadata sidecars for all C# sources. Both native muscle preparers
validate working inputs before packaging and frozen inputs before launch, storing
the metadata report among the protected package files. The new static preflight
rejects the original malformed capture and accepts all 45 current metadata files
and 32 scripts. It is an asset-identity check, not full YAML or Unity validation.
The hinge runtime, build-entry C#, analytic comparison and physical bounds are
unchanged. Preserve the original failed package; use a fresh frozen retry.

The corrected package `native-muscle-hinge-20261116-5a98f8`, frozen revision
`200d8042ac2622fb693dabae5c0ced473832fb83`, is launched in independent visible
PowerShell task `DNNE-Native-Muscle-Hinge-1fb99fb6333b`. All 1,601 initial files
plus manifest are D: verified and rechecked at launch, including the new frozen
metadata report. It is the only pending operator completion report. No build or
physical outcome has been inspected; do not monitor.

## Completed corrected hinge review

The operator reported `5a98f8` complete. All 75 regression cases and all 24 native
hinge cases passed, with all 72 physical steps accounted for. The frozen
independent analysis reproduces exactly without changing any bound. Review tool
`tools/review-native-hinge-20261116.py` verifies all 2,316 files in each C:/D:
copy (185,616,628 bytes), all 1,596 Git files, test/player binaries, all 32 compiled
Unity C# inputs, asset metadata and executable identities. The capture manifest is
`F8DFBA6403E37A762A735C339AA89378620E5B29F30BB3FE8568D3EC87FA77FE`.
Derived `artifacts/native-hinge-review-20261116/review.json` is D: backed.

Worst native joint-speed impulse error is 1.36e-8 rad/s; unforced clearing changes
speed by zero. At the approximately 1 ms physical step, the worst angle error is
5.81e-6 rad within the prospective first-order envelope; at approximately 0.125 ms
it is 1.06e-7 rad. Endpoint linear-velocity error falls from 1.79e-6 m/s to
4.11e-8 m/s. These are single-step checks, not coupled-trajectory qualification.

The actual Unity float steps promoted to double are 0.0010000000474974513 s and
0.0001250000059371814 s. The native coupling must use those represented intervals,
not relabel them exactly 1 or 0.125 ms. The smaller interval is slightly above the
muscle solver's 0.125 ms maximum and therefore still needs at least two substeps.
No test or simulation ran during this review. No experiment remains pending as
of this review; later launch records supersede that statement.
