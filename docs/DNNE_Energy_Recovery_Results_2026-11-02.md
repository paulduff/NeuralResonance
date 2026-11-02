# Energy and recovery result — reviewed 2 November 2026

The candidate sustains its energy reserve and recovers firing in the tested
isolated-cell conditions. The controls do **not** establish that the added channels
are responsible for recovery. The olive remains physiologically miscalibrated;
these results do not qualify the connected cerebellum or physical balance.

Run: `artifacts/energy-recovery-20261030-661900`, revision
`362e097276e271b1a0a3df188da5b5b5b6234dc1`. It finished with exit 0 on
30 October at 18:18:14 UTC. Inspection waited until the operator reported completion.
The reproducible read-only audit is `tools/review-energy-recovery.py`; its output
is `artifacts/energy-recovery-review-20261102/review.json`.

## What the full run establishes

All 120 conditions completed: three profiles, five mechanism controls, two
integration steps (.25/.125 ms), and four receptor/supply conditions. Each used
32 fresh cells for 60 simulated seconds. Every candidate cell was active in the
final ten-second observation period in every condition, including all recovery
conditions. This is an isolated-cell result, not learned behavior.

At .25 ms, final quiet and post-inhibition rates and individual first-return
spike latencies were:

| Candidate population | Quiet rate Hz | Post-inhibition rate Hz | Return latency range, seconds |
|---|---:|---:|---:|
| Vermis reduction | 60.500 | 60.463 | .721–.797 |
| Purkinje layer | 58.200 | 58.200 | 1.061–1.210 |
| Inferior olive | 72.809 | 72.838 | .478–.550 |

Inhibition suppressed activity during seconds 30–40: zero vermis/olive spikes at
.25 ms and .03125 Hz mean Purkinje activity. Return occurs after inhibition ends,
without a pulse or a forced spike. The longer observation resolves the short
pilot's partial Purkinje recovery: all 32 cells eventually resume, at both steps.
The final rates match the corresponding quiet condition closely.

Adequate supply sustains a final normalized reserve of approximately .997–.999.
Candidate unmet energy demand is zero. Glucose removal during seconds 10–30
suppresses all three populations to zero in the second withdrawal phase. First
spikes after restoring glucose arrive within 1.495–2.225 seconds across the sampled
candidate cells; all resume sustained activity. Reserve remains positive during
this protocol, reaching a minimum of about .577, because suppression reduces
demand. This run does not demonstrate survival after exhausting intracellular ATP.

Oxygen withdrawal leaves low residual firing (roughly .86–1.88 Hz in the second
withdrawal phase), consistent with the model retaining limited glycolytic supply.
First spikes after oxygen restoration must not all be described as restarts:
some cells never became completely silent. By the final phase all have resumed
their supported firing range. Units remain normalized, not measured ATP or a
validated ischemia timescale.

The largest phase-rate difference between .25 and .125 ms across all controls
is .08125 Hz. Maximum energy balance error is 2.10e-11 reserve units. The audit
verified 72,000 trace rows containing 2,304,000 sampled cell states, including
gate bounds, phase/event counts, energy conservation and phase-end agreement.
Trace voltages are serialized as float32 while extrema promote those values
to double; the audit restores the exact float32 value before comparing them.

## What the controls change in our interpretation

At the new drive conversion of 12, removing HCN, removing the slow inward current,
or removing both still permits all three populations to recover. Therefore the
new channels are **not necessary for recovery under this tested operating regime**.
Their physiological relevance does not establish their causal necessity here.

Keeping both channels but returning the input-to-current conversion to 10 prevents
Purkinje recovery after inhibition and after either fuel withdrawal, at both
integration steps: no final-phase spikes in any of the 32 cells. Vermis and olive
recover in that control. This directly identifies an operating-regime dependence
for the tested Purkinje model. It does not establish that HCN is irrelevant in real
Purkinje cells or that increasing tonic current is a complete biological repair.

The original v2 capture is protected for comparison, but it has a different
duration and protocol. The present controls all use the new energy mechanism;
they isolate channel/current effects, not a matched 60-second old-energy trial.
The preceding reserve/fatigue intervention remains the causal evidence for the
old energy-feedback failure. The current observations show that failure is absent
under adequate supply in this longer candidate test.

## Biological limitation and the next direction

The model olive fires about 73 Hz even in the quiet condition. For comparison,
an in-vivo mouse whole-cell study reported an average IO firing rate of
.87 ± .22 Hz. A separate mouse slice study reported .1 ± .1 Hz among its
spontaneously firing sample. These are preparation-specific measurements, not a
universal human target, but neither supports treating the present fast tonic
olive as a calibrated biological population.
[Khosrovani et al., 2007](https://pmc.ncbi.nlm.nih.gov/articles/PMC2000380/),
[Loyola et al., 2023](https://pmc.ncbi.nlm.nih.gov/articles/PMC10393294/).

This matters for learning, not just appearance. Source inspection confirms that
arriving olivary spikes invoke `ClimbingInput` on eligible, recently active granule
contacts of the receiving cell. Excessive quiet olivary firing could therefore
supply repeated LTD events without a disturbance. That is a mechanistic concern,
not a causal result of this disconnected-cell test. It must be checked against
the earlier connected circuit's weak quiet/disturbance distinction.

The next implementation should retain the verified supply/accounting work and
calibrate a genuinely olivary membrane/compartment model against an identified
experimental preparation. Match voltage, input/current units, temperature,
subthreshold oscillations, low-rate spiking and timed excitatory/inhibitory
responses together. Treat the current sinusoidal normalized olive drive as an
uncalibrated input boundary. Merely reducing a rate display, counting fewer spikes,
forcing silence, or adding more tonic drive would conceal the mismatch.

After that cell calibration, repeat the isolated connected quiet-versus-disturbance
comparison and quantify actual climbing-fibre arrivals and contact-specific LTD.
The decoder's separate rate-subtraction problem also remains open. Only then use
physical balance/recovery as an acceptance test. This review launches no new long
experiment and makes no production neuronal, Avatar or editor change.

## Preservation and console logging

Verified 1,050 capture files in both the local package and its D: mirror, all 799
frozen package files, and all 7,316 previously protected files. No mismatch occurred.
The completed package and older saves were not edited. This review is backed up
separately to `D:/DNNE-desktop-runs/UnityWorld/energy-recovery-review-20261102`.

The PowerShell transcript contains some preflight output and stage headings but
omits the full study's native console output. Its rounded preflight rates must
not be mistaken for the full study. Structured results and 60,000-ms traces agree.
For future packages, the launcher now streams stdout/stderr into a dedicated
UTF-8 log for each probe/analysis stage while displaying it in PowerShell. Tests
under Windows PowerShell 5.1 verify stdout/stderr, exit codes 0 and 23, paths with
spaces, restored caller error handling, and rejection of a missing executable.
No live monitoring is introduced. The unchanged neuronal code retains the prior
1,023-test regression result; it was not rerun for this analysis/logging-only change.
