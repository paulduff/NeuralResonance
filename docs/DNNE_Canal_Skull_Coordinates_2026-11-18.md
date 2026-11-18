# Measured head motion to the published canal coordinates

Status: authored, not yet launched. Motor `5bdd81` and sensory rest `cd9fba`
are still awaiting completion reports and have not been monitored.

The published canal mechanics now passes its declared numerical comparison,
but its three input numbers are not Avatar pitch, yaw and roll acceleration.
This change supplies an isolated conversion from the original measured skull
endpoints, preserving their physical clock and identities. It does not select
the mechanics in the live Avatar or advance a brain/body/world service.

## Source interpretation

The retained [Ifediba 2007 paper](https://pmc.ncbi.nlm.nih.gov/articles/PMC3005417/)
paragraph P20 explicitly reconstructs head angular acceleration as a sum of
the three prime directions multiplied by their scalar components. Those
directions are nonorthogonal. Consequently the input to equation 26 is found
by solving the matrix with the printed direction vectors as columns. Taking
three dot products would not implement that reconstruction. P45 supplies the
three printed vectors; the paper body and both paragraphs are frozen with the
qualification package, bound to the earlier source SHA-256.

There is a source precision/consistency issue. The squared lengths of the
printed HC, AC and PC directions are 0.999869, 0.999901 and 1.055307. In
particular, PC is about 2.73% longer than a unit vector, beyond the rounding of
three components to three decimal places. The implementation preserves these
literals and the forcing coefficients together. It does not normalize PC,
invent corrected anatomy or claim that this source discrepancy has been
resolved. `PublishedPrintedRightLabyrinth1` names this literal interpretation.
Any later corrected anatomical data must be a distinct, reviewed parameter set.

The source uses posterior/right-lateral/superior coordinates; Avatar uses
right/up/forward. A polar vector maps as `S(x,y,z)=(-z,x,y)`, with determinant
-1. Angular velocity and acceleration are axial vectors, so their map is
`det(S)*S(x,y,z)=(z,-x,-y)`. This ensures that the source-frame rigid velocity
`omega cross offset` equals the mapped native rigid velocity. Positive native
pitch therefore maps to negative source-right rotation; positive native yaw
to negative source-superior rotation; positive roll to source-posterior.
These signs follow the declared coordinates, not a fitted neuronal response.

## Input ownership

`AvatarCanalSkullInput` composes the existing validated `AvatarSkullKinematics`
owner. It computes `(omega_world_now - omega_world_previous)/physical_seconds`,
then expresses that interval-average vector in the current measured skull
basis and converts it to source-prime coordinates. It does not subtract angular
velocities expressed in two different local frames or silently normalize the
float-derived basis. Original head bytes, both endpoint hashes, sample identity
and availability time remain attached to each result.

The first sample has no derived acceleration. Later acceleration is available
only at the final endpoint; it cannot retroactively drive the interval just
measured. Exact retries return the same receipt. Gaps, changed identities,
conflicting retries and invalid rigid geometry are rejected. A numerical
failure after accepting a skull endpoint retains that skull prefix and the
last successful angular receipt, and faults the new owner without resetting it.
No clocked canal integrator or sensory cell is advanced by this input adapter.

## Qualification

The visible window will run 100 focused cases: 24 new basis, rotation-sign,
rigid-velocity, provenance, retry and failure controls; plus the existing 76
canal, physical-clock, skull and bundle regressions. Synthetic oblique vectors
provide an analytic counterexample to treating prime directions as orthogonal.

It then replays 603 original records from the completed native Unity head
capture `unity-skull-20261118-0ada47`: three material points over 201 endpoints.
Every existing skull receipt must reproduce exactly. The 600 subsequent angular
inputs are independently reconstructed at 60 and 90 decimal digits using
Cramer's rule, while the native adapter uses scaled pivoted LU. Six independent
analytic reference controls run first. Native/reference comparison uses an
absolute allowance of 1e-9 rad/s² plus 1e-12 relative; decimal precision uses
1e-45 absolute plus 1e-45 relative. Native solve backward error remains at the
existing 1e-12 limit. All three points on the same rigid head must yield the
same angular components while retaining their separate original provenance.

The old Unity capture and all earlier source kernels remain unchanged. No new
Unity capture, organ trajectory, neuronal firing or live service is requested.
Independent source conversion accuracy will not establish continuous-motion
derivative accuracy, anatomical registration, corrected PC scaling, local bundle
deformation or physiological balance. Connecting these inputs to the mechanical
clock and then to the free sensory cells is subsequent integration work.

Authoring builds of the probe and full test project pass with zero warnings or
errors. Python/PowerShell syntax, the exact 100-case inventory, original 603-row
layout and protected source comparisons pass static checks. No test, adapter
evaluation, independent solve or model ran during authoring. Frozen preparation
and backups use D:, followed by an independent visible PowerShell window without
live monitoring. The two new D: directories share one physical drive.


Initial frame package `canal-frame-20261118-dc4bbc` is NOT LAUNCHED. Static
compatibility review after its initial preflight found the expected managed
runtime came from the smaller UnityPhysicalSampleProbe (NETCore only), whereas
NativeMotorBridgeProbe also requires ASP.NET through the structures runtime.
No model/test or launch attempt occurred. All 2,427 files plus manifest, frozen
source and explicit NOT_LAUNCHED receipt are preserved in its verified D: backup.
The corrected freezer binds the complete reviewed canal-probe runtime and proves
the original skull runtime is an exact unchanged core subset, with only the
already-reviewed ASP.NET directory added. Host, switches and every original
core file must agree. A fresh package is required; no frozen input is edited.
Motor `5bdd81` and sensory `cd9fba` remain the only pending completion reports,
both unmonitored. Full goal remains active and unfinished.
