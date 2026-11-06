# Isolated motor-cell numerical repair candidate — 6 November 2026

The first active comparison failed, as recorded in
`DNNE_Motoneuron_Active_Results_2026-11-06.md`. This candidate tests numerical
accumulation and reference accuracy. It does not alter cell conductances,
geometry, gating equations, inputs, thresholds or acceptance limits, and it is
not enabled in ModelNeuron, any host, Avatar or Unity.

## Compensated state accumulation

The candidate retains an addition residual for each of the 30 RK4 state entries,
including all physical variables and three accounting integrals. Each next
increment subtracts that residual before addition, then retains the newly lost
part. This is Kahan compensated summation; the principle is documented in
[CERN ROOT's numerical utility reference](https://root.cern.ch/doc/v624/classROOT_1_1Math_1_1KahanSum.html).
Its use here is a hypothesis about the observed step-dependent error, not proof
that the neuronal calculation has converged.

RHS evaluation and RK4 stages are unchanged. Non-finite states/residuals or
out-of-domain physical states fail; no clipping, normalization, resets or
accounting corrections are introduced. State and residual commit together only
after validation. The old naive mode remains the default for existing diagnostic
callers; the new active protocol explicitly selects `Compensated`. Its identity is
`isolated-motoneuron-five-compartment-rk4-compensated-v2`, reported alongside the
mode. There is no checkpoint integration or silent conversion of existing saves.

Two new unit cases precede active integration. One uses an explicitly artificial,
passive leak-free cell: total charge must equal constant current times duration.
At 1e-10 pA and 0.003125 ms, each naive voltage increment is below one representable
step. Over 100 ms, compensated addition must preserve the analytic total within
two voltage ULPs times total capacitance; the naive control must exhibit loss.
The tolerance follows representable voltage resolution, not a fitted trace.
The second verifies explicit method identity, invalid mode rejection and exact
continuation after a rejected input. These are numerical diagnostics, not
physiological stimulation or neuronal-source qualification.

## Reference cohort and unchanged gates

Protocol `motoneuron-active-comparison-v2` repeats the same ten six-second cases
and three RK4 steps. Independent references are:

| Reference | Relative tolerance | Absolute-tolerance multiplier | Maximum step (ms) |
|---|---:|---:|---:|
| DOP853 tight | 1e-10 | 0.01 | 0.125 |
| DOP853 tighter | 1e-11 | 0.001 | 0.0625 |
| Radau | 1e-10 | 0.01 | 0.125 |

The predetermined contrasts are tight/tighter and tighter/Radau. All candidate
comparisons use the tighter DOP853 reference, never whichever solver yields a
pass. The earlier ordinary DOP853 failure remains preserved in its original
capture. Radau now uses the previously prepared bounded central physical
Jacobian, with structural zero accounting columns and mandatory linear-system,
ledger-independence and manufactured-state derivative checks. Numerical warnings
fail a condition explicitly. This is not native NEURON reproduction.

All original absolute error, physical/accounting, spike-count and refinement
limits are unchanged. The preparer checks exact equality of old/new cases,
physical inputs and criteria, and freezes the verified failed baseline report,
comparison, original protocol and status. A remaining refinement failure near
floating-point resolution still fails this experiment; it cannot be silently
converted to a pass. Review the retained evidence before deciding any next step.

## Operation

`prepare-motor-axon-verification.py --motoneuron-numerical-from` takes the completed
active review directory and follows its original passive-baseline provenance. It
requires clean pushed source and verified baseline copies. The new independent
PowerShell window runs twelve focused/manifest tests, 30 candidate conditions,
Jacobian checks and 30 adaptive reference solves, then a verified D: backup.
Builds and static checks precede packaging. Numerical tests have not run at
preparation time; the operator reports completion and there is no live monitoring.

The original captures, current Golgi window and all brain saves remain untouched.
Even a complete numerical pass would leave physiological calibration, endogenous
motor-source integration, sensory feedback and Avatar recovery unqualified.
