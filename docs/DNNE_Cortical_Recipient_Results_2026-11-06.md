# Isolated cortical recipient result — 6 November 2026

The operator reported `cortical-memory-20261106-2be2a4` complete. Frozen source
`aeb40be364e2e44dd4d83e47f9a9641d326c6765` completed at 12:52:01 UTC.
All ten selected tests passed: eight recipient tests, the solution manifest and
the original consolidation assertion. This is not a full-suite result. The
unchanged original assertion has failed on earlier invocations; its random input
identities were not retained for replay. One pass does not repair that variability.

`tools/review-cortical-memory.py` checked all 1,778 files (372,169,487 bytes) in
each C:/D: capture, 1,298 packaged files, frozen binaries, test identities and raw
contact/event evidence. Its report is
`artifacts/cortical-recipient-review-20261106/review.json`. The seven conditions
completed, all five previous controls reproduced, and observation did not alter
the retained state/output payloads.

## Recipient evidence

Both new cases use continuous 495 ms integration with no late input delivery.
All 126 declared modulatory contacts reach the specified recipient. The cue
contact remains on cell 166.

| Measurement at cue cell 166 | Modulators on 166 | Modulators on neighbour 165 |
|---|---:|---:|
| Maximum normalized dopamine / ACh | 1 / 1 | 0 / 0 |
| Recorded local modulation updates | 489 | 0 |
| Cue presynaptic / postsynaptic updates | 108 / 57 | 108 / 37 |
| First measured cue weight | 1.0035418 | 1.0033692 |
| Final cue weight | 1.0066861 | 1.0032777 |
| Net weight change attributed to modulation updates | 0.00011372 | 0 |

This supports correct recipient delivery and exclusion from the neighbouring
cue, rather than population-wide broadcasting. The neighbouring condition has
the same cue weight history as the original continuous control. Normalized
modulator values are not measured biological concentrations.

The colocated cue gained 0.0066861 from its initialized weight of 1. Its recorded
changes sum to -0.00138676 at presynaptic updates, +0.00795914 at postsynaptic
updates and +0.00011372 at modulation updates. More postsynaptic firing also
occurred. Consequently the whole improvement cannot be attributed solely to
direct modulatory tag capture; altered firing/pairing and budget competition
also participate. The ensemble consolidation score remains derived bookkeeping,
not an independently measured biochemical memory state.

## Decision

Retain the explicit-recipient implementation as an isolated diagnostic. No live
host selects it. Next cortical work needs deterministic identities for the old
assertion, matched event histories to separate direct modulation from firing,
and receptor/arbor and retention/interference evidence. Do not broadcast ACh/DA,
boost learning gain or enforce monotonic scores to obtain a pass. Biological
consolidation, whole-brain learning and Avatar recovery remain unqualified.
