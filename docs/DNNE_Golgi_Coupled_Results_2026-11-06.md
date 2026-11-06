# Golgi coupled-cell result and reference refinement

The operator reported completion of `golgi-cell-20261106-ee37aa` (source
`ba67f99bea6162b6bd0a77aa235182e91cc6a5c9`). It finished on 6 November at
10:30:49 UTC with exit 1, because the reference's own accuracy comparison failed.
All 24 candidate cases and all 12 reference solves completed before that gate.
The failure must remain visible; it is not evidence that the cell failed to fire.

## What passed

- All 25 focused unit cases, including the solution manifest.
- All 4,536 ordinary-gate clamp comparisons; maximum absolute error
  `2.220446049250313e-16`. The SK2 matrix-exponential checks also completed.
- Cable geometry matched the pinned deposit. Independent discrete passive solves
  differed by at most `2.55333532095392e-9 mV`. The two continuous passive reference
  settings differed by at most `6.652001616203052e-8 mV`, below their existing limit.
- All candidate traces remained finite and passed the declared charge, calcium,
  occupancy and nodal accounting checks. These are not spike-accuracy tests.

Across six seconds, the three candidate resolutions and both active reference
settings had the same counts in every protocol:

| Protocol | Somatic crossings | Candidate late recovery rate |
| --- | ---: | ---: |
| Quiet | 37 | 6 Hz |
| +50 pA from 2–3 s | 43 | 6 Hz |
| −50 pA from 2–3 s | 32 | 6 Hz |
| Persistent sodium removed | 0 | 0 Hz |
| HCN removed | 27 | 4.5 Hz |
| SK removed | 128 | 21 Hz |

The candidate produced no crossings during the −50 pA interval, then resumed.
Its quiet firings arose from the coupled mechanisms, without an imposed spike
sequence or tonic current. These findings support further cell development;
they do not establish human physiology, active axonal propagation, granular-layer
circuit operation, learned balance or Avatar recovery.

## Why the run failed

The existing reference gate requires matching counts, maximum ordinal timing
difference below 0.01 ms and sampled soma voltage difference below 0.05 mV.
The two BDF accuracy settings agreed on counts and passed the timing gate in all
six protocols, but four failed the voltage gate:

| Protocol | Reference timing difference (ms) | Reference soma difference (mV) |
| --- | ---: | ---: |
| Quiet | 0.000453298 | 0.129724 |
| Depolarize | 0.000552786 | 0.168533 |
| Hyperpolarize | 0.000197146 | 0.04799995 |
| No persistent sodium | 0 | 0.0000000370175 |
| No HCN | 0.000460223 | 0.144693 |
| No SK | 0.000638681 | 0.192363 |

Recalculation from saved NPZ state arrays reproduces those maxima exactly. The
largest discrepancies in firing conditions occur roughly 0.09–0.13 ms after a
strict-reference crossing, during the action potential. Small phase differences
are consistent with these sharp-waveform differences; that does not prove every
error is phase alone. Full gate, occupancy, cable and calcium differences are
retained in `artifacts/golgi-cell-review-20261106/review.json`.

The candidate has a larger, separate numerical issue. At its finest 0.00390625 ms
step, maximum ordinal differences from the existing strict reference are about
1.567 ms (quiet), 1.917 ms (depolarize), 0.796 ms (hyperpolarize), 1.928 ms (no HCN)
and 0.223 ms (no SK). The no-SK timing discrepancy does not improve monotonically
across all three steps. Matching counts and short subthreshold traces therefore
cannot qualify the first-order coupled solver. Its large unaligned full-waveform
errors are retained, not hidden by aligning spikes or averaging rates.

## Next controlled comparison

No biological parameter, initial condition, current challenge, C# solver or
acceptance limit is changed. The next package reuses verified copies of all old
candidate traces and the existing strict BDF reference, then solves all six
protocols with:

1. BDF at rtol `1e-12`, voltage/gate/calcium absolute tolerances
   `1e-13 mV`, `1e-15`, `1e-14 micromolar`.
2. Radau at rtol `1e-10`, with `1e-11 mV`, `1e-13`, `1e-12 micromolar`.

Both use a 0.125 ms maximum step; the previous reference used 0.25 ms. The old
strict BDF versus tighter BDF pair and the tighter BDF versus Radau pair must
both satisfy the **unchanged** original criteria in every protocol. Full-state
differences, all sampled states and events, solver evaluation counts and partial
results are retained even if the final gate fails. This is a comparison between
numerical methods for the same declared equations, not an independent biological
model. See the official [SciPy solve_ivp methods and tolerance documentation](https://docs.scipy.org/doc/scipy-1.16.2/reference/generated/scipy.integrate.solve_ivp.html).
The installed version is recorded by the new run rather than inferred from that
documentation version.

Only after reviewing that reference can we judge the necessary C# coupled-solver
repair. Making the threshold easier, increasing imposed drive or retuning channel
conductances would not resolve the observed numerical uncertainty.

## Motor transport result and preservation

The operator also reported return to the prompt for
`motor-axon-20261106-075652` (source `b94ae3f`). Its PowerShell transcript records
“The pipeline has been stopped” during the build; no test log or TRX exists.
The launch task was unregistered and no matching dotnet child remained when
reviewed. The interruption's cause is not established. This is **not** a failed
motor transport test or a passing run: tests never started.

PowerShell cancellation bypassed the catch block but ran finally, leaving an
incorrect RUNNING status with a completion time. The updated launcher marks that
case INTERRUPTED. It never infers success from reaching the footer. The original
record is retained unchanged; the reviewed interpretation is separate.

The follow-up uses one visible PowerShell window: retry the 32 motor transport
cases plus manifest check, then the Golgi reference refinement. Each completed
stage is recorded independently, so a later reference failure does not erase
the transport result. No live monitoring or live brain/muscle access is used.

Read-only review verified all **1,956 Golgi files** and **1,424 interrupted motor
files** against each capture's manifest on both C: and D:, plus frozen package
hashes and matching backup manifests. The separate review is also backed up and
verified on D:. Old captures and saves are unchanged. The replacement cell-local
timing run still awaits a distinct operator completion report; the pasted
`a554e9` failure was the already-preserved original capture.
