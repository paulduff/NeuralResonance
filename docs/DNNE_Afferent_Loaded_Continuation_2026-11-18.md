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
