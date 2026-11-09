# Motor-cell fixed-state precision diagnosis

The reviewed compensated motor-cell study retains FAILED: eight of ten complete
scenario gates pass, while soma-1000 and without-fast-sodium retain tiny calcium
refinement failures. All finest absolute checks pass. See
`DNNE_Compute_And_Cell_Results_2026-11-09.md`. Neither increasing drive nor changing
conductances is justified by that numerical discrepancy.

The next diagnostic compares the two implementations at identical saved states.
It does not integrate another trajectory, repeat the six-second experiments,
relax a criterion, or claim the rounding hypothesis has already been proved.
Both local calcium cancellation and electrically coupled voltage arithmetic are
examined; upstream voltage differences can influence the calcium trajectory.

## Frozen evidence and selection

The preparer verifies selected inputs on C: and D: from reported
`motoneuron-numerical-20261106-640f85`, revision
`22d295951135b2b04992884ddfd309607cc40f6c`. Four conditions are predeclared:
quiet, soma-1000, without-fast-sodium and the spiking soma-6000 control. All three
candidate CSV traces and all three adaptive references for each are retained.
The frozen Python reference modules and pinned HOC/MOD inputs are copied too.

Before analysis, all stored state-error metrics for those conditions reproduce.
Selection includes initial/final and predeclared input-boundary/recovery samples,
each recorded candidate/reference calcium-error maximum, reference-disagreement
maxima and the tight-reference calcium peak. Every selected time is used for all
six trajectories in that condition. The full states, source trace names, indices
and exact selected physical commands are recorded. At a command transition the
RHS uses the new command; this is a local evaluation, not a replay across it.

## Calculations

The candidate exposes an internal read-only derivative entry point in its isolated
reference cell. It calls the existing RHS and preserves its domain checks.
Packaging requires the old cell source to match exactly after removing only that
new diagnostic method; the channel file must remain byte-identical. Thus the
equations and integration code used in the failed study are retained.

The C# probe verifies that evaluating each point leaves its cell state, clock,
crossings and accounting untouched. A reference state outside the candidate's
strict domain is reported as rejected without clipping it. Rejection of a saved
candidate state is a diagnostic failure. The Python side evaluates its original
coupled RHS on the same states and records every derivative and grouped maximum
difference.

Decimal calculations at 80 and 120 digits check calcium entry/removal arithmetic
and the equivalent reduced passive geometry. These operate on the **exact values
of the declared binary64 inputs**; they do not pretend to recover a more accurate
physical state or anatomy. The rounded calcium reversal constant is also reported
beside the high-precision symbolic expression `12.5 ln(40000)`. Agreement between
the two decimal precisions is required. No arbitrary precision solver or native
NEURON model is executed.

The report retains both implementations' local calcium arithmetic errors and
the reference's axial-current error against high-precision pair conductances.
Local calcium derivative bias times the 60 ms removal constant is a diagnostic
scale only: coupled voltage/gate dynamics and changing drive prevent interpreting
it as a trajectory error bound or a causal explanation.

Saved CSV states omit Kahan addition residuals. These points are therefore not
exact integrator checkpoints, and no continuation or accumulated-roundoff claim
is inferred from them. A later controlled experiment may be required after this
report identifies which terms differ.

## Execution and limits

`prepare-motor-precision.py` freezes clean pushed source and reviewed evidence.
`start-motor-precision-window.ps1` verifies its initial C:/D: package, selects
states, builds the probe, evaluates them and runs the decimal comparison in an
independent visible PowerShell window. Source and binaries are rehashed and final
results backed up to D:. No live monitoring or duration limit is imposed.

Release builds pass with zero warnings/errors. Static Python/PowerShell checks
precede packaging; runtime diagnostic results remain pending. The installed
NumPy/SciPy versions are required to match 2.2.6/1.16.3 and are recorded with the
Python version. This is not a full unit-suite or physiological qualification.
No live DNNE, Avatar, Unity, editor control, old save or biological parameter is
changed. Original failures and acceptance limits stay intact.
