# Olivary evoked-response recovery — reviewed 2 November 2026

The olive recovers **outgoing spike responses**, not just its resting voltage
waveform, after the tested inhibitory and supply challenges. Late responses are
close to the matched sham in both schedules, including cells that received no
probes during the challenge. This supports proceeding to a small isolated
olivary–Purkinje/contact-learning test. It does not qualify whole-brain operation,
human physiological equivalence or Avatar balance.

Run: `artifacts/olive-response-20261102-f961e3`, revision
`4696ea54dbce29afa361377c0e145c2d9b541106`. It finished at
2026-11-02 10:02:43 UTC with exit 0. Inspection began after the operator reported
completion. The read-only review is reproducible with
`tools/review-olive-response.py`; results are saved separately in
`artifacts/olive-response-review-20261102/review.json` and
`response-summary.tsv`.

## Execution, numerical consistency and preservation

All **160 conditions** completed. Replaying the validated analyser in read-only
mode checked **4,800,000 trace rows**, all input schedules, supply transitions,
pre-probe states, response windows, spike attribution and energy accounting. The
recomputed report exactly matches the captured report. The new read-only option
was checked against a copied fixture: it changed no input files and created no
report. Existing rejection checks for corrupted evidence still pass.

All **80 coarse/fine trace comparisons are byte-identical**. Every response-window
and outside-window spike count agrees between 1 and 0.125 ms host steps. Recorded
latencies differ by at most 0.875 ms, consistent with rounding a crossing up to
the host-step endpoint. The maximum reported energy-ledger error is **4.51e-13**
normalized units, with zero unmet demand throughout the matrix.

The audit verified 1,703 captured files at both C: and D:, the matching manifests,
407 frozen package files and **14,934 protected older files**. All 58 shared
runtime/protocol/contract files still match the preceding calibration package.
The review is backed up separately to D:. The completed run and old saves are
unchanged; no new experiment or live service is launched by this review.

## Responses during the challenge and after restoration

The following counts use only the 0.125 ms results, pooling the eight deterministic
onset offsets. The 1 ms cases are numerical repeats, not extra biological samples.
An outgoing response means at least one axonal spike in the prespecified 250 ms
window. The late block comprises the last ten probes for each offset, approximately
50–59 s, after restoration at 30 s. Each late column therefore contains 80 probes.

| Challenge | Responses during challenge, probes throughout | Axonal spikes per challenge probe | Late responses, probes throughout | Late responses, no probes during challenge |
|---|---:|---:|---:|---:|
| Sham | 153/160 | 4.075 | 76/80 | 76/80 |
| One inhibitory contact | 138/160 | 3.156 | 80/80 | 78/80 |
| Four inhibitory contacts | 156/160 | 2.013 | 76/80 | 77/80 |
| Glucose withdrawal | 99/160 | 2.194 | 79/80 | 77/80 |
| Oxygen withdrawal | 139/160 | 3.425 | 77/80 | 78/80 |

Late outgoing response fractions are **95–100%**, compared with **95% in the
corresponding sham schedules**. The matched pre-challenge result is 61/64 outgoing
responses in every intervention and schedule. Occasional misses also occur in
the healthy control, so absence of a response on every single probe is not by
itself evidence of failed recovery.

Somatic results tell a similar, separate story: late somatic response fractions
range from 69/80 to 73/80, compared with 72/80 in each sham schedule. Some windows
with axonal events contain no somatic upward 0 mV crossing. Keep the two measurements
distinct; an axonal hit must not be relabelled as a somatic spike.

Late mean axonal output ranges from 3.788 to 4.213 spikes per probe, against
matched controls of 3.950 (probes throughout) and 4.113 (quiet challenge). The
largest relative reduction is approximately 7.9%, for the stronger inhibitory
condition after a quiet challenge. These are descriptive comparisons, not a
prespecified statistical-equivalence test or independent biological replication.
The experiment establishes restored responsiveness in these isolated conditions;
it does not establish an exact return to every pre-challenge trajectory.

## Inhibition changes output structure

Stronger inhibition cuts total axonal output during the challenge from **652 to
322 spikes**, approximately a **51% reduction**, across the same 160 probes.
Somatic output falls from 238 to 156 spikes. However, the fraction of probes with
at least one outgoing spike increases slightly from 153/160 to 156/160. One
inhibitory contact reduces both hit fraction and total output.

Thus inhibition is not a reliable binary veto at these contact strengths. Its
stronger effect here is to reduce burst output and alter response timing. The
current data do not isolate the channel-level cause of this nonmonotonic hit
fraction. Do not increase inhibition merely to force a preferred yes/no result.
The next circuit test must examine how the **actual complete spike trains** affect
Purkinje activity and contact plasticity.

## Fuel and recovery

Glucose withdrawal has the largest response reduction with continuing probes:
99/160 outgoing responses during withdrawal, then 68/80 in the first recovery
block, and 79/80 in the late block. The corresponding sham has 153/160, 76/80 and
76/80. With no probes during withdrawal, glucose cases produce 79/80 responses in
the early recovery block and 77/80 late, versus sham 74/80 and 76/80. Recovery
therefore does not require continued probe activity during the interruption.
Post-restoration probes remain experimental measurements, not a live restart policy.

Minimum normalized reserve is **0.649** for glucose and **0.827** for oxygen when
probes continue, versus **0.771** and **0.933** with a quiet challenge. Final
reserves across all cases are **0.999956–1**. These are the model's reduced energy
variables, not measured ATP, injury tolerance or a claim about real tissue survival.

## Delayed spikes and scoring-window sensitivity

Across the 80 finer-step conditions, 24 somatic and 62 axonal spikes occur outside
the original 250 ms response windows. They occur in the stronger-inhibition and
fuel cases. All are **250.125–491.875 ms after a preceding probe**. The last is at
31.285375 s, about 1.29 s after restoration; none persists into the late recovery
period. Some are axonal tails separated from an earlier somatic event by the
window boundary. Their timing alone does not prove that every event was caused
by the preceding probe rather than the intervention or intrinsic state.

A clearly labelled **post-hoc 500 ms sensitivity analysis** uses complete,
nonoverlapping windows and retains the original 250 ms results as primary.
It leaves the number of inputs eliciting an outgoing response unchanged. During
withdrawal it changes counted axonal output from 351 to 357 for glucose and
548 to 571 for oxygen; sham remains 652 and strong inhibition remains 322.
The central recovery and burst-suppression conclusions are therefore not an
artifact of counting a late burst as an entirely missing response. Delayed spikes
must still be preserved when testing real climbing-fibre teaching effects.

## Next acceptance step

Keep the reference cell parameters fixed. Connect the actual olive output to a
small isolated Purkinje/parallel-fibre circuit with fresh state and matched
controls. Record delivered event identity and biological timing, target responses,
and individual contact changes. Test whether correctly timed climbing input
changes the eligible contacts, and whether quiet, mismatched or absent climbing
input avoids inappropriate changes. Include the brief delayed bursts observed
here, without capping or merging their axonal events into a single teaching pulse.

That step can test the interaction of the repaired cells and existing learning
mechanisms. Electrical gap-junction coupling, population anatomy, full DNNE
deployment and physical Avatar recovery remain separate work.
