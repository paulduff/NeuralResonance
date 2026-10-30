# Connected circuit results and support-state diagnosis — 30 October 2026

Both isolated circuits completed, but adding the selected cerebellar pathways
did not produce a usable decoded balance-error signal. The capture shows real
message delivery and contact changes, alongside substantial depletion of the
model's energy reserve. These observations do not demonstrate learned balance.

## Preserved evidence

`artifacts/postural-loop-20261030-d9ea6f` finished at 14:27:53 UTC: four preflight
cases and eight matched full cases in each of the 14- and 20-instance circuits.
All stage accounting and matched-input checks passed. All 1,230 captured files
match locally and on D:, 698 packaged files and 3,986 protected files retain
their hashes, and the manifests match. No prior state was loaded or overwritten.
The independent review is `artifacts/postural-loop-review-20261030/review.json`.

## What changed, and what did not

All **12,800 saved decoder samples** have zero population balance error and no
active righting latch. Tilt/support inputs still produce bilateral reflex drive;
quiet normal-width support also produces tonic drive. Removing the error gate
would therefore not establish correct disturbance detection. Samples are 10 ms
apart, so they do not exclude brief unsampled excursions.

The expanded circuit has 1,024 granule-to-Purkinje contacts across both sides,
with 568 recording plasticity in every full case, including muted input. Total
absolute change is 8.754991 in broad quiet support and 8.755121 during tilt/return.
Other input conditions differ slightly. The receiving teaching path is operating,
but these changes are dominated by activity also present without the disturbance;
they are not evidence of useful learned correction. Vermis receives a different
set of contacts and is not the parallel-fibre learning site in this graph.

## Energy and fading activity

For the expanded circuit's left-side quiet control:

| Population | Mean reserve at 1 s | At 4 s | At 8 s | Filtered firing at 1 s | At 8 s |
| --- | ---: | ---: | ---: | ---: | ---: |
| Vermis | 0.813 | 0.278 | 0.224 | 63.70 Hz | 4.05 Hz |
| Purkinje | 0.707 | 0.219 | 0.205 | 91.60 Hz | 31.98 Hz |
| Inferior olive | 0.892 | 0.768 | 0.787 | 34.20 Hz | effectively zero |

At 3 s, 147/192 left Purkinje cells are at the 0.2 reserve floor. At 5 s,
220/352 left vermis cells are at that floor. The reduced circuit shows a similar
vermis decline. The olive stops firing while its mean reserve remains much
higher, so reserve alone cannot be assumed to explain every population's decline.
Potassium feedback, other cell state and inhibitory input remain possible factors.

These are normalized software variables, not ATP measurements. The current
`AdvanceMetabolicState` charges **0.0028 reserve per spike**, plus synaptic and
plasticity costs. With zero neuromodulator occupancy, its recovery is at most
1.15/90 reserve per simulated second (maximum lactate/rest support). The spike
cost alone at 50 Hz is 0.14 per second: over ten times that recovery bound.
This arithmetic explains why the reserve falls under sustained activity; it does
not by itself establish how much the feedback causes firing to decline, or supply
a biologically validated replacement budget. Longer training does not correct
this supply/demand imbalance or the decoder's rate-subtraction problem.

## Targeted causal comparison

`PosturalCellProbe --support-controls` adds an isolated diagnostic, with no changes
to production biology. Fresh vermis, Purkinje and olive HH cells are compared in
a two-by-two intervention: ordinary state, reserve/fatigue held at 1/0, potassium
load cleared before each step, and both interventions. The normal in-step update
is retained, including new potassium accumulation. These are artificial controls,
not proposed operating settings or a biological repair.

Each of 48 conditions uses 32 sampled cells, 0.25 or 0.125 ms steps and 24 simulated
seconds. A quiet control is paired with glutamate at 20 Hz during seconds 8–12
and GABA at 100 Hz during seconds 16–20. All other phases have no imposed receptor
input; the existing circuit pacemaker remains enabled. The first eight seconds
of unmodified vermis cells must exactly reproduce the earlier saved spike times.
The analysis checks the full matrix, input/intervention counts, cell identity,
phase accounting, complete voltage/support traces and matched initial conditions.

All 48 short preflight conditions pass, including native reference-prefix checks
at both steps. The Release build has zero warnings/errors and the launcher parses.
No production kernel, contact rule or decoder was changed in this step; the
previous 1,009-test production regression remains the applicable baseline.

Source, executable, reference capture, scripts and preservation hashes are frozen
into a new package. It runs in a visible independent PowerShell window and backs
up to D: on completion/failure. No live agent monitoring. Avatar mechanics,
receptors, neuronal authority and the editor retain their current behavior; no
live service or world is relaunched. The next review must separate causal evidence
from physiological calibration before proposing a production energy correction.
