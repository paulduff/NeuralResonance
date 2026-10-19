# Fresh postural circuit: results and next diagnostic

The corrected fresh circuit transmits spikes and records contact plasticity, but
does not produce a usable decoded balance error. Resetting state was useful for
establishing a baseline; it did not establish balance or recovery. A specific
decoder cancellation is now reproducible from the saved observations.

## Evidence and preservation

Run: `artifacts/fresh-postural-20261019-3e12d4`, revision
`bc078240497da2fe16baedaed90ce61b28927798`. Four short preflight cases and eight
eight-second controls finished with exit 0 at 14:57 UTC on 19 October. Delivery
accounting, fresh initialization and the declared input controls passed. All 715
files in the final manifest match both the local capture and the D: copy; all 244
protected old files and 437 packaged inputs retain their recorded hashes.

`tools/analyse-fresh-postural.py` independently verifies those hashes, reconciles
learning summaries with persisted contacts, reconstructs the captured decoder and
compares equal time windows against quiet input. It writes into a new directory;
the original captures and manifests remain unchanged. Output:
`artifacts/fresh-postural-review-20261019/fresh-review.json`.

This was fourteen isolated DNNE instances, not the whole brain or a moving body.
Each condition had new membranes and zero loaded connections. Avatar's physical
transducer/labyrinth receptor fixtures were retained. The current Avatar balance
and neuronal motor bridge were inspected again; no substitute body controller or
live reset is part of this review.

## What the observations establish

| Input | Vestibular nucleus spikes, both sides over 8 s | Peak decoded error | Sampled righting latch |
| --- | ---: | ---: | ---: |
| Broad-support quiet | 1,551 | 0 | 0 |
| Normal-support quiet | 1,551 | 0 | 0 |
| Tilt and return | 1,609 | 0 | 0 |
| Support displacement and return | 1,550 | 0 | 0 |
| Head motion and return | 1,560 | 0 | 0 |
| Captured trajectory | 1,603 | 0 | 0 |
| Muted tilt | 1,432 | 0 | 0 |
| Left-only tilt | 1,503 | 0 | 0 |

The initial two seconds of all region traces match exactly between the broad
quiet, tilt, support and head-motion cases. During the matched 2–6 s window,
vestibular nuclei emit 642 spikes for tilt versus 581 for quiet, a difference of
61. The final 6–8 s window is 186 versus 189. Comparing the first and middle phases
of one case alone would confound the response with the common initialization
transient. These are single deterministic runs; they do not establish general
physiological sensitivity or statistical significance.

Tilt also produces a sampled bilateral afferent/descending drive with a peak of
0.562. Its drive exceeds the current 0.04 entry requirement on 375 of 400 stimulus
samples. Thus some disturbance information reaches the spinal pathway, even
though the separate balance-error requirement blocks sampled latch entry.

In the tilt case, 2,824 contacts exist and 2,635 record nonzero accumulated
plasticity; telemetry records 365,974 presynaptic and 275,181 postsynaptic contact
events. Counts are contact events, not distinct cell spikes. Accumulated change
does not imply a useful final weight or learned behaviour. Muted input still
produces firing and plasticity. Eight seconds cannot qualify long-term learning,
stability or generalisation.

## The cancellation and its limits

The captured `VestibuloReticularPopulationDecoder.Compose` computes:

```text
error = max(0, mean vestibular rate
               - 0.55 * mean vermis rate
               - 0.25 * mean spinal rate)
```

Broad quiet means are about 0.379 Hz vestibular, 34.899 Hz vermis and 4.732 Hz
spinal. The unrectified result is negative at every saved sample in every case
(overall approximately -26.780 to -10.598). Applying this formula reproduces all
6,400 saved zero error samples exactly. Righting entry additionally requires an
error of at least 0.40. This is a direct explanation of the sampled failure, not
evidence that the physical body was stable. Error and latch samples are 10 ms
apart, so unrecorded brief transitions cannot be excluded; spike totals cover
every integration step.

The vermis's sampled mean rate and cumulative output count match quiet in all
eight conditions. This does not prove that every individual cell or membrane is
insensitive. In the tilt case only 26 of 352 cells per vermis instance own incoming
contacts. About 95.7% of integrated vestibular-nucleus input events come from
FastigialNucleus (96.3% in quiet). Counts identify the dominant input source; they
are not a causal ablation or a measurement of current contribution.

There are no changing vermis contacts in this experiment. The reduced circuit
omits CerebellarGranule and InferiorOlive; its persisted vermis contacts originate
from vestibular nuclei. The repaired cerebellar rule requires locally arriving
olive input and coactive granule contacts. This rig therefore cannot assess that
learning rule in its intended circuit. It must not be presented as a complete
cerebellar learning test.

Simply dropping the error requirement would be unjustified: normal-support quiet
already exceeds the reflex-drive requirement in 799 of 800 samples. Similarly,
changing the two subtraction coefficients to make this run pass would not create
a calibrated neuronal error representation. DNNE remains the action authority.

## Next diagnostic: cells before decoder tuning

`tools/PosturalCellProbe` characterizes the existing production LIF vestibular,
HH vermis and Izhikevich fastigial cell implementations. Each condition starts
fresh. Thirty-two unique cell indices per profile are observed for eight seconds:
two without imposed receptor input, four with input, two after removal. Six
conditions cover no input, 20/100 Hz glutamate, stronger glutamate, GABA and mixed
glutamate/GABA. Each repeats with the production circuit pacemaker on/off and at
1/0.25 ms integration steps: 72 conditions in total.

This deliberately isolates the cell response. Receptor quanta are imposed; it
does not create contacts, perform synaptic learning, copy brain weights, route
between nuclei or move Avatar. Turning off the circuit pacemaker retains the
cell's intrinsic tonic and homeostatic mechanisms. Identical physical event times
and a held 1 ms circuit clock prevent the finer step from increasing input rate
or speeding the imposed pacemaker waveform. Spike times, per-cell phase rates,
currents, soma/AIS voltage ranges and post-step AIS boundary occupancy are saved.
Production clamps remain active: finite saved output cannot exclude pre-clamp
numerical trouble. The existing HH-labelled model remains the documented hybrid,
not a newly validated Hodgkin–Huxley implementation.

`analyse-postural-cells.py` validates the complete matrix, receptor-event counts,
elapsed time, spike records and exact matching of pre-stimulus baselines. It
reports input response and time-step sensitivity without treating completion as
physiological qualification. The short preparation check covers all 72 conditions
with four cells and 200 ms per condition; it is only a harness check. The launcher
repeats that preflight before the full independent PowerShell run, checks protected
hashes and backs up the results to D:. No agent live monitoring is used.

Preparation validation: the diagnostic Release build succeeds with zero warnings
and errors; all 72 short conditions pass event/time and fresh-baseline checks. A
deliberately corrupted receptor-event count is rejected, as is reuse of an
existing output directory. Python compilation and PowerShell parsing pass.
Evidence is in `artifacts/postural-cell-preparation-20261019`. Production neural
code is unchanged, so these checks validate the new harness rather than claiming
another physiological result.

The next implementation decision depends on these results: repair cell response
or numerical defects if present, measure convergence where step sensitivity is
large, then assess the complete cerebellar input/teaching circuit and a calibrated
neuronal representation of disturbance. Only after those checks should a
paused-start world run assess physical feedback and recovery. No production cell
equations, motor thresholds, Avatar mechanics or editor behaviour were changed
merely to produce a passing trial.
