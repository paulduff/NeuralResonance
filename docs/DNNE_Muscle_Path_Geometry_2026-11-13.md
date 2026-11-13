# Muscle attachment geometry candidate, 13 November 2026

Avatar's existing 19 joint profiles name 38 muscle groups and declare nominal
moment arms. `AvatarMuscleJoint.Advance` derives normalized lengths from joint
angle; it does not provide physical attachment paths or calibrated fiber/tendon
lengths in metres. Its independent length and torque approximations cannot simply
be used as the physical geometry of the qualified dynamic muscle candidate.

`AvatarMusclePath` adds an isolated geometric foundation. It does not change the
live `AvatarArticulatedBody`, `MusclePlant`, joint drives, editor or saves. The
qualified dynamic muscle and causal motor coupling cores are unchanged.

## One geometry for length and force

A declaration contains the muscle identity, body geometry fingerprint, explicit
provenance and ordered attachment/via-point identities, each with a body identity
and local position in metres. A SHA256 fingerprint includes every declaration
field and the geometry version. Signed zero has one numerical identity.

The physical owner must transform these local attachments and measure their
world positions and point velocities at the same completed physical endpoint.
The pure geometry library checks identities, completeness and finite values; it
does not authenticate a native pose, timestamp or anatomical calibration.
Arrays are copied into immutable snapshots, and observation ordering cannot
change the declared path order.

For segment displacement `d = p[i+1] - p[i]`, length is `|d|` and direction is
`u = d / |d|`. Total path velocity is the sum of
`u dot (v[i+1] - v[i])`. A uniform tensile force `T` applies `+T*u` to its first
attachment and `-T*u` to its second. Each intermediate attachment receives both
neighboring reactions; omitting that force would violate the mechanical balance.
There is no separate moment-arm multiplier. Moment arms emerge from the actual
attachment geometry relative to joint axes.

These forces imply zero net internal force and torque and the virtual-power
identity `sum(F[i] dot v[i]) = -T * pathVelocity`. Both sides and their residual
are exposed. This identity concerns the path/body boundary, not total metabolic
energy or the finite-step energy error of a coupled solver. The existing causal
coupling has separate finite-step work accounting.

Collapsed segments, nonfinite observations and negative/nonfinite tension are
rejected; no geometry reset, clipping or silent removal of a via point occurs.
Scaled norms avoid premature squared-distance overflow/underflow. Direction is
normalized at unit scale so rounding a subnormal length does not change tension
magnitude. Extreme-value cases are numerical guards, not anatomical scales.

## Avatar and Unity parity

`tools/prepare-unity-muscle-paths.py` generates
`UnityWorld/Assets/NRE/Runtime/MusclePathGeometry.cs` with only the namespace and
source-hash header changed. Do not edit that generated file directly.
The DNNE test assembly compiles both versions and compares their results and
fingerprints exactly, as well as verifying the full generated source.
This does not qualify Unity's compiler, native point measurements, float precision
or PhysX force application. No native adapter is selected by this change.

## Prospective visible-window checks

The new 31-case suite covers analytic straight and bent paths, both reactions at
a via point, rigid motion, frame transformations, hinge torque/length derivative
at four signed angles, independent central length differences, internal force/
torque balance, power, reversed paths, immutable state, geometry fingerprints,
missing/invalid observations, collapsed paths, invalid/zero tension, norm extremes
and exact generated Unity parity. The fixture comparisons generally use an
absolute `2e-13` bound; the central finite-difference derivative uses `2e-10`.
These bounds are established before inspecting a run.

`prepare-muscle-paths.py` freezes clean pushed source after checking the verified
coupled numerical review and preserving the qualified cores. The visible
`start-muscle-paths-window.ps1` runs those 31 cases plus 27 causal motor tests,
16 dynamic muscle tests and the solution-manifest check: 75 cases in total.
It requires exact method/count coverage, preserves source and binary hashes,
and backs results up to D:. No timing comparison, neuronal simulation or live
Unity run is part of this package. Inspect only after the operator reports it
complete. Compilation during authoring is not a test pass.

## Required next integration

1. Qualify an isolated native articulation adapter against known attachment
   positions, angular point velocities, applied forces/torques and body work,
   including realistic native float precision and physical-step refinement.
2. Bind path snapshots and efforts to the existing physical clock, session,
   body geometry epoch, same-step effort receipt and single physics owner.
   Stale or mismatched geometry must fail before any force or step is committed.
3. Source and calibrate local attachment sites, wrapping geometry, fiber lengths,
   tendon slack lengths and unit pools for the represented anatomy. The existing
   reference tibialis-anterior parameters must not become every muscle's anatomy.
4. Keep total musculotendon path length distinct from muscle fiber state. Tendon
   stretch is not the fiber stretch sensed by a spindle. The canonical spindle
   state repair alone does not qualify its independent numerics or afferent firing.

Fixed body-attached via points represent ideal frictionless guides. Sliding
surface wrapping and changing contact topology are not implemented. Geometric
correctness alone does not establish anatomical realism, physiological motor
control, whole-body performance or Avatar recovery.

## Launch record

Frozen package `muscle-paths-20261113-2d0fa4`, revision
`31a6f457ecb29dffc15467e2e84cc8a73d6deceb`, was launched in independent visible
PowerShell task `DNNE-Muscle-Paths-7a1d62f9f57f`. All 1,581 initial files plus
manifest were verified on D: and rechecked at launch. Authoring compilation
finished with zero warnings/errors; Python and PowerShell syntax checks passed.
The 75 test outcomes have not been inspected. This is the only pending operator
completion report. No live monitoring, native simulation or live selection.

The operator subsequently reported the run complete. All 75 cases pass and the
full capture, source, binaries and method coverage verify. The derived review is
D: backed; this package is no longer pending. See
`DNNE_Native_Muscle_Paths_2026-11-13.md` for the next native force boundary and
its prospective criteria. No native pass follows from the managed suite.
