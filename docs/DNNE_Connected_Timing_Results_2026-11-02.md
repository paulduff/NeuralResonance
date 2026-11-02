# Connected timing results — 2 November 2026

The causal scheduling repair passes the complete packet-cadence comparison.
Successive solver refinements do **not** yet establish stable connected source
activity. This is a successful scheduling check with an unresolved numerical
qualification, not a qualified learning circuit or a physical balance result.

## Capture and audit

Run: `artifacts/connected-timing-20261102-26272f`, revision
`6e4c8c93b6051c381aea5fb028130a2f25b7b4ea`. It finished at
15:27:49 UTC with exit 0. D: mirror:
`D:\DNNE-desktop-runs\UnityWorld\connected-timing-20261102-26272f`.

The run contains 120 fresh conditions, each 60 biological seconds, with
360,000 electrical/energy frames, 1,797,000 contact frames and 1,661,044 recorded
events. The protocol is in `DNNE_Connected_Timing_Repair_2026-11-02.md`.
The independent read-only review is `tools/review-connected-timing.py`, with
outputs in `artifacts/connected-timing-review-20261102`. Completed captures are
not rewritten. The two oscillator offsets are deterministic conditions, not
independent biological subjects.

The audit verified SHA-256 hashes for 2,238 captured files in each C:/D: copy,
542 packaged files and 24,958 protected older files. All six protected physiology
source files match the previous package. Recomputing the entire saved analysis
reproduces its report exactly without modifying the original evidence. Maximum
energy-balance error is 2.637e-12; maximum calcium-mass error is 8.596e-12, with no
unmet demand recorded. These are accounting checks, not physiological calibration
of normalized supply or pump costs.

## What the scheduling repair establishes

All 60 matched host-packet comparisons pass: at a fixed solver ceiling, host
periods of 1 ms and 0.125 ms produce identical electrical and chemical trace
bytes, identical olive spike times and identical Purkinje spike times. Every
scheduled release is processed at its delivery boundary, including the
fractional fixture times. All actual olive axonal events are accounted for and
transported individually in connected conditions. The disconnected control
retains outgoing events without delivering them.

The calcium-entry ablation does not change the unidirectional olive spike train.
The PF timing variants and disconnected condition also retain the same source
train at a fixed resolution and onset offset. Consequently, the source
differences below arise before Purkinje learning or downstream transport could
explain them. Matching packet periods use the same numerical grid; their exact
agreement is not a proof of solver convergence.

## Refining the solver still changes olive bursts

Actual olive axonal counts over 60 seconds, using only the 0.125 ms host cadence:

| Input condition | Onset offset | Solver ceiling 0.125 ms | 0.0625 ms | 0.03125 ms |
|---|---:|---:|---:|---:|
| Paired, PF leads 100 ms | 0 ms | 238 | 237 | 258 |
| Paired, PF leads 100 ms | 60 ms | 223 | 234 | 220 |
| Inhibition | 0 ms | 189 | 182 | 189 |
| Inhibition | 60 ms | 189 | 190 | 185 |
| Glucose withdrawal | 0 ms | 196 | 196 | 196 |
| Glucose withdrawal | 60 ms | 197 | 193 | 194 |

The 20 ms-leading and disconnected plans have the same source counts as the
100 ms-leading plan. Differences do not consistently shrink with refinement.
Equal total counts in the glucose condition do not establish matching event
times or matching burst responses. The review retains counts in intervals
between actual olive stimulus arrivals, rather than interpreting an ordinal
spike comparison as a valid latency measurement when burst membership changes.
For example, both finer glucose runs at zero offset emit 196 spikes, but their
spike counts differ in 23 of the 58 stimulus-response intervals. The zero-offset
paired condition differs in 36 of those intervals between the two finer levels.

Before the first stimulation, the maximum sampled source-voltage differences
are below 0.000001 mV. The initial spike trains are also close. Differences grow
under repeated stimulation: for the zero-offset paired condition, the first
sampled difference above 1 mV occurs at 9.16 s for the 0.125/0.0625 ms pair and
8.00 s for the 0.0625/0.03125 ms pair. Samples are 20 ms apart; these are sampled
attainment times, not exact first-divergence timestamps.

This does not by itself prove a wrong biological equation or a chaotic regime.
Numerical coupling error and sensitivity of the driven nonlinear trajectory
have not been separated. Counting all emitted spikes rules out silent event loss
as the explanation for the observed count differences.

## Similar active-contact weights conceal later divergence

At the finest tested solver ceiling, the 100 ms-leading calcium condition has
active-contact losses of 0.004840% and 0.003076% at the two offsets after 60 s.
Dormant contacts on the same cell lose 0.001419% and 0.001293%. Entry-blocked
contacts have no loss at saved float precision; this is not a claim of exactly
zero biochemical induction. These are descriptive model effects, not measured
biological LTD or qualified contact selectivity.

The active contact's final induction maximum is first sampled at 3.36 s and
6.40 s for the two offsets, before the larger source divergence and before the
10–30 s challenges. The peak-retaining induction approximation then preserves
that early target. The paired, inhibition and glucose plans have identical
active induction and final active weight at a given offset and resolution,
despite their different later source activity.

Thus the active induction's relatively small last-refinement differences
(about 0.76% and 0.14% in the 100 ms-leading condition) are insufficient evidence
for converged teaching. Dormant induction differs more in some conditions.
No learning rate, calcium density, dose curve or expression timescale was fitted
in this run. Its 60 s final losses should not be compared directly with the
previous 240 s observation as if exposure duration and timing were unchanged.

## Source-code interpretation and next work

The olive membrane already uses coupled RK4 with state-dependent step limits.
Replacing Euler with RK4 is therefore not the missing repair. Its current step
limits respond to conductance and voltage slope; they do not estimate local
truncation error. Synaptic conductance and energy are coupled in separate
substeps. With the current `ceil(dt / 0.025)` partition, regular outer intervals
of 0.125, 0.0625 and 0.03125 ms create synaptic substeps of 0.025, approximately
0.020833 and 0.015625 ms respectively. Event boundaries can split them further.
This comparison changes several numerical details together. It does not identify
one as the sole cause.

The existing inner-refinement regression covers three seconds of a different
20 Hz stimulus protocol, without the energy ledger. Passing that regression
does not establish convergence of this 60 s driven circuit.

Next separate the two sides of the circuit:

1. Reproduce the exact olive stimulus schedule in an isolated source test. Vary
   membrane accuracy and receptor/energy coupling resolution independently, and
   compare with an independently implemented error-controlled reference using
   the same biological equations and initial conditions. Retain voltage, gates,
   currents, spike times and burst membership around the first divergence. Test
   whether the outcome reflects integration error or sensitivity to small input/
   state perturbations; do not tune channels merely to force matching counts.
2. Replay one immutable recorded olive axonal train into fresh Purkinje cells,
   preserving every timestamp and conduction delay. Refine receiving voltage and
   calcium integration while holding the source train fixed. This diagnostic
   replay is not live brain control or proof of a calibrated olive.
3. Once those numerical questions are resolved, return to local calcium-dose
   calibration and repeated teaching/consolidation. Then follow the agreed
   fastigial/vestibular, balance-readout and Avatar-recovery sequence.

This review makes no production changes and starts no experiment or live service.
