# Prospective full Golgi coupled-solver comparison

The completed reference continuation supports a numerical repair of the original
split solver, not a change to channel biology. The isolated candidate and short
component checks are described in `DNNE_Golgi_Coupled_Solver_2026-11-09.md`.
This longer study is implemented and builds, but **has not run**. Its launch
requires the operator-reported component result and a verified passing review.

## Fixed conditions and evidence

`golgi_sdirk_protocol.py` defines the versioned input before execution. It keeps
23 C, initial -60 mV, the original morphology/channels and six active protocols:
quiet, +50 pA, -50 pA, and separate persistent-sodium, HCN and SK removals.
Each lasts 6000 ms; injected currents occur from 2000 through 3000 ms. Three
fixed steps are 0.015625, 0.0078125 and 0.00390625 ms. Each active protocol also
has a finest-step control with eightfold tighter nonlinear tolerances.

Two passive protocols apply 25 pA at soma or electrode during 20–60 ms of a
100 ms run, each at the three steps. Total: **30 conditions**, including 24
active runs. There is no imposed time limit, automatic retry, physiology fit,
clamping or live brain access. An ordinary condition failure retains its partial
trace, error and diagnostic counters; the probe proceeds to the other conditions.
Process-level interruption or resource exhaustion can still stop the study.

Every 0.25 ms the probe records time and all 161 physical state values: 135
cable voltages relative to -55 mV, 18 gates, six SK occupancies and both calcium
pools in micromolar. Binary rows contain 162 little-endian float64 values.
The accounting CSV records charge, calcium entry/removal, maximum stage residuals,
mass errors and Newton work. All somatic crossing times are retained separately.
These are observable physical traces, **not exact restart checkpoints**: solver
work arrays and compensation residuals are not exported.

The already reviewed active BDF/Radau arrays and original control traces are
copied from `golgi-resume-20261109-3b4dff`, verified against both C: and D:.
No six-second active reference is recomputed. The original passive reference
arrays were not saved; the window computes and preserves both original BDF
tolerance settings at both injection sites. Their original 0.000002 mV agreement
bound remains required. The reference preflight happens before long candidate
integration. Python, NumPy and SciPy versions and dependency hashes are recorded
and compared again at analysis completion.

## Acceptance fixed before execution

The original twelve active reference comparisons must reproduce exactly and pass
their original soma/timing/count criteria. In addition, full-state BDF/Radau
differences must be at most 10% of the candidate absolute limits below. This is
a reference uncertainty gate, not a claim that either finite-tolerance trace is
mathematically exact.

Both finest candidate runs must meet the following maxima against **both** BDF
and Radau. State comparisons cover all 133 capacitive voltages, all gates, all
SK occupancies and both calcium pools. The two massless junctions are preserved
in the candidate trace and checked through the Kirchhoff residual; the saved
reference arrays omit their algebraically eliminated coordinates.

| Quantity | Absolute limit |
| --- | ---: |
| Voltage | 0.05 mV |
| Ordinary gate | 0.0005 |
| SK occupancy | 0.0005 |
| Calcium concentration | 0.0000005 mM |
| Corresponding somatic crossing time | 0.01 ms |

All active resolutions must have matching crossing counts. Refinement is assessed
against the preselected BDF reference. Each finer maximum error must be no larger
than the greatest of 1.1 times the coarser error, twice the measured BDF/Radau
difference, and 1% of that quantity's absolute limit. The small declared floor
avoids making monotonic improvement below a meaningful accuracy budget a separate
requirement. It is prospective, not fitted to candidate results. The existing
failed motor-cell study and its original refinement bounds remain untouched.

Default/tighter nonlinear-tolerance finest traces must agree to within 1% of each
absolute limit, with equal crossing counts. Passive finest voltage error must be
at most 0.0002 mV with no spikes; passive refinement uses the same ratio/floor
rule and the independently measured passive tolerance spread.

Every condition must remain finite and within the cell's declared gate/calcium
domains, with complete sample grids and ordered crossing times. Per-stage and
sampled accounting are both checked. Prospective bounds are 0.02 fC charge-ledger
error, 1e-9 mM calcium-ledger error, 1e-5 pA Kirchhoff residual, 1e-7/ms gate/SK
stage residual, 1e-10 mM/ms calcium stage residual, 1e-8 SK mass error and at most
one scaled nonlinear residual unit. Recorded maxima must be monotone and match
final metadata. No failed or incomplete condition can qualify through averaging.

## Execution and review

1. Await the operator's component completion report. Only then run
   `review-golgi-sdirk-components.py CAPTURE NEW_REVIEW --operator-reported`.
   It checks the full C:/D: file manifests, original source/binaries and all
   32 TRX results, including exact class counts. Back up the new review to D:.
2. With clean, pushed source, run `prepare-golgi-sdirk.py --component-review REVIEW`.
   It requires that verified review, confirms that the numerical source matches
   the reviewed components except for physical trace export, and freezes all
   source/input/reference provenance. It performs no integration or tests.
3. Verify the new package's initial D: backup, then use its frozen
   `start-golgi-sdirk-window.ps1`. It opens an independent visible PowerShell
   window with no execution time limit and remains open when done.
4. The window repeats 32 component cases, runs reference preflight, captures all
   conditions, analyses complete traces, verifies source/binary/dependency hashes
   and writes the final D: backup. No live monitoring; inspect only after the
   operator reports completion. Keep both original and failed evidence.

Release probe and test builds pass with zero warnings/errors. Python syntax and
PowerShell parsing pass; these are preparation checks, not runtime results.
Even a successful numerical study would leave native NEURON reproduction,
active axonal propagation, metabolic calibration, granular-layer integration,
physiology and Avatar recovery unqualified. No host selects this candidate.
