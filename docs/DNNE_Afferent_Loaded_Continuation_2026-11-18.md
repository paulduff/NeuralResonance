# Connected sensory nerve continuation from checked rest

Status: launched in independent visible PowerShell as
`afferent-loaded-20261118-cd9fba`, frozen at
`8c9b28cce57660f9402c2dcea53ee9da33a648a3`. Static frozen preflight passed;
2,020 initial files plus manifest were verified in the D: backup before task
`DNNE-Afferent-Loaded-66866bb1eb52` started. Launch returned successfully.
No post-launch status, output or process inspected. The separate full refined
motor window `refined-motor-20261118-5bdd81` also remains pending and untouched.
There are exactly TWO pending completion reports; no result is inferred.

The previous successful EndpointHold trajectories lasted at most 0.0625 ms.
This study tests four milliseconds from a retained resting state, with all
72 voltage, ion and gate variables evolving freely. It asks whether a connected
hair cell, two cleft patches, calyx and seven-region nerve retain a quiet state
and respond consistently to small disturbances.

The initial state is the actual saved `2-uniform-original` native point from
`afferent-rest-20261118-62949f`. That particular search passed its stricter
internal criterion and the independently checked physical residual bounds.
Six other searches failed their internal stopping test; their failures remain
unchanged. All eighteen last native points meet the separately declared physical
residual limits. The saved-only diagnosis is recorded in the active-rest note.

Four conditions share the checked initial state: quiet, hair voltage +1 mV,
hair voltage -1 mV, and a +20 nm bundle displacement during the second of four
one-millisecond intervals. Voltage disturbances preserve the actual saved gates
and ions. No equilibrium reset occurs during integration. EndpointHold remains
an explicit candidate; the production default and all biological equations are
unchanged. The old resting Jacobian is not reused to claim stability under the
candidate kinetics. Neither geometry nor these stimuli are human calibration.

Native steps are 1/1024, 1/2048 and 1/4096 ms: twelve runs and 114,688 complete
steps. Every accepted endpoint, compensation and integrated flux is retained.
The common numerical comparison grid is 1/1024 ms. Independent Decimal-derived
Radau standard/tight and BDF tight integrations run for each condition, with
60/90-digit derivative witnesses. All references use the same declared saved
initial state; they never use native trajectory values, stages or currents.
Their own nodes, dense-output samples and precision witnesses are preserved.

Error, refinement, nonlinear and charge/ion conservation limits are inherited
unchanged from the reviewed short trajectory protocol. The new duration and
step sizes are prospective conditions. Numerical success requires agreement
between the independent methods, finest-step accuracy, refinement and flux
accounting. All native endpoints receive accounting checks; trajectory accuracy
is assessed on the common grid, not claimed at unsampled intermediate points.

Quiet drift has separate prospective limits: 1e-6 mV, 1e-8 mM and 1e-8 gate
probability over four milliseconds, checked for all native/reference quiet
runs. Peak and final departures of the disturbed cases are reported separately.
A failure to return within four milliseconds is not rewritten as numerical
failure or corrected by changing channels. Four milliseconds cannot establish
asymptotic stability, physiological spontaneous firing or normal balance.

The freezer binds 67 unchanged source files to the completed EndpointHold
capture, reuses its 333 focused tests and preserves the prior resting evidence
(196 tests). Only isolated experiment tools and a new probe dispatch are added.
Build/static checks run during authoring; all model evaluation belongs to the
independent visible PowerShell window. No live monitoring, world, brain service,
old save or editor selection is involved. Workspace and verified backup use
separate D: directories; both copies share one physical drive.


Authoring probe build passed with zero warnings/errors. Four Python modules,
PowerShell syntax, all four fixture/state layouts and the 114,688-step inventory
passed static checks without evaluating equations. The saved stopping review
has its own three-file verified D: copy. Package workspace is
`D:/DNNE-build-work/afferent-loaded-20261118-cd9fba`; backup is
`D:/DNNE-desktop-runs/UnityWorld/afferent-loaded-20261118-cd9fba`.

## Reported completion and verified saved trajectories

The operator reported the final DIAGNOSTICS_COMPLETE footer. All four conditions
pass the unchanged numerical, independent-reference, sampled-refinement and
conservation criteria. The saved quiet state also stays within its separate
prospective drift bounds. Native error fractions at the three resolutions are:

| Condition | 1/1024 ms | 1/2048 ms | 1/4096 ms |
|---|---:|---:|---:|
| Quiet | 8.79233e-9 | 8.78166e-9 | 8.74371e-9 |
| Hair +1 mV | 0.519272 | 0.0749995 | 0.0169670 |
| Hair -1 mV | 0.519291 | 0.0750023 | 0.0169677 |
| Bundle displacement then release | 0.0845764 | 0.0122073 | 0.00276124 |

These are fractions of the allowed error, not voltage amplitudes; values below
one pass. The largest finest-step fraction is about 1.697% of the bound.
At the finest step, the initial +/-1 mV disturbances have maximum voltage
departures of about 0.02089 and 0.02090 mV at four milliseconds. The bundle case
peaks at 0.40236 mV and ends at 0.03956 mV. These are maxima across the saved
voltage coordinates relative to the quiet root. Ion concentrations and channel
gates still differ from the root. This finite-time contraction does not establish
asymptotic stability, full recovery, human calibration or sensory spike output.

`tools/review-afferent-loaded-result.py` verifies both original 2,459-file copies,
frozen Git source, binaries, dependencies and preserved activation/default
choices, then reproduces the saved numerical analysis and observations exactly.
There are 114,688 advanced native steps and 114,700 retained rows: each of the
twelve runs also retains its initial state. The first review attempt stopped on
a reviewer count assertion that incorrectly counted initial rows as steps; no
study failure occurred and no captured evidence was changed. The corrected
review distinguishes the two counts. No model, test, derivative or independent
reference solve is rerun during this review.

The review output is `artifacts/afferent-loaded-20261118-cd9fba-review`, separately
backed up under `D:/DNNE-desktop-runs/UnityWorld` with the same name. It preserves
the full numerical analysis and finite-time observations. The 333 earlier
component checks and 196 earlier root checks are reused, not counted as newly
executed. Original failed resting searches, short-trajectory failures and the
production activation default remain unchanged. No further study is launched
during the operator's requested wind-down.
