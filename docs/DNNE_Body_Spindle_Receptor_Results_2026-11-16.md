# Reported body, spindle and receptor results — 16 November 2026

The operator reported all three windows complete. Results below distinguish
verified saved evidence from scope that has not been qualified. No model or
test is rerun during review; completed captures remain immutable.

## Motor receptor component `501da5`

All 74 focused cases passed: 14 receptor-contact cases and 60 membrane,
transport, replay and solution regressions. Exact method/case coverage, frozen
source, binaries and every C:/D: capture file verify: 2,063 files per copy,
95,532,707 bytes, and 1,657 Git source files.

`artifacts/motor-receptor-review-20261116/review.json` records the review and has
a verified D: copy. This is the original contact component, not a passed coupled
receptor/membrane or active motor-output comparison. Its success clears the
prerequisite for the separately prepared 111-case/five-source gate.

## Fixed-length spindle `18ec5e`

The saved analysis reproduces exactly. Six analytic controls, the saved Jacobian
comparison, 18 reference integrations, 18 reference contrasts and 54 native/
reference comparisons pass their original bounds. Full C:/D:/source integrity
verifies: 1,800 files per copy, 94,395,667 bytes, and 1,650 Git files.

| Quantity | Largest observed discrepancy | Original bound |
|---|---:|---:|
| Reference state | 2.368e-12 | 1e-8 |
| Reference stress | 4.097e-7 Pa | 0.01 Pa |
| Native/reference state | 2.138e-7 | 1e-5 |
| Native/reference stress | 0.03696 Pa | 1 Pa |

The six saved analytic traces agree with independently reconstructed closed-form
expectations. Their largest state error is 1.163e-11 against 1e-9. The saved
Jacobian discrepancy is 8.679e-9 against 2e-5. Accepted reference nodes and sampled
native/reference states preserve the required population/accounting bounds.

`artifacts/spindle-fixed-review-20261116/review.json` is D: verified. This supports
the declared fixed-length mechanics only. Moving/slack mechanics, sensory
membrane firing, physiological calibration, live sensory return and Avatar
recovery remain unqualified.

## Observed neuronal/native body `bb10dc`

All 160 focused cases pass with the exact expected inventory. Full C:/D: byte
verification covers 89,458 files per copy (888,870,070 bytes); the frozen Git
archive, probe/player binaries, 36 compiled Unity source files and build metadata
verify too. Both saved source and coupled native/reference analyses reproduce
exactly, with no test or model rerun.

Five original six-second source conditions retain their exact traces. After
1,000 ms of source priming, seven native conditions cover 14,336 actual physics
steps and 14 delivered motor events. The coupled body interval is approximately
128 ms per condition. The stimulated reference cells produce their own threshold
crossings under somatic current injection; these events drive the muscle model
and actual Unity hinge. Packet cadence and retry contrasts preserve state
bitwise, and the coupled source prefixes exactly match the original traces.

All twelve independent references and the candidate comparisons pass their
original criteria. Selected largest candidate/reference discrepancies are:

| Quantity | Largest observed discrepancy | Original bound |
|---|---:|---:|
| Joint angle | 1.138e-8 rad | 1e-5 rad |
| Joint speed | 2.507e-6 rad/s | 1e-5 rad/s |
| Muscle force | 4.488e-5 N | 0.01 N |
| Fibre length | 1.346e-8 m | 5e-7 m |

`artifacts/observed-native-body-review-20261116/review.json` records the complete
metrics and has a verified D: copy. The reviewer initially assumed exactly
128 ms when reporting duration. It now checks all seven final native clocks
against the recorded binary32 step: 128.00000607967377 ms. The original coupled
comparison already used this actual clock; no acceptance bound changed.

This establishes source causality and numerical agreement for the declared
fixture. It combines a reference cat motor cell, human muscle-unit parameters
and a synthetic hinge. It does not establish live DNNE authority, sensory
closure, physiological calibration, whole-body balance or recovery. Physical
step refinement for this particular input is also not assessed. The newer
receptor extension changes the membrane source file and has its own gate;
this older frozen result does not qualify that extension automatically.

## Next execution

The 111-case receptor/membrane/source gate was prepared from the verified
receptor review and launched in a visible independent PowerShell window:

- Package: `receptor-membrane-20261116-f2c9a7`.
- Frozen revision: `2453007fad2c1acdfff476fb7a70dc2ff46f5d2f`.
- Launch task: `DNNE-Receptor-Membrane-5ad21bf3811c`.
- Initial D: backup: 1,712 files plus manifest, verified before launch.

It runs the 111 focused cases followed by all five full original source
conditions. There is no live monitoring or post-launch result inspection.
This is the only pending operator completion report. Its successful reported
review is required before the 18 raw candidate, 36 active source/Avatar receiver
and 18 independent-reference comparison can run. That comparison has not been
launched. Existing bounds and saved states remain unchanged.
