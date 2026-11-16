# Independent comparison of captured native hinge trajectories

## Purpose and scope

The completed native motor/body bridge `native-motor-bridge-20261116-e7ddb6`
passes all 81 tests, four physical cases and eight rejection controls. Its
independent impulse and command checks do not establish whether the coupled
muscle and hinge trajectory is accurate throughout the run.

This comparison reuses those four fully reviewed, D: verified captures. It
introduces a continuous nonlinear hinge reference coupled to the already
qualified Python muscle/calcium equations. No native physics, brain, live body,
editor, saved brain or neuronal source is launched. The input events remain
explicit artificial motor fixtures. No candidate model or acceptance bound is
changed to obtain agreement.

This is an initial comparison at the captured physical resolution, not a full
physical-step or muscle-step convergence study. Its strongest positive result
is `capturedTrajectoryAgreementPassed`; `fullCoupledTrajectoryQualified`, both
step-refinement flags, physiological calibration and Avatar recovery remain
false. Matched native resolution refinement is a later requirement.

## Continuous mechanics

`native_hinge_reference.py` recovers the actual binary32 attachment coordinates,
mass, central inertia and center of mass declared by the native fixture. For
angle q, each child attachment is rotated about the hinge X axis. Its distance
to the fixed attachment gives L(q); differentiating this geometry gives dL/dq.
The total generalized torque is `-sum(T_i * dL_i/dq)`. Inertia includes the
parallel-axis contribution `Icentral + mass * centerY^2`. Gravity, damping,
friction and drive force must all be absent, as in the captured fixture.

Each adaptive derivative evaluation recomputes both path lengths, tendon
tensions, muscle/calcium rates, joint acceleration and mechanical work from
that trial state. Native endpoints are neither interpolated into the reference
nor used to drive it. The reference shares the previously reviewed continuous
muscle equations and adaptive recorder from `causal_motor_reference.py` and
`dynamic_muscle_reference.py`; it does not use the C# integrator, held-force
partition, native stepping or C# path implementation. This is numerical
independence, not a second independently calibrated physiological model.

The 1,025 actual captured timestamps in each case are used exactly. The native
step is 0.1250000059371814 ms, ending after 1,024 steps at
128.00000607967377 ms. It must not be relabelled as exact 0.125 ms. The actual
initial path length is 0.3398271957066012 m, rather than the nominal authoring
rest length. Arrival boundaries remain exactly 10.03125 and 60.03125 ms, with
source neuron, contact and message identities retained and checked.

## Prospective controls and comparisons

Before muscle/hinge solves, 49 angles from -0.75 to +0.75 rad check path length
and derivative against a separate scalar law-of-cosines/complex-step expression.
Moment arms and power are checked against point-force cross/dot products.
Bounds are 2e-15 m for length and 2e-12 for derivative, torque and power in their
respective units. These controls cannot validate human anatomical attachments.

Six mechanical reference controls use two adaptive methods for free rotation,
balanced constant tension and unequal conservative constant tension. They check
analytic free/balanced motion, frozen artificial muscle states, joint impulse,
work and conservative potential energy, with three cross-method contrasts.
Constant tension is a method-control intervention only. The controls exercise
event intervals shorter than the recording grid and preserve partial evidence
on failure. Energy/impulse/work bounds are 1e-10 in their respective units,
analytic angle/speed and frozen-state bounds 1e-12, and cross-method angle/speed
bounds 1e-9 rad / 1e-8 rad/s. All control results must pass before biological
equations are integrated.

Then four conditions (quiet, left, right, balanced) use DOP853, tighter DOP853
and Radau: 12 independent references. Settings are identical to the qualified
analytic coupled study. Every accepted adaptive state is domain checked; all
sampled trajectories, forces, statistics and failure prefixes are retained.
The reference methods must agree before interpreting candidate disagreement.

Maximum errors are measured over the entire trace for angle, speed, fiber
length/velocity, yield, calcium, activation, flux integrals, all force components,
acceleration and fiber impulse/work. Bounds are copied unchanged from the
earlier coupled study's corresponding columns: candidate angle/speed 1e-5,
fiber length 5e-7 m, fiber velocity 5e-5 m/s, calcium/activation/yield 1e-5,
flux integral 1e-4, force 0.01 N, acceleration 0.1 m/s2 and fiber impulse/work
1e-5. Reference bounds are respectively 1e-9 rad, 1e-8 rad/s, 1e-9 m,
1e-7 m/s, 1e-8, 1e-7, 1e-5 N, 1e-4 m/s2 and 1e-8.
`native_hinge_protocol.py` is the authoritative machine-readable specification.

Calcium, fiber momentum and fiber kinetic ledgers are independently reconstructed
and checked against both saved and reference states. Reference joint impulse,
joint work and tendon work are also checked, with tendon constitutive energy
calculated at two quadrature orders. No native joint-integral accuracy is claimed:
the captured native player did not record integrated joint impulse/work or
tendon-work columns. Unobserved columns remain NaN in the parser, excluded from
accuracy metrics rather than replaced by invented values. Native path geometry,
release drive and exact event delivery are separately verified.

## Execution and preservation

The preparer requires clean pushed source, the verified native review, the
qualified analytic-muscle numerical review, unchanged qualified C#/Python cores
and exactly unchanged numerical dependencies. It copies selected native traces,
source batches, input parameters, reviews and original manifests into a fresh
package. The original 27,120-file capture stays intact. The new source archive,
copied evidence, protocol and numerical environment are protected by hashes.

`start-native-hinge-comparison-window.ps1` opens an independent visible PowerShell
window, verifies the initial D: backup, runs preflight, controls, solves, analysis,
postflight environment/source checks and a final verified backup. It stops with
saved failure evidence if any requirement fails. No live monitoring; await the
operator's completion report. Long-form refinements and live integration are
not implied by a `DIAGNOSTICS_COMPLETE` footer.

Authoring validation: six Python files parse, the PowerShell launcher parses,
and all four completed native traces decode to 1,025 ordered samples with exact
event identities and times. No new numerical solve or test has been executed
during authoring. Consult the launch record below for pending work.

## Launch record

`native-hinge-comparison-20261116-967542`, frozen revision
`9d6aca66712e9c207cbdc6a3ad4ebadfaf7296ef`, is launched in independent visible
PowerShell task `DNNE-Native-Hinge-Comparison-553670a7b85c`. All 1,657 initial
files plus manifest are verified on D: and rechecked by the launcher. The
qualified numerical environment and unchanged reference/candidate source passed
preparation checks. This is the only pending operator completion report. No
post-launch controls, solves or analysis have been inspected; do not monitor.

## Completed comparison

The operator subsequently reported `967542` complete. Review verifies all 1,700
files / 111,292,507 bytes in each C:/D: copy, all 1,624 Git source files, the
frozen input package, copied original evidence and unchanged numerical environment.
The frozen saved-data analysis exactly reproduces the saved result. All 49
geometry controls, six mechanical method controls, three control contrasts,
12 muscle/hinge references and four native trajectory comparisons pass their
original criteria. Saved mechanical-control traces also reproduce their reported
geometry, energy, impulse and work residuals without another solve.

Maximum native/reference differences over the compared trajectories are
3.57628956034427e-8 rad in angle, 4.697297118781956e-6 rad/s in speed,
3.310100334108501e-8 m in fiber length, 9.556588212156214e-5 N in force and
7.081600032710078e-6 in yield. All calcium, fiber momentum/work and reference
joint/tendon accounting checks pass. The largest reference-method discrepancies
are 5.214727846584999e-14 rad, 9.884636512080824e-13 rad/s and
6.264661012167494e-11 N. Agreement remains a numerical result for this synthetic
pair, not neuronal authority or physiological calibration.

Capture manifest:
`5F4C423AB3B72DE94F78869E60AE1B72169553C187EA320F327CF43F6E0FAA7F`.
`review-native-hinge-comparison-20261116.py` records the review;
`native-hinge-comparison-review-20261116/review.json` has a verified D: backup.
No earlier run remains pending. Physical/muscle refinement is still unassessed
by this capture; the next prospective matrix is documented in
`DNNE_Native_Hinge_Refinement_2026-11-16.md`.
