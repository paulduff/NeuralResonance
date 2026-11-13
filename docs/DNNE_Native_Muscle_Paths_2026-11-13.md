# Native muscle point-force qualification candidate

The reported geometry package `muscle-paths-20261113-2d0fa4` passes all 75 cases:
31 geometric/analytic/generated-parity checks and 44 causal/dynamic regressions.
The offline review verifies 1,971 files per C:/D: copy (94,443,694 bytes), 1,577
Git source files, all binary hashes, exact source parity and every test method.
The derived `artifacts/muscle-path-review-20261113/review.json` is D: backed.
Original capture manifest:
`31EF510EBBF58ECC13CF8B0BD0B2203424073CC86CB296F3EBBD20E871429A68`.
No tests were rerun during that review. The geometry package is no longer pending.

The next boundary is native point measurements and force application. This
candidate builds a separate Unity player with a single qualification component
in an otherwise empty scene. It does not load Avatar, contact DNNE, change the
live World scene or alter saves. The generated muscle geometry core is unchanged.
Free articulation roots isolate the force API from joint constraint solvers.
Passing this fixture will not qualify constrained joints or a full native muscle
trajectory.

## Prespecified matrix and checks

`MusclePathNativeQualification` runs 24 cases: two or three bodies (straight path
or bent path with a via reaction), identity or rotated bodies, native float steps
of 10, 1 or 0.125 ms, and zero or 10 N tension. Each case starts with fresh free
articulation roots, declared masses and isotropic inertias, nonzero linear and
angular velocities, zero gravity/damping and separated small colliders. Isotropic
inertia removes gyroscopic coupling from the angular impulse oracle.

Only the qualification component owns `Physics.Simulate`; scripted physics is
selected before any fixture exists. Each case takes exactly two physical steps:
one with the declared attachment forces and one with none, to check that force
accumulators clear. Component-registration and destruction waits do not step
physics. Expected total: 48 steps. The full represented float step is recorded
as a double; no rounded nominal clock is substituted.

Checks compare:

- Native transformed points with an independent double quaternion transform.
- Native point velocities with `v_com + omega cross (point - com)`.
- Rounded native force vectors with the qualified double path forces.
- Resulting linear/angular velocities with `v + F*dt/m` and
  `omega + torque*dt/I`, using the actual supplied float force and measured mass
  and inertia. Force application uses each attachment's world position.
- The unforced second step with unchanged linear/angular velocity.
- Force/point-velocity power with the path-shortening power, while retaining all
  initial and post-step body states and errors.

Before inspecting results, bounds are fixed at 2e-6 m for point transformation,
2e-6 m/s for point/linear velocity, 2e-6 rad/s for angular velocity, 3e-6 N for
force conversion, 2e-5 W for native power conversion and 1e-11 W for the double
geometry power identity. These are local single-precision boundary checks, not
an accuracy claim over long mechanical trajectories. Nonfinite native states fail.

Unity documents that
[GetPointVelocity](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/getpointvelocity)
uses a world point and includes angular motion.
[AddForceAtPosition](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/articulationbody/addforceatposition)
accepts world force/position and creates both force and torque. This fixture
uses only `ForceMode.Force`; it never mixes force and impulse modes.

## Evidence and execution

The new runtime and editor build entry compile against 149 installed Unity
assemblies without errors/warnings. This is authoring compilation, not a Unity
player build or native physics result. Python/PowerShell syntax is checked.

`prepare-native-muscle-paths.py` requires clean pushed source, the verified
75-case review, unchanged qualified core source, exact generated geometry and
Unity 6000.3.19f1. `start-native-muscle-paths-window.ps1` opens an independent
visible PowerShell window, runs the 75 regression/geometry cases, then builds the
isolated Unity player in a separate writable project. It preserves the imported
project inputs and player binaries and verifies the compiled C# against the
frozen source. Native cases preserve initial, post-force and final JSON even
when a numerical criterion fails. The runner verifies the full parameter matrix,
session, step count and original bounds before reporting diagnostic completion.
All results receive a verified D: copy. No live monitoring.

Remaining requirements include constrained articulations, ownership/clock-bound
production path observations and effort receipts, native coupled trajectory
refinement, actual anatomical paths/wrapping, calibrated muscle fiber/tendon
parameters and afferent physiology. The live Avatar remains unchanged.

## Launch record

`native-muscle-paths-20261113-d9097a`, frozen revision
`c9eff12ace1bc35f7bfa2cfc7777f3a377a64cf5`, was launched in independent visible
PowerShell task `DNNE-Native-Muscle-Paths-ea468e8fcb13`. All 1,589 initial files
plus manifest were verified on D: and rechecked at launch. This is the only
pending operator completion report. Neither the Unity build nor native test
outcome has been inspected; no live monitoring or qualification is inferred.
