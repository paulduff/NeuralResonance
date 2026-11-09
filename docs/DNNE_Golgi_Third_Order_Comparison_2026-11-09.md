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

## Launch after the component review

The operator reported the component window complete and its C:/D:/TRX review
passed all 43 tests. The sixteen short-refinement rows are floor dominated;
they do not establish sustained firing accuracy. The component review is recorded
in `DNNE_Golgi_Third_Order_2026-11-09.md` and has its own verified D: copy.

The full comparison is now launched as `golgi-sdirk3-20261109-210c3d`, frozen
revision `faac10d04c87d48155e33946edf6a6cdb1ded763`, task
`DNNE-Golgi-Sdirk3-c7ed345c12dc`. The preparer verified unchanged solver source
against the passing component capture and copied the 102 verified reference files.
All 1,524 initial package files plus manifest have a verified D: copy at
`D:\DNNE-desktop-runs\UnityWorld\golgi-sdirk3-20261109-210c3d`.

The independent visible PowerShell window is pending operator-reported completion.
Do not inspect its live outputs. CUDA cost profiling may be prepared independently,
but do not run a timing benchmark while this comparison competes for CPU resources.

## Reported failure and verified outcome

The operator reported `FAILED`. The window completed all 38 conditions and all
43 focused tests; analysis rejected the numerical result. The offline review at
`artifacts/golgi-sdirk3-review-20261109/review.json` verifies all 2,114 files
(1,964,554,387 bytes per copy) on C: and D:, frozen source, executed binaries and
TRX coverage. Recomputing from saved arrays reproduces the original analysis
exactly, including the failure. No ODE was solved and no bound was changed.

The first review attempt imported SciPy before setting the frozen analyzer's
single-thread numerical-library environment; exact report equality failed.
The reviewer now sets those variables before numerical imports, matching the
frozen analyzer. The repeated review passes exact equality. No original capture
or reference array was modified to obtain that result.

All cases pass physical domains, stage/accounting checks and the declared
refinement checks. The five firing protocols still fail voltage, gate and
calcium absolute accuracy at 0.001953125 ms. Their ordinary finest comparisons
against the preserved tighter BDF reference are:

| Protocol | Voltage error (mV) | Gate error | Calcium error (mM) | Crossing error (ms) |
| --- | ---: | ---: | ---: | ---: |
| Quiet | 0.210635 | 0.00263938 | 1.59777e-6 | 0.000740411 |
| Depolarize | 0.253744 | 0.00266363 | 1.87226e-6 | 0.000846024 |
| Hyperpolarize | 0.0937742 | 0.000972978 | 8.13527e-7 | 0.000371219 |
| Without HCN | 0.251334 | 0.00257976 | 1.79106e-6 | 0.000801098 |
| Without SK | 0.109158 | 0.00129466 | 7.00479e-7 | 0.000360253 |
| Unchanged bound | 0.05 | 0.0005 | 5e-7 | 0.01 |

Crossing counts match and finest crossing-time and SK-occupancy errors pass.
Tightening nonlinear tolerances eightfold also passes the sensitivity bound;
the largest resulting voltage change is approximately 3.24e-6 mV. The no-NaP
protocol and both passive protocols pass overall. The old 0.00390625 ms resolution
still fails all five firing protocols; the extra resolution has not qualified
the candidate either.

For the last timestep halving, firing-protocol maximum voltage errors shrink
by 7.87–7.98 times and crossing errors by 7.83–7.88 times. This is evidence of
third-order behavior in these resolved full-trajectory errors, unlike the earlier
floor-dominated component checks. It is not a universal order proof, a waveform
phase-alignment exemption or permission to loosen limits.

A justified next numerical experiment is one further halving to 0.0009765625 ms,
retaining both ordinary and tighter nonlinear tolerances, unchanged references,
full-state comparisons and all existing bounds. Continuing the measured scaling
would predict approximately 0.032 mV worst voltage error, but that is an
extrapolation, not a passing result. Preserve/reuse the verified coarse traces
for the refinement comparison rather than silently replacing them. This follow-up
is not launched. The prepared CUDA cost window runs first without competing work;
the existing motor scheduling comparison still follows that timing report.

The completed Golgi comparison is no longer pending. No live solver selection,
physiological calibration, circuit qualification or Avatar recovery follows from
this failed result.
