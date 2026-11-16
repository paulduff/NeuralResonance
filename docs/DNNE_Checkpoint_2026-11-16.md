# DNNE / Avatar / World checkpoint — 16 November 2026

The operator requested notes, commit/push and a pause until tomorrow. Preserve
the full brain/body/world goal. No new experiment is to start during this pause.
All experiments launched in this sequence have now been operator-reported and
reviewed; no completion report is pending. No live brain, Unity world or body
was started during this sequence, and no live service state was polled.

## Completed and preserved

- Native crossing gate `native-crossing-20261116-0b35ae`: all 124 cases pass,
  including active-membrane state preservation and passive receptor/receiver
  checks. Five original six-second source conditions reproduce exactly; each
  of the four active conditions retains its 28 original events. The review
  verifies 2,284 files per C:/D: copy and 1,704 Git files. The two-file
  `native-crossing-review-20261116` review is separately D: verified.
- Retained spindle transport `spindle-characteristics-20261116-7c6db6`: twelve
  controls, 25,308 saved translations, 31 continuity probes, 144 histories and
  96 cadence contrasts pass. All 8,208 saved history endpoints reconcile.
  Maximum shape L1 error is 5.6455e-16, force error 5.8208e-11 Pa and second
  moment error 1.4211e-14 nm². Shape/force agree exactly across the tested
  movement subdivisions. The review verifies 2,093 C:/D: files, 1,710 Git
  files and unchanged numerical dependencies. Its one-file
  `spindle-characteristics-review-20261116` review is separately D: verified.
- The original receptor numerical comparison `f7ece6` remains FAILED and
  preserved. The original spindle remapper and its completed `ba9648` run
  remain preserved too. Their limitations are not erased by later results.

No test or model was rerun while reviewing those completed results. Every
experiment ran in an independent visible PowerShell window without monitoring.

## Next spindle work

The material-coordinate representation removes the demonstrated projection
diffusion in transport-only tests. It is not a complete intrafusal muscle model.
Next design and qualify attachment/detachment together with retained transport,
actin activation and filament overlap, before an unloaded/slack root controller
or sensory membrane is added. New attachment must be represented at its actual
strain coordinates; observing a grid must never become a hidden state reset.
Strain-dependent detachment can change the within-interval density, so its
representation and error control must be explicit. Account for representation
growth and computational cost as well as accuracy.

Source inspection located the canonical Bag/Chain classes inside the completed
capture's `reference-source/canonical-state/.../spindle_model/` tree, and the
independent fixed-length equations in `tools/spindle_fixed_reference.py`.
The source attachment term is proportional to detached myosin times
`active - total_bound`; despite one variable's name, that active state therefore
includes occupied sites. Off-rate constants differ between Bag and Chain.
Preserve the two conservation ledgers and review moving-overlap admissibility
before coding a coupled update. No new coupled equation or solver was authored
or executed during this source inspection. The existing fixed-length results
and their original bounds remain the starting reference.

## Remaining native timing work

The optional cubic estimator is component-qualified, with a distinct cell/source
version and event namespace. Linear remains the default. The full 18-condition
receptor comparison, its independent reference/refinement checks and 36 active
source/Avatar receiver cases still need cubic-mode execution.
The current full recorder/source probes select the default linear mode; their
explicit mode selection and version-aware audits must be updated and qualified
before preparing that run. Do not weaken the original archive/binary guards or
assume a saved-endpoint interpolation result proves native event production.

## Scope and operating constraints

DNNE remains neuronal authority; Avatar owns the body and nervous system; World
and Maze are environments. LM/Entity/Dyad stay parked and CIV separate. No live
selection, editor interface, Unity physics, old save or biological parameter was
changed by these isolated candidates. Physiological calibration, closed-loop
neuronal control, balance/recovery and the full CPU/CUDA objective remain open.

Resume only when the operator asks. Keep all future tests/comparisons/benchmarks
in visible independent PowerShell windows, preserve frozen evidence and D:
backups, wait for operator completion reports, and keep worlds paused on startup.
