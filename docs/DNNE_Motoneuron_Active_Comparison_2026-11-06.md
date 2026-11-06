# Active motor-cell numerical comparison

The operator-reported result is now recorded in
`DNNE_Motoneuron_Active_Results_2026-11-06.md`. The original run failed its numerical
criteria. Its next explicitly versioned candidate is described in
`DNNE_Motoneuron_Numerical_Repair_2026-11-06.md`; the protocol below remains the
historical specification of the first run, not a new passing result.

This is the next isolated gate after `DNNE_Motoneuron_Passive_Results_2026-11-06.md`.
The five-compartment candidate and its parameters are unchanged. No live DNNE
host, Avatar, Unity service, editor or saved brain selects this model.

The source remains the low-threshold cat MG cell from
[Powers and Heckman (2017)](https://doi.org/10.1152/jn.00850.2016),
[ModelDB 239582](https://modeldb.science/239582) revision
`0d11354490fad5fcd940d016a8d69b000693fd81`. Fixed reversal baths, local calcium
removal and missing energetic/homeostatic coupling retain the limitations in
`DNNE_Motoneuron_Reference_2026-11-06.md`. This is not a calibrated human motor pool.

## Fixed experiment

`tools/motoneuron_active_protocol.py` declares ten six-second protocols before
execution. All start at -65 mV with local gate equilibria and somatic calcium
0.0001 mM. Initial settling is retained; steady state is not assumed. These
inputs are diagnostic choices, not published stimulation protocols.

| Case | Input |
| --- | --- |
| Quiet | No applied current or synaptic conductance |
| Three somatic steps | 1,000, 3,000 or 6,000 pA from 1,000 to 3,000 ms |
| Brief somatic input | 20,000 pA from 1,000 to 1,002 ms |
| Dendritic excitation | 25 nS per branch from 1,000 to 3,000 ms |
| Matched inhibition | Same excitation, plus 100 nS inhibitory conductance per branch from 1,800 to 2,200 ms |
| Without fast sodium | The 6,000 pA case with transient sodium conductance removed |
| Without dendritic calcium | The excitatory case with L-type calcium conductance removed |
| Without calcium-activated potassium | The 3,000 pA case with KCa conductance removed |

All remaining time has zero diagnostic input; no reset occurs after withdrawal.
Excitatory reversal is 0 mV and inhibitory reversal -80 mV. Conductance is per
dendritic branch, not a pool firing-rate proxy. Ablations are diagnostic only.

Each case uses RK4 steps 0.0125, 0.00625 and 0.003125 ms: 30 candidate conditions.
Every condition records all 30 state variables at 0.1 ms intervals and every
interpolated upward -20 mV soma crossing. Integration splits at exact input
boundaries. Crossings are observations, not reset events or proven axonal
propagation. A failed condition retains its partial trace and fault; other fixed
conditions still execute. There is no automatic restart or parameter search.

## Independent reference and criteria

`tools/motoneuron-active-reference.py` reuses the previously checked source-derived
Python gate rates and independently expresses coupled currents and calcium
equations. It reads geometry and conductances from pinned MOD/HOC declarations,
not candidate metadata. Dense linear algebra reduces the seven-node passive
graph; reconstructing diagonal terms from off-diagonal edges preserves internal
charge conservation despite cancellation during elimination.

Three integrations cover every protocol: DOP853 at rtol 1e-8, tighter DOP853 at
1e-10, and Radau at 1e-10. Absolute tolerances are scaled separately for voltage,
gates, calcium and charge. Maximum steps are 0.25, 0.125 and 0.125 ms. Each real
input transition ends an interval and carries all state forward. Dense output
supplies shared sample times; event roots supply reference crossings. Original
HOC/NMODL and native NEURON are not executed.

Both ordinary-versus-tight DOP853 and tight-DOP853-versus-Radau comparisons must
meet the reference bounds. Candidate error always uses tighter DOP853, never
whichever method produces a smaller discrepancy.

| Quantity | Reference agreement | Finest candidate |
| --- | --- | --- |
| All voltages | 0.005 mV | 0.05 mV |
| All gates | 5e-5 | 5e-4 |
| Somatic calcium | 5e-8 mM | 5e-7 mM |
| Ordinal crossings | Equal counts and 0.001 ms | Equal counts and 0.01 ms |

Charge residual must stay below 1e-5 fC and calcium-ledger residual below 1e-10 mM.
Every candidate step must retain physical states and accounting. Reference domain
tolerances are 1e-10 for gates and 1e-13 mM for calcium; no saved state is altered.
Non-finite, missing or incomplete evidence fails.

Each refinement must not worsen voltage, gate, calcium or crossing-time error
beyond 1.1 times its predecessor or twice measured reference uncertainty,
whichever is greater. All candidate steps must have matching crossing counts.
Any failed condition/reference prevents the overall numerical pass.

Quiet agreement is insufficient: the strong somatic case must exercise
positive-going action potentials and elevated calcium in all three references.
Otherwise the experiment is inadequate for active verification, even with small
numerical discrepancies.

Window counts, full crossings, voltage extrema, calcium peaks and post-brief
voltage minima are retained for physiological review. Numerical agreement does
not automatically qualify recruitment, AHP, hysteresis or inhibitory recovery.
Persistence after withdrawal must be interpreted alongside the model's persistent
currents and matched ablations, not automatically labelled success or malfunction.

## Operation and next gate

Preparation uses `prepare-motor-axon-verification.py --motoneuron-active-from`
with the reviewed passive-results directory. It requires the accepted review
and matching repaired C:/D: manifests, freezes pushed source, pins inputs, and
retains the original backup failure as history. It uses the long-path backup repair.

Build and static parsing passed before launch. No active numerical result is
claimed here. The visible PowerShell window runs ten focused/manifest tests,
30 candidate conditions and 30 adaptive reference solves. It has no imposed
duration ceiling or live monitoring; the operator reports the outcome. Failed
criteria require review, not retrospective relaxation of acceptance bounds.

After numerical review, calibrate source behavior and causal channel dependence,
then connect original identified cell events through the checked transport to the
calcium/force consumer. Further integration must preserve Avatar's receptors and
mechanics and replace the existing derived motor-rate/control path. Whole-body
balance and recovery remain unqualified.

## Reported reference warning: prepared correction, not yet tested

While `motoneuron-active-20261106-df5b5b` was still running, the operator supplied
overflow warnings from SciPy 1.16.3 `integrate/_ivp/common.py` lines 326, 356,
357 and 378. No running logs, status or results were inspected in response, and
the package/process was not changed or restarted.

The installed source and [pinned SciPy source](https://github.com/scipy/scipy/blob/v1.16.3/scipy/integrate/_ivp/common.py)
show an adaptive finite-difference perturbation factor that grows when a column
produces little change. The reference has three structural zero columns: the
calcium-entry, calcium-removal and charge integrals at state indices 27–29 do not
feed back into the RHS. Escalation on those columns is a plausible trigger,
not yet a reproduced diagnosis of the particular running warning. A warning
inside Jacobian estimation does not by itself prove overflow of the physical
trajectory, and numerical acceptance cannot be inferred from continued execution.

The next-source candidate supplies a bounded central-difference Jacobian for the
27 physical variables, with the three accounting columns explicitly zero. Each
perturbation is recomputed from the current variable and a declared physical
scale; no factor accumulates across calls. The ODE, inputs, solver tolerances and
comparison bounds are unchanged. This is an approximate Newton derivative, not
an analytic Jacobian or an increase in claimed physiological accuracy. Its mode
is explicitly recorded as `motoneuron-bounded-central-physical-v1` in future
protocols/reports. The original frozen package continues to use its original code.

Before any future adaptive comparison, the prepared rig will require a known
linear-system check (including very large passive totals), exact ledger
independence and 40 manufactured-state directional-derivative checks. The latter
cover four channel blocks, five voltages and two calcium values, with interior
gate probabilities to avoid crossing a nonsmooth flux-rectification boundary.
Non-finite Jacobians fail. Future reference solves also turn overflow/invalid-
arithmetic warnings into recorded condition failures, without clipping states.

These new checks have not run yet. They must pass in a visible PowerShell package,
followed by unchanged cross-method/step criteria. Await the operator's completed
original run before deciding which retained comparisons need to be repeated.
