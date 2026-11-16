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

Its saved completion report declares 160 passing tests, seven physical cases,
14,336 native steps and successful independent comparisons. The full review of
its 89,458-file evidence set is in progress; these are not yet promoted to a
verified result in this document. The receptor gate does not depend on this
new body's result: it uses the already reviewed original source capture.

## Next execution

The 111-case receptor/membrane/source gate is ready to prepare from the verified
receptor review. It will run in a visible independent PowerShell window, with
no live monitoring. Its successful operator-reported review is required before
the 18 raw candidate, 36 active source/Avatar receiver and 18 independent-reference
comparison can run. Existing bounds and saved states remain unchanged.
