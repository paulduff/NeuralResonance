# Canonical intrafusal state candidate

The completed `98c085` reference revealed that imposed length changes update
`y` but leave reported populations stale. The slack-force search reads those
stale populations. Initial force is also nonscalar because a column state is
multiplied elementwise by row coefficients. The preserved source and results
remain unchanged; see `DNNE_Muscle_And_Spindle_Results_2026-11-13.md`.

The next isolated candidate makes `y` the authoritative state. Reported bound
bins, detached population and total bound population become read-only dependent
properties. Initialization uses the same 84 values in consistent row form.
The positive-overlap kinetic fluxes, parameter values, interpolation, original
split ordering and Octave solver defaults are retained. The preparer records
every source edit and its before/after hashes, and retains the original AGPL-3.0
license with the separate derived source tree.

Geometry now assigns exact-zero overlap deterministically and refreshes after
zero-duration imposed shifts too. Kinetics reject zero, negative, nonfinite or
greater-than-one overlap before evolving state. This is an explicit unsupported
domain, **not a zero-overlap physiological repair**. No epsilon denominator,
population clipping, invented activation reset or automatic continuation is used.

## Why zero overlap needs a separate mechanism

In the pinned source, active eligible sites `a` include bound sites `b`, and the
fluxes contain `a/F` where `F` is geometric overlap. For admissible states write
`a = F*q`, `b = F*r`, with `0 <= r <= q <= 1`. The activation/deactivation fluxes
become `k_on*Ca*F*(1-q)*(1+q)` and `k_off*F*(q-r)*(2-q)`. They tend to zero as
`F` tends to zero along admissible states. However, jumping geometry to zero
while retaining nonzero `a` or `b` does not satisfy those assumptions. This is
an algebraic inference from the preserved equations, not a validated additional
biological rule.

A complete extension must specify how sites and attached heads enter/leave the
overlap region and how activation outside that region is represented. The
existing aggregate state does not define that history. Assigning zero geometry
alone cannot provide it. The original 30 imposed trajectories remain within
positive overlap; their domain and site availability are checked explicitly in
the new comparison. Extreme extension/injury behavior remains unfinished.

## Force-query building block

`tools/spindle_trial_stress.m` evaluates a trial length using `y`, the original
linear shift with zero outside the bin support, and passive stress at the trial
length. It returns stress in Pa, shifted populations and the detached mass,
without mutating the input fiber. It supplies the mathematical building block
for the forthcoming slack solver; it does not select a full slack controller.
The original driver's coupled slack flags and time convention still require
repair before a live causal port.

## Comparison and controls

`spindle-reference.py prepare-state` freezes clean pushed source, the unchanged
upstream and Octave compatibility copies, a separately hashed canonical-state
copy and all 30 previously reviewed baseline trajectories/two boundaries.
It verifies the reported review and selected baseline C:/D: bytes, then the
entire 59,532-file Octave runtime before any execution. Each original source
snapshot and its backup remain untouched.

The independent PowerShell window runs:

- The same 30 bag/chain trajectories at 1, 0.5 and 0.25 ms, with true initial
  states and the original imposed-length and pCa fixtures.
- Two original shift/overlap boundary records, now requiring consistent state
  views, scalar force and history-independent exact-zero geometry.
- Both fibers' read-only-property, trial-force, analytic unloaded-root, detached
  mass and rejected-domain controls. Trial queries must not mutate state; their
  results must match applying the same shift to a separate fiber instance.

State differences from saved baseline must be at most `1e-12`; post-initial
stress differences at most `1e-7 Pa`. Initial stress must equal the analytic
passive stress. All reported population views must match `y`, with myosin/actin
accounting and eligible-site bounds checked to `1e-12`. Trial stress uses the
same `1e-7 Pa` bound; its empty-crossbridge root must match slack length within
`1e-9 nm`. These are representation/equation-consistency checks, not independent
ODE convergence or physiological acceptance. The original outer-step
differences remain descriptive and must not be relabelled convergence.

Preparation and Python/PowerShell syntax checks run without models. Octave class
loading, controls and trajectories run only in the visible window, with results
reviewed after the operator reports completion. The candidate is isolated:
actual gamma firing, causal receptor transduction, afferent membrane calibration,
full slack/re-tension, Avatar/Unity selection and recovery remain outstanding.

The first preparation `spindle-state-20261113-c9a66b` stopped before execution:
isolated Python could not resolve an unrelated motor protocol imported by a
shared verification helper. The preparer now verifies selected C:/D: files
directly using standard-library hashing. The rejected source snapshot and
preparation-failure record have a verified D: copy (1,639 files plus manifest).
No PowerShell experiment was launched for that package; it is not pending.

The corrected package is now launched as `spindle-state-20261113-3468db`, frozen
revision `cd0c193d44426704dce0a03b3d27aa0864088d95`, task
`DNNE-Spindle-State-0f7f73e4c0ec`. Preparation verified all 59,532 runtime files,
the unchanged compatibility copy and the 18-file candidate execution tree.
All **1,697 initial files plus manifest** have a verified D: backup, rechecked
before launch. This is the only pending operator report. No live output has
been inspected and no candidate result is inferred from launch.

## Reported first outcome

The operator reported `3468db` FAILED. Full review verifies all 1,772 C:/D: files
per copy (98,300,886 bytes), all 1,551 frozen Git files, declared transformations,
and matching preparation/before/after fingerprints for all 59,532 runtime files.
Manifest SHA256:
`56F65FDB9D67D567741B8F4978CB508FA5B6E16F647F0BFECD313AD6A68BE836`.
`tools/review-spindle-state-failure-20261113.py` records the review in
`artifacts/spindle-state-failure-review-20261113/review.json`, with its own verified
D: copy. It ran no test or model.

All **30 trajectories and two boundary records** completed. State and post-initial
stress differences from the preserved baseline are **exactly zero**. Reported
populations now agree with the authoritative state; initial force is scalar and
equals analytic passive stress; zero-overlap geometry no longer retains history.
Accounting and eligible-site bounds pass the declared checks on saved samples.
This still does not qualify independent numerical convergence or physiology.

The subsequent control failed its first unloaded-root assertion. The original
harness did not save the root value or solver exit information before asserting,
so the cause is not yet established. The pinned Octave `fzero.m` declares a
default `TolX` of machine epsilon; there is no evidence supporting a blanket
tolerance increase. No bound or mechanism is changed on that supposition.

The next diagnostic saves the initial state, parameters, four direct force
queries and their analytic passive values, scalar-guess and explicit-bracket
root results/residuals/exit information, in both JSON and MAT before asserting.
The bracketed root is a diagnostic contrast, not a fallback replacing the failed
root. The original `1e-9 nm` bound remains. A preflight check stops early if the
failure recurs; the same check still runs after the matrix so moving it earlier
cannot conceal an order-dependent fault. This is investigation, not a claimed
root repair. The original failed capture remains preserved.

The diagnostic retry is launched as `spindle-state-20261113-ae7ae4`, frozen
revision `b60ee5b95908e5c866b5ed83014947bf14dbb0af`, task
`DNNE-Spindle-State-7f9eefcd0c40`. All 1,698 initial files plus manifest have a
verified D: copy, rechecked before launch; preparation verified the entire pinned
runtime and source transformations. This is the only pending operator report.
Do not inspect live outputs. A launch does not establish a passing root check.

## Root-search failure identified

The operator reported `ae7ae4` complete/FAILED. Full review verifies all 1,710
C:/D: files per copy (80,647,828 bytes), all 1,552 frozen Git files and the
canonical transformations. Preparation/before/after runtime fingerprints match.
The capture manifest is
`0B6E0A0454891DE23AD35F11588F78951C88E090664E610CE409867566B5223B`.
The review and pinned solver source are preserved in
`artifacts/spindle-root-review-20261113`, with their own verified D: copy.

Both scalar-zero searches returned exit code -6, no root and no residual.
Neither performed a root iteration: each exhausted its 14 interval-search
queries. The pinned `fzero.m`, verified against the saved runtime manifest,
sets `aa=0.1` for a zero initial guess and evaluates `aa + aa*srch` using a
finite search list. Its most negative displacement is **-49.9 nm**. The actual
unloaded roots require **-200 nm (bag)** and **-50 nm (chain)**. The solver never
brackets either zero; this is not insufficient root precision or failed kinetics.

All four recorded direct queries equal the analytic unloaded passive stresses.
The diagnostic brackets centered around the known analytic roots return exactly
1,050/1,200 nm, residual zero, exit code 1, with the original state intact. The
run correctly stopped before its trajectory matrix after recording this evidence.
Its FAILED status remains preserved; the earlier 30 exact trajectory contrasts
remain separate evidence.

The corrected control explicitly supplies displacement interval
`[-initial_length/2, 0]`, equivalent to positive trial lengths from half the
initial length to the initial length. This interval does not use the analytic
root to choose a narrow bracket. Its endpoint signs are recorded and checked.
The accepted root must retain the original **1e-9 nm** error bound, successful
solver exit, finite result and residual within the existing **1e-7 Pa** stress
bound. The scalar-search failure is still saved as a diagnostic contrast.
The same checks run before and after the trajectory matrix.

This corrects initialization of the unloaded test's root search. It does not
implement a general active slack controller, change cross-bridge equations,
provide zero-overlap kinetics or establish physiological validity. Fresh packages
include the verified failure review and pinned solver source explaining this
change. The original captures are not rewritten.

## Corrected root-control launch

`spindle-state-20261113-a43f1a` is frozen from
`b9937e9163233b89322510b7742ee6d292361836`. Preparation verified all 59,532 pinned
runtime files and the canonical transformations, including the saved root-search
failure evidence. All 1,712 initial package files plus manifest have a verified
D: copy, rechecked at launch. Visible task `DNNE-Spindle-State-83574529f0c3` is
launched and unmonitored. Await the operator's completion report; this is the
only pending experiment. The motor coupled comparison has not been launched.
