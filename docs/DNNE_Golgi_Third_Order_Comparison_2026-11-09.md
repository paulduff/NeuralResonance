# Prospective third-order Golgi reference comparison

This rig extends the full study in `DNNE_Golgi_Coupled_Comparison_2026-11-09.md`
to the explicit third-order candidate described in
`DNNE_Golgi_Third_Order_2026-11-09.md`. The earlier second-order failure and every
numerical acceptance limit are preserved. This preparation does not inspect the
pending component window, run an ODE, or establish its outcome.

## Fixed comparison

The original six active protocols remain: quiet, +50 pA, -50 pA, no persistent
sodium, no HCN and no SK. Each lasts 6,000 ms at 23 C from -60 mV, with the same
2,000–3,000 ms input interval. Passive soma/electrode protocols retain 25 pA over
20–60 ms of 100 ms. Morphology, channel/calcium equations, somatic threshold and
linear endpoint crossing interpolation are unchanged.

Four constant steps are declared before execution: 0.015625, 0.0078125,
0.00390625 and 0.001953125 ms. Each active protocol also runs the finest step
with eightfold tighter nonlinear tolerances. This gives 30 active and eight
passive conditions: **38 total**. The extra resolution investigates whether a
finer step is needed after changing order; it does not alter an acceptance limit.
Analysis reports the original 0.00390625 ms absolute result separately, so an
improvement requiring the extra resolution cannot masquerade as a success at
the old resolution.

All physical states and accounting are sampled at the same 0.25 ms cadence.
Every somatic crossing is retained. The candidate records partial failures and
attempts the remaining conditions; incomplete conditions cannot qualify.
No imposed deadline, automatic retry or live DNNE/Avatar execution is introduced.

## References and unchanged gates

The 102 copied reference/provenance files come from the reviewed, preserved
second-order full capture. They include the original active BDF/Radau arrays,
reference source and the passive arrays already computed at both tolerance
settings. Preparation verifies selected bytes on both C: and D:. The new visible
window rechecks the references and uncertainties before integration; it does not
solve those references again. New dependency metadata is recorded for the
analysis, while reference provenance remains tied to the original capture.

All existing limits apply: 0.05 mV voltage, 0.0005 gate/SK occupancy, 5e-7 mM
calcium and 0.01 ms corresponding crossing time, with matching counts, complete
grids and physical domains. Both finest active runs must satisfy both references.
All ordinary resolutions must pass the existing count/refinement conditions.
Reference uncertainty, nonlinear sensitivity, passive accuracy, Kirchhoff balance,
charge/calcium ledgers, SK mass and stage residuals retain their previous gates.
No phase alignment or replacement of waveform comparisons by spike counts is used.

The shared analyzer still reproduces the earlier 30-condition analysis exactly,
including its failures. The default second-order protocol reproduces the saved
JSON exactly. Its original criteria dictionary equals the new protocol's criteria.
This offline audit is saved in `artifacts/golgi-sdirk3-preparation-20261109/regression.json`.
It rereads reported-complete evidence only; no solver or runtime tests ran inline.

## Launch prerequisites

The third-order component window must first be reported complete by the operator.
`review-golgi-sdirk-components.py CAPTURE NEW_REVIEW --operator-reported` then
verifies all C:/D: files, executed binaries and 43 test results, including all
sixteen saved active-refinement rows. It distinguishes resolved ratios from
floor-dominated errors. Back up this review before preparation.

`prepare-golgi-sdirk3.py --component-review REVIEW` requires that passing review,
clean pushed source, and identical solver/biological source to the components.
It freezes a new package; it cannot accept the old second-order component result.
After the initial D: backup, the frozen `start-golgi-sdirk-window.ps1` repeats
all 43 component tests, checks references, captures 38 conditions, analyses them,
verifies source/binary hashes and backs up final results. It selects `--sdirk3`
explicitly; the old probe mode still selects second order.

The extended probe builds with zero warnings/errors, Python/PowerShell syntax
checks pass, and the saved-analysis regression passes. The longer window has not
been frozen or launched. Await the component report without live monitoring.
Even full numerical agreement would leave physiology, active axonal propagation,
metabolic/circuit integration and Avatar recovery open. It would not establish
the order of the retained event interpolation or whole-brain performance.
