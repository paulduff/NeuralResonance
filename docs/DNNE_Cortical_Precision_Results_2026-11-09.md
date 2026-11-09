# Cortical precision integration: completed numerical checks

The operator reported `cortical-precision-20261109-bae439` complete. Frozen source
is `5e2c3a55c021b25c5745190ebce4dc0e88f9db25`, completed 12:22:30 UTC. The review
verifies all 2,097 files (689,013,214 bytes) in both C: and D: copies, matching
manifests, 1,450 frozen package files and both executed binary manifests.
`tools/review-cortical-precision-20261109.py` re-evaluates every saved contact
transition and reproduces the complete analysis without running the model again.
The review artifact is `artifacts/cortical-precision-review-20261109/review.json`.

All 68 focused cases pass, including the corrected conduction-aware fixture and
all ten determinism tests. The seven old cortical controls reproduce their saved
traces exactly after excluding generated message UUIDs. The first failed capture
remains preserved; its premature 1 ms assertion was a fixture error.

## Actual-cell evidence

The six precise scenarios contain 2,613 learning events and 840 receptor
deliveries. Every postsynaptic update matches an actual observed cell firing.
The full cortical population is retained, but only two recurrent precise contacts
are exercised in these particular new fixtures; do not generalize this to dense
whole-cortex integration. The original legacy controls exercise their own larger
recurrent contact populations and remain unchanged.

| Scenario | Target firings | Precise contacts | Final external cue weight |
| --- | ---: | ---: | ---: |
| Legacy paired | 40 | 0 | legacy float path |
| Precise paired | 40 | 3 | 1.0015365297469205 |
| Neighbor modulation | 38 | 3 | 1.0013805887197986 |
| No modulation | 38 | 3 | 1.0013805887197986 |
| STDP alone | 40 | 3 | 1.0012805887220335 |
| Capture alone | 39 | 3 | 1.0001593599349700 |
| Synthetic cue | 39 | 2 | no precise synthetic contact |

Thirteen paired-case and thirteen capture-only updates change the double weight
while leaving its float mirror unchanged. All 26 are modulation updates. Saved
double contact state matches the final accepted state exactly, and all six
explicit warm starts preserve learned efficacy/budget while clearing transient
chemistry and acquiring a new clock origin. The independent recorded-state
transition calculation reports zero discrepancy at the saved precision; maximum
receptor gain discrepancy is 7.106e-15, within the prospective 1e-11 limit.

Neighboring and absent modulation give the same external cue weight and no local
capture contribution. Paired and capture-only runs each record a signed cue
capture contribution of 0.0001593599349700714. Those are measurements of the
implemented reduced rule, not fitted biological quantities. Different rule
conditions can change firing and subsequent history, so whole-trajectory weight
differences must not be treated as fixed-history causal estimates.

## Decision and remaining work

The isolated contact-to-receptor-to-storage precision integration passes its
declared checks. Preserve it as the numerical foundation for further cortical
learning work. The live host still uses schema 4; no automatic migration or live
promotion occurred. Schema 5 remains an explicitly isolated learned warm start,
not a membrane/event checkpoint.

The provisional rate budget, tagging/capture approximation and receptor/cell
calibration still require biological review. The original aggregate consolidation
assertion and full unit suite are not qualified by this focused window. Neither
physiology nor Avatar recovery has been demonstrated. The next active numerical
repair is the separate Golgi integration-order/timing failure; none of its failed
criteria is changed by this cortical result.
