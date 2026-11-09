# Somatic neuron to muscle: completed isolated circuit

The operator reported `motor-unit-circuit-20261109-83e7ee` complete. All 88
focused tests and all eight circuit conditions passed. Actual somatic membrane
crossings, with their original identities and fractional times, reached the
existing Avatar transport/calcium/isometric-force path. This advances the earlier
fixture-only plant evidence to a neuronal-source interface result.

The read-only review is `artifacts/motor-unit-circuit-review-20261109/review.json`.
It verifies all 2,071 files (481,362,588 bytes per copy) on C: and D:, the frozen
revision `e12fff09af30f078e229ed2f56c71b4af93886ec`, inputs and executed binaries.
The capture manifest SHA256 is
`ABFC1F69D15EE010E2EDD8E6980C373CECEB53A896BB6D66CDD68FCB0CFCBCF0`.
Recomputing the saved-array analysis reproduces the complete report exactly.
No model or reference integration was performed during review. The separate
review also has a verified D: backup.

## Causal and transport findings

| Condition | Emitted | Delivered | Intentionally cut | Peak force (N) |
| --- | ---: | ---: | ---: | ---: |
| Packet intervals 1, 10 and 50 ms, each | 28 | 28 | 0 | 0.419612306595 |
| Retry every seventh 10 ms packet | 28 | 28 | 0 | 0.419612306595 |
| Half muscle timestep | 28 | 28 | 0 | 0.419612306632 |
| Quarter muscle timestep | 28 | 28 | 0 | 0.419612306635 |
| Quiet source | 0 | 0 | 0 | 0 |
| Disconnected active source | 28 | 0 | 28 | 0 |

All eight full neuronal traces reproduce their qualified baseline CSV bytes,
crossing times, integration-call counts and elapsed-time accounting. Changing
packet cadence, recipient resolution or the output connection does not change
the neuron. No events fall beyond the horizon in this particular train; all
28 active emissions are accounted for. This does not exercise a nonempty pending
tail in the circuit experiment.

The three packet cadences and retry condition have exactly equal complete muscle
sample arrays. All 85 deliberately withheld receipts are retried without duplicate
muscle activation. Attempts to advance the body before the next packet are blocked.
The disconnected neuron still fires 28 times, but its recipient has zero release,
activation and force. The quiet neuron also produces zero force. Connected force
begins only after the declared arrivals. This demonstrates dependence on this
isolated neuronal connection; it is not a live spinal-circuit result.

## Numerical checks

All three independent muscle solutions agree within the unchanged reference
bounds. Candidate maximum state error falls from `1.6128000e-8` to `9.7598624e-10`
to `5.9154945e-11` on successive muscle-step halvings. Finest force error is
`3.9014625e-13 N`, below the original `1e-6 N` bound. Maximum calcium-ledger error
is `6.6131861e-12`, below `1e-9`; release-waveform error remains below `2.754e-14`.
No thresholds, biological parameters or equations changed to pass these gates.

TRX coverage is 32 transport, 16 replay-buffer, 13 calcium, 15 isometric-plant,
11 motor-cell and one solution-manifest case. This was a focused suite, not a
new full DNNE unit-suite run.

## Scope and next integration work

The source is a five-compartment cat MG reference under current injection; its
recipient uses a human tibialis-anterior slow-unit parameter row at optimal fixed
length. The declared delay is 2.0625 ms. This mixed reference pairing is an
interface/numerical experiment, not calibrated human physiology. Somatic crossings
plus a fixed delay do not qualify active axonal propagation. Ideal endplate
transmission and an unlimited reference bath omit NMJ failures and supply limits.
Dynamic muscle/tendon mechanics, spinal synaptic recruitment and the sensory return
path still need qualification before body authority changes.

No live DNNE, Avatar, World, Unity or editor path was changed or newly qualified.
Balance, fall recovery and walking were not assessed. The existing neuronal-source
and sensory-boundary work should now build on this causal result, rather than
repeat the fixture-only force experiment. The next already prepared numerical
window is the finer Golgi comparison; CUDA speed measurements must wait until
competing studies finish.
