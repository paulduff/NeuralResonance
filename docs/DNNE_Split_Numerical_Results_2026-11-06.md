# Olive reference and fixed-input Purkinje results

The 80-condition study completed successfully on 2 November 2026 at
17:45:46 UTC. Review date: 6 November. The capture is
`artifacts/split-numerics-20261102-1e49f9`, with its D: copy under
`D:/DNNE-desktop-runs/UnityWorld/`. Source revision is
`77857bdc65b65b0272e81d705c68ec96ea119cb2`.

The fixed-input experiment separates an improving calcium calculation from
unresolved electrical spike timing. The olive comparison finds appreciable
production numerical error alongside strong initial-state sensitivity. Even
the independent reference does not establish a unique 60-second paired spike
trajectory at the tested tolerances. This is progress in diagnosis, not a
qualification of the source solver, Purkinje physiology or learned balance.

## Scope and accounting

The [frozen protocol](DNNE_Split_Numerical_Qualification_2026-11-02.md) comprises
36 production olive controls, 20 independent reference conditions and 24
receiving-cell replays. Each lasts 60 biological seconds. Four archived default
source baselines are reproduced. The saved validator reports 2,400,000 voltage/
energy rows, 3,593,568 contact rows and 367,349 events, with complete arrival
identity/timing and energy/calcium accounting. No live brain was accessed.

The read-only review reproduced the complete saved analysis exactly. All 5,303
captured files in each C: and D: copy match their recorded hashes (2,538,579,379
bytes per copy); all 4,583 packaged files and 61 unchanged runtime/contract files
also verify. The run recorded its 29,452 protected older files unchanged at
completion. This review did not repeat a new hash of every older save. Derived
results are in `artifacts/split-numerics-review-20261106`, outside the original
capture; `tools/review-split-numerics.py` reproduces them.

The independent reference excludes metabolism. Only energy-omitted production
controls can be compared with it as implementations of the same equations.
Energy-enabled controls are separate and remain necessary for eventual DNNE
qualification. All original biological parameters remain fixed in this study.

## The reference has protocol-dependent agreement

| Olive schedule | Axonal spikes, rtol 1e-7 / 1e-9 / 1e-11 | Changed burst windows, 1e-9 versus 1e-11 | First sampled voltage difference above 1 mV, last pair |
|---|---|---:|---:|
| Paired, offset 0 ms | 249 / 241 / 242 | 12 | 33.132 s |
| Paired, offset 60 ms | 234 / 235 / 236 | 3 | 38.132 s |
| Inhibition, offset 0 ms | 183 / 183 / 183 | 0 | None |
| Inhibition, offset 60 ms | 184 / 184 / 184 | 0 | 58.090 s |

Windows extend between successive actual excitatory arrivals, retaining all
off-window activity rather than counting only a narrow evoked response. The
paired reference becomes more accurate locally as tolerance tightens, but its
later burst membership still changes. The strictest reference is therefore not
an unquestionable full-minute spike oracle.

The inhibitory conditions show much better event agreement. Maximum ordinal
spike-time differences between 1e-9 and 1e-11 are 0.0000771 ms and 0.0021016 ms
for the two offsets, with identical counts in every burst window. Their maximum
sampled voltage differences are 0.02467 mV and 1.19124 mV. Large voltage
differences near a steep spike must be interpreted alongside event timing;
voltage difference alone cannot measure functional disagreement.

Perturbing initial soma voltage by +/-0.001 mV at the strictest tolerance moves
paired outputs from 242 spikes to 232/240 at offset 0, and from 236 to 227/227
at offset 60. It changes 28/34 and 37/32 response windows respectively. The
inhibitory conditions retain 183/184 spikes and their window counts under those
same perturbations, although timing moves by up to approximately 0.202/0.135 ms.
This demonstrates sensitivity of the implemented driven system, with different
outcomes across protocols. It does not establish deterministic chaos, physiological
variability, or permission to disregard numerical error.

## Production receptor coupling is a concrete accuracy target

Energy-omitted production counts at fixed 0.025 ms membrane ceiling are:

| Schedule | Coupling ceiling 0.025 / 0.0125 / 0.00625 ms | Strict reference | Changed burst windows at finest coupling |
|---|---|---:|---:|
| Paired, offset 0 | 230 / 240 / 245 | 242 | 36 |
| Paired, offset 60 | 225 / 233 / 236 | 236 | 35 |
| Inhibition, offset 0 | 189 / 190 / 185 | 183 | 2 |
| Inhibition, offset 60 | 189 / 184 / 185 | 184 | 1 |

Equal totals can conceal different bursts: the finest paired offset-60 result
has the reference's 236 spikes but differs in 35 windows. Refinement does not
monotonically converge the full-minute source trajectory.

At the default coupling, sampled voltage first differs from the strict reference
by more than 1 mV at 5.192 s / 2.274 s for the two offsets. At the finest coupling
these times become 7.212 s / 8.230 s. The short pilot already showed reduced
early error under coupling refinement. The full capture confirms maximum sampled
errors over its first four seconds of 0.617 / 0.207 / 0.037 mV for offset 0, and
1.096 / 0.375 / 0.076 mV for offset 60 as coupling is refined. Reducing only the membrane ceiling at
the coarsest coupling does not remove the early discrepancy. At 0.00625 ms
coupling, both tested membrane ceilings produce identical source results because
the surrounding coupling step already limits the membrane's allowed interval.

The current implementation freezes receptor conductances during membrane
substeps. Its membrane uses RK4 with slope/stiffness limits, but has no embedded
local-error estimate. The results support improving and measuring this coupling;
they do not identify it as the sole source of long-trajectory differences.
The energy-enabled refinement also changes burst membership and remains open.

## Fixed incoming spikes improve the learning comparison

Every Purkinje refinement receives the same archived source messages, emission
times, conduction delays and parallel-fibre stimuli. No regenerated olive train
can account for differences among these receiving-cell cases.

For intact calcium learning, final active-contact induction differences shrink
at each successive refinement. At the last refinement, 0.03125 to 0.015625 ms,
the differences are approximately 0.099% and 0.082% of the finer induction value
for the two offsets. The active calcium peaks are approximately 1.07658 and
0.90587 micromolar at the finest step.

This review also checks the whole sampled calcium trajectory. Maximum active-
contact calcium differences in the last refinement are 0.001555 / 0.001521
micromolar; integrated-calcium differences are 0.0000868 / 0.0002056 micromolar.
Thus similar final induction is accompanied by small sampled chemistry
differences for this fixed input. The retained active induction maximum is first
sampled at 3.350 / 6.400 s, so it still cannot establish repeated-teaching fidelity.

Stored active-contact weight losses after 60 s remain only approximately
0.004846% and 0.003076%. Dormant contacts on the same taught cell still lose
approximately 0.001419% and 0.001293%. Removing calcium entry or disconnecting
climbing input leaves the stored weights at 1 at saved float precision; tiny
biochemical induction can still be nonzero. Numerical consistency of these small
effects does not establish physiological magnitude, contact selectivity or
adequate repeated teaching. The retained-maximum induction approximation remains.

## Fixed-input electrical output still needs attention

Intact-calcium spike counts at the four receiving ceilings are:

| Offset | Cell | 0.125 / 0.0625 / 0.03125 / 0.015625 ms |
|---|---|---|
| 0 ms | Taught | 3663 / 3661 / 3661 / 3661 |
| 0 ms | Other cell | 3537 / 3536 / 3536 / 3541 |
| 60 ms | Taught | 3661 / 3661 / 3660 / 3660 |
| 60 ms | Other cell | 3544 / 3544 / 3544 / 3544 |

At the finest pair, the offset-0 taught cell's maximum ordinal timing difference
is 0.328125 ms, but the other cell gains five spikes. At offset 60, equal counts
still accompany maximum ordinal differences of 15.109375 ms and 2.390625 ms.
These comparisons do not establish converged spike timing. Ordinal alignment
can also exaggerate a local mismatch, so it must not be read as a calibrated
spike-distance measure.

The other cell receives its original parallel input but no olive climbing input.
Its spike trains are identical between intact and entry-blocked learning at every
resolution. Its resolution dependence therefore persists with calcium entry
blocked and identical external events. This narrows the investigation to the
receiving electrical/support dynamics and their numerical coupling.

Disconnecting climbing input does introduce small timing differences in this
other cell: up to 0.125 ms at the coarsest step, and 0.0625 ms for offset 0 at
the next step. The two finest resolutions are exact across all three variants.
The runtime splits the integration interval for **every neuron** at any scheduled
arrival. Removing climbing events therefore removes some numerical boundaries
even for a cell that never receives them. This is a concrete route for numerical
influence between cells; a controlled test retaining those boundaries without
delivering a spike is needed to attribute the observed difference causally.
It must not be described as biological cross-cell learning.

Source inspection shows that the receiving model advances receptors, support
variables, reduced dendritic compartments, HH membrane, activity feedback and
metabolism in sequential stages. Refining the receiving step changes all of
those couplings. The membrane itself already uses RK4. Neither replacing a
nonexistent Euler membrane nor increasing learning strength is a supported fix.
The HH soma, provisional drives and reduced climbing compartment remain
physiological approximations requiring cell-specific calibration.
In particular, `IntrinsicCircuitDriveTopology` still supplies Purkinje with an
imposed oscillatory drive, and `ModelNeuron` adds a reduced tonic drive. The
olive's earlier removal of imposed oscillations did not remove these receiving-
cell terms. This experiment validates neither endogenous Purkinje pacemaking
nor a complete biological Purkinje cell. That limitation must remain explicit
when returning to cell-specific electrical and learning calibration.

## Next work

1. Test the receiving neuron's shared arrival-boundary effect with diagnostic
   no-op boundaries, starting with the cell that receives no climbing input.
   Record receptor, membrane and support states to locate the first discrepancy,
   then assess per-neuron event integration or error-controlled coupling. Preserve
   biological mechanisms while examining update order and numerical accuracy.
2. Measure olive integration error in short intervals starting from identical
   complete diagnostic states, including receptor states and event phase. Use
   states near the first divergence as well as rest. Add error estimation to
   receptor/membrane integration and compare against a reference whose own
   accuracy is checked within each interval. Metabolic feedback requires its
   own matched reference before qualifying the energy-enabled source.
3. Retain long runs, but assess their burst/timing distributions over a declared
   ensemble of small perturbations alongside short-interval accuracy. The current
   two perturbations are a sensitivity diagnostic, not a population validation.
   Set acceptance tolerances before using results to qualify a replacement.
4. Then return to the local calcium-dose, selectivity and repeated-teaching model,
   followed by fastigial/vestibular integration, balance readout and Avatar recovery.

Diagnostic state copying must capture every relevant dynamic variable; schema-4
learned-state warm starts are not exact continuation. No live cell should be
periodically reset to force agreement. This review changes no production model
and launches no further experiment.
