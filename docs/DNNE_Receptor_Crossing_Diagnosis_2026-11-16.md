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

The separate spindle population transport component is preserved as an
unexecuted candidate in `DNNE_Spindle_Population_Transport_2026-11-16.md`.
Its comparison rig is unfinished and no transport experiment has been launched.
