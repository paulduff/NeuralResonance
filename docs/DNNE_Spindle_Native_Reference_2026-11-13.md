# Original spindle mechanics under Octave

This isolated rig executes the pinned author bag/chain classes without editing
their equations. It is the next mechanics boundary from
`DNNE_Spindle_Mechanics_Review_2026-11-13.md`, before a port or Avatar integration.
Octave execution is not agreement with MATLAB's solver or calibrated physiology.
No receptor potential, sensory membrane, gamma motor interface, live body or
speed measurement is part of this study.

## Prospective experiment

The source is `Neuromechanics-Lab/spindleModelTutorial` revision
`721ba03cb775261548766ed1349c4bf2e0e35385`. The original vendored bag and chain
classes and default parameters are resolved explicitly from the frozen package.
The isolated portable runtime is Octave 11.3.0; its archive and full extraction
manifest are preserved on C: and D:. Runtime files and declared archives are
verified before and after execution. No Python numerical environment is modified.

Thirty trajectories combine two fibers, five inputs and three step lengths
(1, 0.5 and 0.25 ms). Each lasts 100 ms and records a genuine unevolved initial
state followed by completed endpoints. Inputs are quiet hold (pCa 9), active hold
(6.5), drive on/off (9 → 6.5 at 20 ms → 9 at 80 ms), stretch/shorten
(1250 → 1270 → 1250 nm), and an imposed excursion below slack
(1250 → 1000 → 1250 nm). Length ramps occupy 20–40 and 60–80 ms, with a hold
between them. The below-slack case deliberately does not invoke the upstream
slack controller; it is not a claim of physiological slack mechanics.

The original `forwardStep` updates length, evolves kinetics using `ode23`, shifts
the distribution and computes force in its own order. Default ODE options remain
unchanged. Interval pCa is supplied from the start of each interval. This is a
declared imposed-length driver, not the original full tutorial driver, and outer
step differences can reflect its operator splitting as well as numerical error.

Two further records warm each fiber for 100 ms, then retain states before and
after diagnostic instantaneous +3, −3 and +100 nm shifts without another ODE
solve. They also inspect the exact zero-overlap boundary reached from positive
and zero overlap, without solving at that boundary. The saved state includes
both the evolved vector and separate caches, their shapes, reported forces,
population totals and original empty fields. The constructor's column-vector
state versus row-vector force calculation, stale caches after shifts, and
history-dependent zero-overlap branch remain visible rather than corrected.

Each case retains MAT and JSON output. MAT retains nonfinite values even if JSON
represents them as null. Failed trajectories retain their completed prefix and
failed attempt, without retrying or relabelling a partial trajectory as complete.

## Audit and interpretation

`spindle-reference.py analyse` reads saved records without invoking Octave. It
requires the complete 32-case inventory, initial state, endpoint counts, declared
input values, finite state dimensions and corresponding MAT files. Clock checks
use 1e-12 seconds and prescribed length checks use 1e-8 nm solely for arithmetic
representation; these are not biological or solver accuracy tolerances.

The report measures myosin/actin totals, cache discrepancies, minimum populations,
stress ranges, non-scalar force shapes and overlap history. Twenty paired outer
step comparisons retain all aligned states. A non-scalar force is explicitly
listed and is not silently reduced to its first element. No acceptance bound is
invented from these differences. Finite completion may coexist with bookkeeping
defects and does not establish numerical accuracy or a usable spindle model.

## Preparation and status

`spindle-reference.py prepare` requires clean pushed source, pinned C:/D: archives
and the exact extracted runtime inventory. It freezes repository/upstream source,
metadata and runtime fingerprints. After a verified initial D: backup,
`start-spindle-reference-window.ps1 -PackageDirectory PACKAGE` runs only in an
independent visible PowerShell window. Octave has startup files/history disabled,
explicit function paths and process-local runtime/thread settings. Final evidence
is backed up and results await the operator report without live monitoring.

The freezer, original-class harness, launcher and offline analyzer are coded.
Python and PowerShell syntax can be checked without running a model; native
compatibility and all numerical results remain untested. No spindle package is
frozen or launched. The now-reported Golgi and contact results take review
priority, followed by the exclusive CUDA host timing study. Do not launch this
mechanics experiment or perform heavy runtime hashing during that benchmark.

## Launch after timing review

The operator reported CUDA timing complete and its full evidence review passed.
The spindle package is now `spindle-reference-20261113-9a5090`, frozen revision
`d3f148e5e281f3354b4c391f0f2b160768807abb`, launched as independent visible
PowerShell task `DNNE-Spindle-Reference-cebf3230ec3d`. Preparation verified the
pinned C:/D: archives and all 59,532 extracted runtime files without executing
Octave. The 1,590 initial package files plus manifest have a verified D: copy,
rechecked by the launcher. Runtime hashes are also checked inside the window
before and after execution; those execution-stage outcomes are not yet known.

No live monitoring follows launch. All thirty trajectories and two boundary
records await the operator report. Compatibility, numerical behavior and
physiology remain unqualified. The other outstanding report is the 70-case body
profile window `physical-observation-20261113-64ad44`; neither measures speed.

## First Octave execution: argument-binding failure, reviewed

The operator reported `spindle-reference-20261113-9a5090` failed. All thirty
trajectories stopped on their first integration attempt, retaining only their
unevolved initial state; both boundary records failed before warmup completed.
All 32 errors say `derivs: function called with too many inputs`. There is no
completed integration step from which to assess numerical or biological behavior.

The pinned author classes call `ode23(@derivs,time_step*[0 1],y,[])`. The actual
Octave 11.3.0 `ode23.m` sends a non-struct fourth argument to the derivative as
an additional parameter. Its callback accepts only time and state. The stack
stops at `starting_stepsize` invoking that callback. This is an execution
compatibility failure, not evidence of unstable physiological equations.

The separate failure review verifies all 1,665 files in each C:/D: copy
(67,205,238 bytes), 1,520 frozen Git files and every extracted upstream file
against the pinned archive. Runtime verification before/after exactly matches
preparation, covering all 59,532 files. Manifest SHA256:
`AD1079D9FF9FFF89C02D19E8D0A83D0767810355E7921839F387F9E0D44404E1`.
The review has its own verified D: backup; the failed source/status stays intact.

## Declared Octave compatibility retry

A fresh package keeps `upstream/` byte-for-byte and makes a separate
`octave-compatible/vendor/` execution copy. Exactly two call sites, one in bag
and one in chain, replace the empty argument `[]` with `odeset()`. The generated
`octave-compatibility.json` records both source/output hashes and exact edits.
Verification checks matching inventories, unchanged remaining bytes and the
precise two substitutions. No equation, parameter, integration interval, solver
setting, initial state, slack logic or population bookkeeping is changed.

The retry declares `octave-ode23-empty-options-struct-v1`; it is no longer described
as execution of completely unmodified source. Before any fiber trajectory, a
control integrates y'=-y over 0.01 s using the three-argument call and explicit
empty options structure. Their saved time/state arrays must match exactly, their
maximum error against exp(-t) must be at most 1e-8, and passing `[]` must reproduce
the argument-count rejection. These prospective controls test Octave binding and
its own default solver, not MATLAB equivalence or spindle accuracy.

The original thirty trajectories, two boundary records and twenty descriptive
outer-step comparisons remain required. The source-resolution record includes
the compatibility controls. Independent PowerShell execution, full runtime/source
verification and final D: backup remain mandatory; no tests/models run inline.
