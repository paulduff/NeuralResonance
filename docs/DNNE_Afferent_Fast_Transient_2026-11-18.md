# Joint sensory nerve transient capture: authoring checkpoint

Status: fixtures and native capture authored; **independent trajectory driver,
saved-trace analysis, review prerequisite and launcher are not yet implemented**.
No package or test window exists for this comparison. The immediate execution
step remains the independent derivative study, followed by the joint dynamics
component study. This partial capture is not a completed qualification rig.

`tools/afferent_trajectory_protocol.py` declares seven conditions: matched
quiet/bundle single-patch states, initially loaded and reverse-current states,
a severed contact, graded two-patch and bundle-driven eight-patch states.
Each uses the declared seven-compartment nerve geometry and actual initial
gates. Matching initial cable and calyx voltages removes the initial interface
voltage jump in selected controls; it does not imply a stationary state.

The grid is 262,144 ticks/ms, with native steps of four, two and one ticks
(approximately 0.00001526, 0.00000763 and 0.00000381 ms). Common samples occur
every sixteen ticks, including the rapid initial capacitive transient. Durations
are 0.015625 or 0.0625 ms. These short intervals are intentional numerical
transient checks and cannot establish sustained firing or physiological rest.
There are 21 prescribed native runs, retaining every accepted endpoint.
Static fixture inspection reconciles 114,709 endpoint records, including the
initial state in each run, unique state/ledger labels and aligned intervals.
The prospective joint-component inventory remains 181 cases; none ran here.

The planned independent comparison has Radau standard/tight and BDF tight
references, separately evaluated equations, actual free gates and all charge/
ion ledgers. Original sensory state/ledger tolerances and refinement criteria
are retained; cable/interface charge residual bounds are declared as 2e-8 fC.
All accepted reference nodes and comparison samples must remain admissible.

The independent membrane/cable/joint evaluator now has an explicit optional
`allow_trial_gates` argument, default false. Ordinary derivative comparisons
retain their strict physical gate-domain checks and unchanged equations. A
future implicit reference solver may request smooth polynomial gate extension
for internal Newton trials, as the earlier sensory reference does; accepted
states still require [0,1] gates. No clipping or alternate current is introduced.
This option has not been numerically exercised during authoring.

`AfferentTrajectoryStudy` captures initial state/derivative, all accepted states,
compensated ledgers, clock/nonlinear diagnostics and complete failure prefixes.
It is available only through the explicit probe command, with no live caller.
The authoring probe build passes with zero warnings/errors. Fixture/static
validation is separate from runtime checks; no model or test has run.

Finish the independent reference, saved-trace accounting and guarded execution
workflow after the earlier gates. Preserve all failures and original bounds.
Longer free trajectories, loaded roots and spike-event production follow; do
not interpret these short traces as a usable vestibular nerve.
