# Golgi channel verification results

The operator reported completion of the **Golgi ion channels** window on
6 November 2026 and separately stated that one other test was still running.
Only this completed capture was inspected. The boundary-timing experiment remains
uninspected until the operator reports its completion.

Capture: `artifacts/golgi-channels-20261106-943d65`, source revision
`d683601ed9582745e91cda331b2f02555701b55c`. It finished at
`2026-11-06T08:57:09.0379728Z`, status FINISHED, exit 0.

## Evidence inspected

- Both frozen-source Release builds: zero warnings and errors.
- TRX and test log: all 16 focused tests passed, none skipped.
- Complete 36-condition capture with seven samples per case: 252 independent
  SK2 comparisons. The saved analysis passed its matrix completeness and state
  checks. Its implementation parses the pinned NMODL SK graph and uses SciPy's
  matrix exponential, separate from the candidate's uniformization algorithm.
- Maximum absolute SK2 occupancy difference: `7.72715225139109e-14`.
- Maximum SK2 occupancy-sum error: `3.6415315207705135e-14`.
- All 1,771 manifest-listed files were rehashed in both the C: capture and D:
  backup: 92,990,449 bytes per copy, plus matching backup manifests. The frozen
  source, binaries, logs, TRX, raw capture and analysis are retained.

The test itself never loaded old brain saves or accessed a live brain. This review
verified this capture/backup pair; it did not repeat a whole-drive legacy-save
audit or run the full DNNE test suite. The earlier cortical-consolidation failure
is still unresolved.

## What this permits next

The implemented channel calculations pass their current mechanistic tests. SK2's
clamped state propagation agrees with an independently evaluated reference graph
to much better than the predeclared 2e-11 tolerance.

That permits work on a **candidate** coupled Golgi membrane, passive cable loading
and calcium pools. It does not qualify the other channels against full NEURON
traces, free-cell pacemaking, synaptic inhibition, learning or Avatar recovery.
The input domain and 37 C sweep are numerical checks, not human calibration.

The next protocol must separate passive cable/charge accounting, ionic numerical
refinement, endogenous firing, evoked response, inhibition/recovery and relevant
channel ablations. Keep it isolated until actual mossy/granule input and Golgi GABA
output contacts are assessed. Do not replace a current biological mechanism based
only on this channel-level success.
