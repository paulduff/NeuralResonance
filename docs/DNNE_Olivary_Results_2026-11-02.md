# Inferior olive result — reviewed 2 November 2026

The candidate has corrected the former rapid tonic olive: it maintains slow
intrinsic voltage oscillations, fires in response to sufficient synaptic input,
and returns to its baseline oscillations after the tested perturbations. The
calcium-channel controls give causal evidence within this model. This is useful
progress in isolated-cell calibration, **not qualification of the connected
olive, cerebellar learning or physical recovery**.

Run `artifacts/olive-calibration-20261102-8edb50`, source revision
`a08b8c12dbe86ad79ee38acfd834092ed74a571f`, completed at
2026-11-02 08:39:23 UTC with exit 0. Inspection began after the operator reported
completion. `tools/review-olive-calibration.py` independently audits the saved
evidence without modifying it. The resulting record is
`artifacts/olive-calibration-review-20261102/review.json`.

## Numerical and preservation checks

All **180 conditions** completed, each a fresh deterministic cell for 60 simulated
seconds. All **5,400,000 trace rows** passed state, energy, phase and event-count
checks. The maximum energy-ledger discrepancy was **1.44e-10** normalized units.

All 120 comparisons of a coarser-step trace against its corresponding 0.125 ms
trace were **byte-identical**. Total somatic and axonal spike counts also agreed
across 1, 0.25 and 0.125 ms outer steps. This confirms the internal coupled update
removes host-step sensitivity in these conditions; it is not independent evidence
of biological accuracy. Inner-step refinement was tested before launch.

The audit verified all 2,741 captured files against their completion hashes on
both C: and D:, checked the matching manifests, all 1,647 frozen package files,
and all **9,440 protected older files**. Old saves remain unchanged. The review
is saved separately and backed up to D:.

## Candidate behavior

Quiet cells produced **zero somatic and axonal spikes in 60 seconds**, while
maintaining approximately **8.20 Hz** subthreshold oscillations. Their final
voltage range was approximately **-64.70 to -42.80 mV**. This matches the intended
reference oscillator regime; it does not reproduce an awake olive's population
firing distribution. The setup has no electrical coupling or background network.

During the ten-second, 20 Hz excitatory-input period:

| Simultaneously stimulated contacts | Somatic spikes | Axonal spikes |
|---|---:|---:|
| 1 | 0 | 0 |
| 4 | 100 | 299 |
| 16 | 197 | 609 |

Thus the cell is responsive, rather than merely silent. These deliberately strong
test inputs are not desired resting firing rates. The sixteen-contact case also
produced one delayed somatic spike and two axonal spikes about 111 ms after the
input period ended, then settled into its nonspiking oscillations. The four-contact
case produced no post-input spikes. Somatic spikes and axonal bursts must remain
distinct when assessing the eventual climbing-fibre teaching input.

Every candidate protocol returned to the baseline oscillation frequency and
approximately 21.9 mV peak-to-trough amplitude in the final ten seconds. Following
inhibition or supply restoration, the first completed sequence of five stable
cycles occurred as follows:

| Challenge | Minimum energy reserve | Five stable cycles after release/restoration |
|---|---:|---:|
| Inhibitory input | 0.9996 | 0.660 s |
| Glucose withdrawal | 0.7780 | 1.424 s |
| Oxygen withdrawal | 0.9380 | 0.652 s |

The stability criterion requires each cycle's amplitude within 10% and period
within 5% of the 2–10 s baseline. This is an analysis criterion, not a measured
physiological recovery target. Oscillations persisted during these challenges;
these times describe a return to the baseline waveform, not restarting a silent
cell. The glucose condition slowed to approximately 6.85 Hz in its latter
withdrawal phase. The five-cycle check itself takes about 0.61 seconds; the oxygen
waveform was already close to baseline during withdrawal, so its 0.652 s value
must not be interpreted as a restart delay. Candidate unmet demand remained zero, with final reserves
approximately 0.99995–1. These remain normalized energy proxies, not ATP measurements
or evidence that a real neuron tolerates the same interruption.

The weakest excitatory input and inhibitory input changed the voltage waveform
without producing spikes. Peak counts during stimulation can include driven
deflections and must not be labelled intrinsic oscillator frequency.

## Channel controls

Removing low-threshold calcium (CaT) abolished sustained quiet oscillations. The
moderate four-contact input then produced just **1 somatic / 2 axonal spikes**,
compared with the candidate's 100 / 299. Strong sixteen-contact input still evoked
195 / 603. This supports a specific role for CaT in oscillation and moderate-input
responsiveness within the implemented model; it does not show that CaT is required
for every action potential.

Reducing CaT conductance from 1.1 to 0.7 retained subthreshold oscillations at
approximately 7.69 Hz. Raising it to 1.5 produced approximately **8.2 Hz continuous
somatic firing** and 16.4 Hz axonal firing at rest in the settled phases. Increasing
this conductance simply to obtain more output would lose the chosen sparse-rest
regime. Keep the reference setting of 1.1 for the next comparison.

Removing HCN did **not** abolish quiet oscillations or evoked responses. Its quiet
frequency was approximately 8.06 Hz, and the weak paired protocols could now evoke
spikes. HCN influences the operating regime, but these results do not establish
that it is necessary for oscillation or recovery. The membrane-only reference and
production candidate had matching spike totals in every protocol; their healthy
supply settings did not change those totals.

## What remains unresolved

1. **Recovered responsiveness was not tested.** No fresh strong input was sent
   after inhibition or supply restoration. A return of voltage oscillations and
   reserve is encouraging, but cannot establish that an evoked response has
   recovered. Inhibition was also tested without concurrent suprathreshold
   excitation, so this run does not measure suppression of an ongoing spike response.
2. **Paired timing is inconclusive.** All three weak paired protocols produced zero
   candidate spikes. Additionally, the 40 s cutoff leaves 31 inhibitory releases
   in every pair case, but 31 excitatory releases at 50 ms delay and only 30 at
   100/150 ms. This endpoint difference must be corrected before a matched timing
   comparison. It does not affect the validated accounting, which captured those
   actual counts.
3. **Network biology remains open.** There is no olivary gap-junction network,
   population heterogeneity or connected Purkinje/LTD measurement in this run.
   Conductance per simulated contact, anatomical inhibitory placement and energy
   cost remain explicit approximations. No Avatar movement or recovery was tested.

The next focused test should repeat the **same sparse four-contact input before,
during and after** sham treatment, inhibition, glucose withdrawal and oxygen
withdrawal. Match the stimulus schedule and initial conditions across those cases,
record somatic response probability, axonal burst size, inhibition sensitivity and
post-restoration response, and preserve the current reference parameters. Use
complete, equal-count inhibitory/excitatory pairs if testing timing. Once these
responses pass, reconnect a small isolated olivary–cerebellar circuit and check
actual teaching/contact updates before returning to the whole brain and Avatar.

This review changes no production dynamics and launches no further experiment.
