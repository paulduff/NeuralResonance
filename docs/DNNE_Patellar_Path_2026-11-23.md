# Source kneecap motion and force transfer

The existing Avatar hip, knee and ankle groups remain intact. This component
supplies the moving patellar attachments required by the pinned anatomical
quadriceps paths. It does not select a new live body, change muscle recruitment
or replace the original neuronal and learning equations.

## Source and declared reduction

The protected Rajagopal model at OpenSim models revision
`e0cee6cbcf56f74731e841668ef668556d140ce9` has bilateral patellofemoral
CustomJoints, with an enforced identity coupling to each knee coordinate.
For each side it declares a rotation about parent Z, translations along parent
X and Y, constant Z translation, and zero other rotations. Each variable
transform uses the same 13 source knots. All nine quadriceps attachment points
per patella, frame offsets, raw numeric text, mass and inertia are retained in
`artifacts/patellar-input-20261123`; all 23 files and their D: copy were verified.
The embedded test fixture matches the retained decoded source after canonical
LF line-ending normalization; the protected original bytes are separately hashed.

Source spline conditions are pinned from
[OpenSim SimmSpline](https://github.com/opensim-org/opensim-core/blob/c00135abd92d4d7ac155d1e604dfa1612493ea3c/OpenSim/Common/SimmSpline.cpp),
revision `c00135abd92d4d7ac155d1e604dfa1612493ea3c`, SHA256
`371a2d507fe96622b3f6b0fdefd44032a490f5975034d76241cb5a10ce690b5c`.
Its source, license, notice and provenance are retained with the input.
The independent C# formulation solves for second derivatives, enforcing cubic
continuity and the endpoint third divided differences. Two-point declarations
are linear; three-point declarations are quadratic. No OpenSim binary is run.
Tiny-denominator clipping and out-of-range extrapolation are not imported.

`AvatarPatellarPath` calculates a source point's position and first/second
derivatives with respect to the knee coordinate. Its immutable identity includes
source hash, body IDs, offsets and every knot/value. All inputs must be finite.
The source support is 0–2.0944 radians (about 120 degrees), whereas the original
Avatar maximum is 2.45 radians. Values outside the source range are rejected,
without changing the Avatar's existing limit or silently clipping the source.
This range mismatch must be resolved explicitly before native selection.

This is a restricted **massless constrained attachment** reduction. The source
patellar mass of 0.0862 kg per side and its inertia are retained as metadata;
their dynamics are not yet simulated. The source knee CustomJoint also includes
kinematics beyond a simple native hinge. Neither its complete three-dimensional
motion nor the original anatomical model is reproduced by this component alone.

## Mechanical transfer

For parent-local attachment position p(q), the world Jacobian J is its
derivative with respect to the declared native knee coordinate, transformed by
the authenticated parent frame. A muscle force F gives generalized load
Q = F dot J. The massless reduction applies F once at the virtual point on
the parent, plus equal/opposite relative-coordinate moments on child and parent.
The summed external force and moment remain those of F at that point. The
paired moments transfer the work Q times qdot to the constrained coordinate;
they are derived constraint reactions, not an independently chosen motor torque.

`PatellarForceTransfer` retains the application point, original force, paired
moments, relative speed, point velocity and both power calculations. The native
owner must supply the correct coordinate sign, unit hinge axis, world Jacobian,
parent material-point velocity and the same acknowledged physical sample.
This component does not authenticate those samples or perform the source-to-
native frame mapping. Cylinder surface reactions remain separate required loads.
No live owner selects either new geometry component in this change.

## Prospective qualification

The visible PowerShell study runs 24 new cases and 75 existing straight-path,
causal-plant, dynamic-muscle and solution-manifest regressions: exactly 99 cases.
Controls cover analytic linear/quadratic/cubic interpolation, rotating and
translating points, immutable curve input, source identity/range, invalid inputs,
constraint power and exact namespace-only Unity parity. The source-point case
retains every attachment at 13 knots and 12 midpoints: 450 complete records.

The independent reference solves all 48 piecewise-polynomial coefficients per
curve from interpolation, continuity and endpoint constraints, at 80 and 110
digits. It differentiates point position and the force work potential, without
reusing the native rotation derivative or tridiagonal implementation. Both
reference sets are saved before comparison. Bounds are fixed before execution:

| Quantity | Absolute bound |
|---|---:|
| Position and application point | 1e-12 m |
| First coordinate derivative | 1e-11 m/rad |
| Second coordinate derivative | 1e-10 m/rad² |
| Transferred force | 1e-12 N |
| Generalized load and paired moments | 1e-9 N m |
| Moving-point velocity | 1e-10 m/s |
| Relative angular speed | 1e-12 rad/s |
| Point and delivered power | 1e-9 W |
| Independent precision agreement | 1e-60 |

The separately pinned mpmath 1.3.0 environment is reused; the older numerical
environment remains untouched. Preparation verifies source/fixture identity,
exact test inventory, generated parity and runtime dependencies without running
tests or geometry. All test/reference execution happens in the visible window.
Both original pending studies (`b3cf65`, `0b9165`) remain untouched.

A pass would establish source-curve and force/work arithmetic at the sampled
coordinates. Native attachment/constraint delivery, continuous coupled dynamics,
anatomical scaling, muscle populations, neuronal authority and Avatar standing
still require their own evidence. No editor or live-world presentation changes
are made before a body owner selects the mechanism.
