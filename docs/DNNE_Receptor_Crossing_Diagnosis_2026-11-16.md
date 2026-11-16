# Receptor comparison and crossing-time diagnosis

The operator reported `receptor-membrane-comparison-20261116-f7ece6` failed.
The original capture remains FAILED and unchanged. Its completed-data review
verifies 1,975 files per C:/D: copy, 5,405,631,249 bytes, 1,728 protected files
and 1,684 frozen Git files at `ade8c0252749a204dee4715cb88ab850d1492fb5`.
Managed runtime records agree; the pinned numerical environment is unchanged.

`tools/review-receptor-comparison-20261116.py` rechecks recorded trajectories,
source/receiver delivery and numerical contrasts without advancing any model.
`artifacts/receptor-comparison-review-20261116/review.json` is separately D:
backed. Original acceptance limits are unchanged.

## Completed evidence

All 18 candidate conditions satisfy the absolute voltage, gate, calcium,
receptor, spike-count/timing and physical-ledger limits. All 18 pairwise
reference comparisons pass across the three independent integration settings.
Six analytic receptor and 36 Jacobian controls were recorded in the original
run; they were not rerun in this review. The 36 source/Avatar receiver cases
reproduce their saved audit exactly: 540,036 observation points and 1,296 input
contact deliveries. Original source events, packet-cadence equivalence and
receiver delivery remain intact.

The window's `SourceOutputAuditPassed=false` is not evidence that this audit
failed. Its assignment lies after the reference command; that command exited
nonzero before the assignment was reached. The independently reproduced
`motor-output-analysis.json` is complete and passing. Overall coupled numerical
qualification remains false because the original refinement gate did fail.

Only one of twelve refinement contrasts fails: distributed excitation,
0.0125 to 0.00625 ms, spike-time error. Maximum errors are approximately
42.802 ns, 70.486 ns and 4.083 ns at the three respective step sizes. Each is
well inside the absolute 0.01 ms timing limit, but the middle result violates
the predeclared refinement ratio of 1.1. No rule is relaxed retrospectively.

## Saved-data error decomposition

The native cell estimates a threshold crossing by a straight line between two
accepted endpoint voltages. Apply the same secant to the independently saved
reference voltages at those exact endpoints. The total timestamp difference
then separates into candidate/reference trajectory difference and secant error.
Every selected reference crossing is bracketed by the same two endpoints.

For the second distributed-excitation spike:

| Maximum step (ms) | Trajectory contribution (ns) | Secant contribution (ns) | Total (ns) |
|---|---:|---:|---:|
| 0.0125 | +80.467 | -56.780 | +23.687 |
| 0.00625 | -63.929 | -6.557 | -70.486 |
| 0.003125 | -1.276 | -2.808 | -4.083 |

At the coarse step, cancellation makes this spike's total error smaller than
either component. The first coarse spike instead sets the condition's maximum
error (about -42.802 ns). The middle-step trajectory error has the opposite
sign, so the two contributions reinforce. This explains the failed maximum-error
ratio from recorded data; it does not establish that voltage integration needs
no further work. The smallest step improves both contributions substantially.

## Next isolated saved-endpoint experiment

`tools/receptor_crossing_study.py` freezes the reviewed crossing brackets from
all 18 conditions, their complete endpoint states and the tighter independent
reference states/events. Preparation extracts saved data, verifies source hashes
and dependency fingerprints, and evaluates no dynamics or new crossings.

The visible PowerShell experiment will evaluate the pinned independent membrane
derivative at each endpoint and form a cubic Hermite interpolant. It requires a
strictly increasing polynomial throughout the bracket before selecting a root;
ambiguous/nonmonotonic brackets fail. Controls cover three known monotonic
cubics, a linear limit and rejection of a polynomial with three crossings.
Polynomial residual is bounded by 1e-8 mV; original candidate/reference timing
limits and the original 1.1 refinement ratio/floor are preserved.

The experiment compares both candidate-state and reference-state cubics with
the saved independent event times, retaining per-spike signed errors. It does
not rerun an ODE, change C# event production, alter weights, generate a new
motor train or execute physics. A successful result would justify implementing
and qualifying a native event-estimator candidate, including causal publication
and explicitly versioned compatibility; it would not retrospectively turn
`f7ece6` into a pass or qualify live DNNE authority, physiology or Avatar recovery.

The separate spindle population transport work is recorded in
`DNNE_Spindle_Population_Transport_2026-11-16.md`. Its later completed numerical
comparison and limitations are documented there; it remains isolated from Avatar.

## Launch record

The saved-endpoint package is `receptor-crossing-20261116-c099e3`, frozen from
`8a6b4c4431d62d2715258ef46bd7bbece1b3d3ad`. It contains all 27 recorded spike
brackets across the 18 conditions and requires five analytic controls. Its
1,713 initial files plus manifest were D: verified before launch. The independent
visible task is `DNNE-Receptor-Crossing-03729f91bef1`. No post-launch output,
status or process has been inspected; await the operator's completion report.

The eight-file `receptor-and-transport-authoring-20261116` capture is also D:
verified. It records Python/PowerShell syntax checks and the unexecuted source
snapshot. No test, new crossing estimate or model ran during preparation.
Only this new crossing diagnostic now awaits an operator completion report.

## Completed crossing diagnostic

The operator subsequently reported `c099e3` complete. Its review verifies 1,718
C:/D: files per copy, 58,398,460 bytes, 1,712 protected files and 1,694 frozen Git
files. The current pinned dependency fingerprint matches the captured one.
`tools/review-receptor-crossing-20261116.py` reconciles the saved polynomial
coefficients, bracket membership, residuals, strictly positive derivative bounds,
timestamp errors and all aggregation/refinement records. It evaluates no neuronal
derivative, performs no root search and executes no test.

All five analytic controls, 27 endpoint pairs (54 candidate/reference polynomials),
18 conditions and twelve original refinement checks pass. Maximum cubic timing
error against the independent event is 8.0256e-5 ms (80.256 ns); the largest
reference-state cubic error is 5.4969e-8 ms (about 0.055 ns). All original
candidate/reference timing limits and refinement bounds remain unchanged.

This is not pointwise improvement everywhere. Distributed excitation at the
coarsest step changes from maximum error 42.802 ns to 80.256 ns, exposing the
trajectory error previously reduced by cancellation. Its middle/fine cubic
errors are 64.363 and 1.278 ns, and satisfy the declared refinement ratio.
Local excitation at the middle step also increases slightly, from 9.443 to
9.563 ns. Report these outcomes rather than calling the estimator uniformly
more accurate. At the finest step, all excited cases improve in this capture.

`artifacts/receptor-crossing-review-20261116/review.json` is D: backed. The
original `f7ece6` capture remains FAILED. A native implementation is the next
receptor step: explicitly version the optional crossing estimator, retain the
existing linear mode and its saved regressions, and use actual endpoint
derivatives with fail-closed rejection of ambiguous brackets. Then qualify
native event production, unchanged physical trajectories, source identity and
causal publication/receiver delivery before using it in a connected circuit.
No C# estimator or live neuronal source has changed in this review.

The operator has also reported the subsequent spindle transport window complete.
Both windows are now reviewed; neither remains pending or is being monitored.
