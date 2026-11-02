# Olive-to-Purkinje calcium comparison — 2 November 2026

The isolated run completed, and the new mechanism demonstrates calcium-dependent
changes at the connected receiving cell. It has **not** established useful,
physiologically calibrated Purkinje learning. The present dose-to-weight response
is very small, dormant contacts also change, and connected-host timestep
sensitivity remains material. The fastigial/vestibular stage should follow a
stronger Purkinje qualification rather than proceed on completion alone.

## Evidence and preservation

Run: `artifacts/olive-purkinje-20261102-923ebc`, source
`daadf7e5397a95914ec2dd4786e3db2eb6d06e79`, completed at 13:11:07 UTC with exit 0.
Its D: mirror is `D:\DNNE-desktop-runs\UnityWorld\olive-purkinje-20261102-923ebc`.
The separate read-only audit is `tools/review-olive-purkinje.py`, with outputs in
`artifacts/olive-purkinje-review-20261102`. No completed capture is rewritten.

The audit verified SHA-256 hashes for 3,292 captured files in each C:/D: copy,
1,483 packaged files and 18,356 protected older files. All five protected olive
implementation files match the previous response package. Recomputing the full
saved analysis reproduces its report exactly without altering the original.

The full matrix has 128 fresh conditions: four learning variants, eight input
plans, two onset offsets and two outer steps. Each condition contains 240 seconds
of biological time. There are 1,536,000 electrical/energy frames, 7,676,800 contact
frames and 4,305,708 recorded events. The protocol and approximation boundaries
remain in `DNNE_Purkinje_Calcium_Candidate_2026-11-02.md`.

Biological summaries below use **only the 0.125 ms cases** and average two deterministic
onset offsets. The 1 ms cases are numerical comparisons, not additional biological
subjects. There is no statistical population replication or equivalence test.
All percentages are relative to the initial contact weight 1.

## Calcium dependence is present, but qualification is incomplete

For the nominal 100 ms PF-leading condition:

| Variant | Active PF contact weakening | Dormant PF contact on same cell | Active contact peak calcium |
|---|---:|---:|---:|
| Calcium candidate | 0.013137% | 0.004698% | 0.98769 uM |
| Calcium entry blocked | 0% at stored weight precision | 0% | 0.05730 uM |
| Internal-store release blocked | 0.004698% | 0.004698% | 0.83593 uM |
| Olive disconnected, candidate intact | about 0.000006% | about 0.000006% | 0.11013 uM |

Thus voltage-dependent entry is necessary for the observed stored-weight effect
in this test, and internal-store release supplies the active contact's additional
response. The disconnected and unstimulated-olive controls match. The other
Purkinje cell's active contact stays close to that tiny baseline. The non-granule
glutamate and climbing contacts retain weight 1.

This is evidence for the implemented mechanism's dependencies and cell ownership.
It does not establish a correctly calibrated biological learning rule. The
dormant contact's effect is about 36% of the intended contact's weakening. The
model gives contacts on one cell a shared receiving-voltage signal; local IP3
adds selectivity but does not remove the common calcium contribution. Some
inactive-contact LTD can be biologically possible under sufficient calcium, so
the appropriate remedy is a spatial/dose calibration, not an arbitrary rule that
forbids all changes whenever a contact lacks a recent activity marker.
The calcium-uncaging evidence is discussed by
[Tanaka et al., 2007](https://doi.org/10.1016/j.neuron.2007.05.014); it does not
establish that this candidate's dormant-contact response has the correct size.

## Timing and learning magnitude

| Nominal PF stimulus timing | Legacy rule: active loss | Calcium rule: active loss | Calcium rule: dormant loss |
|---|---:|---:|---:|
| Leads 100 ms | 0% | 0.013137% | 0.004698% |
| Leads 20 ms | 8.971452% | 0.014507% | 0.004974% |
| Follows 100 ms | 2.320835% | 0.010315% | 0.004613% |
| Leads 500 ms | 0% | 0.003947% | 0.004064% |

These timings refer to fixture emission relative to olive stimulation. They are
not the actual PF-to-CF arrival intervals. Every olive burst event and conduction
delay is retained; later CF events can overlap a nominally reversed PF burst.
The review saves actual arrival-lag distributions and the legacy rule's individual
weight changes. Do not infer an optimal physiological learning window from the
nominal labels alone.

For example, the nominal 100 ms-leading condition has no CF arrivals within
50 ms of the most recent active PF arrival. The 20 ms-leading condition has
118/114 such arrivals at the two offsets; the nominally reversed condition still
has 36/31. These are event counts within burst-driven traces, not trial-level
success rates or proof of an experimentally matched timing protocol.

Both rules use the same new climbing conductance compartment. The comparison
isolates their learning behaviour; it is not a replay of the older Purkinje
membrane. Larger legacy weight changes are not an experimental biological target
and do not justify multiplying the candidate learning rate until it matches.

Four minutes leaves slow expression unfinished, but waiting alone cannot explain
the small effect. With the currently stored induction and no stronger subsequent
induction, the 100 ms-leading active contact approaches only 0.028860% total loss;
the 20 ms-leading condition approaches 0.031851%. Those are the model's inferred
asymptotes, not extra simulated observations or physiological limits.

The candidate retains the maximum historical filtered-calcium dose as a persistent
induction target. This explicit consolidation approximation can cause later
weaker episodes to have no additional teaching effect. In the 100 ms-leading,
inhibition and glucose conditions, active-contact induction and final loss are
identical despite different olive output totals. Review the stored trajectories
before treating this as appropriate memory or disturbance learning. In all three
plans, the final maximum induction is first sampled at 3.36 s and 6.40 s for the
two offsets, before the 10–30 s challenges. Those timestamps have 20 ms sampling
resolution. The induction target subsequently remains unchanged; the weights
continue relaxing toward it. This explains why later differences in olive output
do not affect final active-contact loss in these conditions.

## Circuit output, chemistry and numerical limits

For ordinary stimulated cases the finer runs emit 237 and 231 olive axonal events
at the two offsets; inhibition reduces these to 190/191 and glucose withdrawal
to 197/193. Connected delivery preserves every event. The disconnected control
records the same 237/231 outgoing events without delivery; the quiet olive emits 0.

Total Purkinje output is approximately 28,300 spikes across the two cells over 240 s.
For the 100 ms-leading condition, legacy and calcium variants have exactly the same
total counts at each offset: 28,311/28,322. This does not prove identical spike
timing, but it does not demonstrate a useful output correction from the tiny
weight changes either. The body, balance decoder and physical recovery were not
part of this experiment.

Maximum saved energy-balance error is 1.143e-11; maximum calcium-mass error is
2.057e-9. No unmet energy demand is recorded. These are accounting statements,
not evidence that normalized supply or chemical pump costs are physiologically
calibrated.

Outer-step sensitivity is not negligible:

- Olive axonal counts differ by up to 6 between 1 ms and 0.125 ms; this difference is
  already upstream of the learning-rule comparison.
- Purkinje total counts differ by up to 22.
- Candidate final weights differ by up to 0.004071 percentage points, appreciable
  relative to the small reported learning effects. Legacy weights differ by up
  to 0.333905 percentage points.

The earlier directly driven isolated olive test had matching outer-step traces.
That does not establish equivalence of this connected engine's event scheduling
and coupled voltage/chemistry updates. Arrival versus integration boundaries and
the held voltage supplied to chemical substeps need a focused convergence check.
This is a candidate explanation for the sensitivity, not a proven sole cause.
Specifically, `StructureEngine` processes arrivals through the substep's end
timestamp before advancing the membrane for the full substep. Chemical integration
then receives the resulting dendritic voltage held for its substeps. The next
test must distinguish these scheduling and coupling effects before attributing
all numerical differences to channel physiology.

## Next work within the agreed plan

The plan remains Purkinje learning → fastigial/vestibular integration → reliable
balance readout → Avatar recovery. This result keeps us at the first stage.

1. Establish connected-event and voltage/calcium convergence with identical
   biological input times, preserving all burst events and actual arrival logs.
2. Calibrate local calcium amplitude/duration, active-versus-dormant specificity
   and the calcium-to-LTD response against reference protocols. Separate the
   voltage driver from the biochemical response in these controls.
3. Assess repeated teaching and consolidation explicitly; replace the retained
   maximum approximation only with a justified mechanism. More elapsed time or a
   larger arbitrary multiplier is insufficient evidence of a repair.
4. Qualify useful receiving-cell effects before advancing to fastigial learning.
   Missing LTP, full active Purkinje dendrites, and the prior zero balance-readout
   issue remain separate outstanding work.

No new experiment or live service is launched by this review. Production biology,
Avatar and the editor remain unchanged while the completed evidence is assessed.
