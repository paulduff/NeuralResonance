# Independent complete sensory trajectories

The joint dynamics component capture `cleft-dynamics-20261118-ca2f31` is now
operator-completed and reviewed. All 2,391 files per C:/D: copy, frozen source,
test/probe binaries, exact managed runtime and all 120 focused cases are verified.
Saved analysis reproduces without rerunning models/tests. Its separate review,
`artifacts/cleft-dynamics-review-20261118`, has a verified D: copy. This establishes
the tested component behavior, including analytic passive gate refinement,
accepted-prefix preservation and accounting; active trajectory accuracy remains
the next requirement.

The prospective `joint-cleft-trajectory-comparison-v1` workflow preserves every
reviewed biological/solver source file and earlier test. It adds a capture and
independent comparison, without changing the candidate equations, step method,
nonlinear tolerances or live Avatar/editor selection.

## Conditions and time ownership

| Condition | Patches | Duration | Input |
|---|---:|---:|---|
| Passive gate relaxation | 1 | 2 ms | Zero active currents; actual mixed gates |
| Active quiet | 1 | 8 ms | Constant zero bundle displacement/axon port |
| Bundle transitions | 1 | 8 ms | 0, +200, -200, 0 nm |
| Terminal transitions | 1 | 8 ms | 0, +10, -10, 0 pA outward |
| Graded local state | 2 | 8 ms | Distinct local voltages, ions and geometry |
| Bundle transitions | 2 | 8 ms | Same declared bundle sequence |
| Graded bundle transitions | 8 | 2 ms | Same declared bundle sequence |

Each condition has four equal, explicitly held intervals. These are numerical
fixtures, not anatomical calibration or a claim that a quiet interval establishes
equilibrium. Initial gates/states come from the prospective protocol. No candidate
output initializes an independent reference. Session, cell and compartment
identities and exact integer ticks are retained.

The candidate runs at `.002`, `.001` and `.0005 ms` (21 runs), with the reviewed
nonlinear settings unchanged. It records every accepted endpoint's complete
state, compensated-addition residuals, transfer ledgers, ledger compensation,
clock and solver diagnostics in one CSV per run. Initial/final snapshots and
failed prefixes are retained separately. All cases are attempted before a native
failure is reported. There is no hidden timestep retry or state clipping.

## Independent equations and reference accuracy

The reference reuses the separately evaluated Decimal membrane equations and
full capacitance-matrix solve qualified in the derivative comparison. It computes
the RHS at 40 digits before conversion for SciPy time integration. It uses no
candidate stage, voltage update or current ports. Seven initial conditions and
every reference segment's accepted endpoints retain 60/90-digit witnesses;
the two high-precision calculations must agree within `1e-40 + 1e-40*abs(value)`,
and the 40-digit RHS witness within `1e-25 + 1e-25*abs(value)` of the 90-digit one.
Native initial derivatives also retain their earlier `2e-10 + 2e-12*abs(value)`
comparison bounds.

Each condition runs Radau at two tolerances and BDF at the tighter setting
(21 independent references). The RHS includes the integral ledgers. An explicit
Jacobian perturbs physical states only; integral ledger columns are exactly zero.
This avoids asking SciPy's numerical Jacobian to repeatedly perturb states that
cannot affect any derivative. Reference Newton trials may evaluate the smooth
gate expressions outside [0,1], but no gate is clipped and every retained accepted
node and comparison sample must have physical gates and positive K/Na.

Input transitions stop/restart the reference solver at the exact declared
boundary, preserving the complete state/ledger prefix. Every accepted reference
node, segment status, dense comparison sample and precision witness is saved.
Unsuccessful solves save their available nodes and failure evidence too.

## Prospective acceptance limits

Comparisons use the common `.01 ms` grid. The following are new, declared
trajectory criteria; earlier numerical bounds are unchanged. Absolute limits
are augmented by relative limits of `1e-8` for reference agreement and `2e-6`
for candidate agreement with tight Radau.

| Quantity | Reference absolute limit | Finest candidate absolute limit |
|---|---:|---:|
| Voltage, mV | 5e-7 | .002 |
| K/Na, mM | 5e-9 | 2e-5 |
| Gate probability | 5e-10 | 2e-6 |
| Charge ledger, fC | 5e-7 | .0002 |
| Amount ledger, attomoles | 5e-9 | 2e-6 |

For each condition, the maximum normalized candidate error must decrease on
both halvings: `next <= .4*previous + .01`. The small floor avoids imposing an
observed-order claim once errors are already near the reference/roundoff scale
relative to the candidate acceptance limits. Finest-step error must independently
pass every quantity's bound; a decreasing but inaccurate trajectory cannot pass.

A separate capacitor/incidence calculation checks every candidate endpoint,
every accepted reference node and every comparison sample against the signed
charge/K/Na ledgers. Maximum residual limits are `2e-8 fC` and `2e-9 attomoles`.
No fictitious intracellular calcium, leak species or reservoir volume is added.
Reference agreement, finest accuracy, refinement and ledger checks are reported
separately per condition. Analysis is written before reporting a numerical failure.

The visible frozen runner first runs exactly 120 focused cases, then all native
captures, independent references and saved comparisons. It pins the previously
reviewed managed/numerical environments, preserves attributed source evidence,
verifies initial/final D: copies and retains stage logs. The capture authoring
build and static script/schema/inventory checks pass; no equations, controls or
tests have been executed inline. Runtime costs are not benchmarks.

A pass would establish sampled numerical accuracy for these declared trajectories.
Stationary initialization and stability, longer transients, anatomical calibration,
afferent firing, measured head-to-bundle coupling and Avatar balance remain open.

Launched 18 November as `cleft-trajectories-20261118-87562a`, frozen at pushed
revision `22148e322fe862e8b69c2f42388aa26534b8539a`. Initial 1,866 files plus
manifest are verified on D:. The launcher opened visible task
`DNNE-Cleft-Trajectories-8ff1f1475f1e` and returned successfully. No post-launch
outputs/status/processes have been inspected; await the operator's completion
report before reviewing or claiming numerical success.

## Completed result and finer follow-up

The operator-completed `87562a` was fully reviewed with C:/D: verification.
Six conditions pass; `bundle-eight` misses finest accuracy while references,
conservation and halving behavior pass in every condition. Detailed original
errors and the unchanged-bound follow-up are in `DNNE_Coupled_Outcomes_2026-11-18.md`.
The follow-up reuses completed references, halves the finest step to 0.00025 ms,
and leaves initial states and biological equations unchanged.

## Finer sensory result verified

The operator reported `cleft-refinement-20261118-884934` complete. Review
`artifacts/coupled-finer-review-20261118` verifies all 2,820 files on both C: and
D:, frozen Git source/test/probe binaries, exact prior and unchanged runtime, and
all 132 focused cases (including all twelve stationary controls). Recomputing
only saved-array/accounting comparisons exactly reproduces the final analysis.
The two-file review also has a verified D: copy.

All seven numerical conditions pass at the finest 0.00025 ms step. Worst allowed
fraction is `0.37435413377613114` for eight patches, down from
`1.5018732660967322` at 0.0005 ms. All repeated overlapping metrics, independent
reference, sampled refinement and ledger criteria pass unchanged. No fresh
reference solve was needed. This confirms the diagnosed finite-step error for
these sampled trajectories, not active physiological rest, afferent firing or
balance. All previous studies are now reviewed; `884934` is no longer pending.
