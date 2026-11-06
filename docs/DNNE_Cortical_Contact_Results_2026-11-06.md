# Cortical contact replay result — 6 November 2026

The operator reported `cortical-contact-20261106-59be07` complete. Frozen source
`10228ade65624aa3741c745b2d284a46d52a5762` completed at 13:23:39 UTC with
`DIAGNOSTICS_COMPLETE`. Four recorded histories reproduced every contact field
exactly; all sixteen interventions and twenty-seven cadence fixtures completed.

`tools/review-cortical-contact.py` checked all 1,451 files (65,434,243 bytes) per
C:/D: capture, 1,311 packaged files, frozen binaries, original selected line
provenance and saved event/weight accounting without running learning. Report:
`artifacts/cortical-contact-review-20261106/review.json`.

## Direct modulation under fixed activity

With the colocated history, final weight was 1.0066861, versus 1.0066315 with
capture disabled and 1.0066484 with dopamine removed only from the capture
calculation. Thus direct capture contributes about 0.0000546 to final weight
under those fixed spikes, including its later interactions with weight dependence
and the existing limiter. This is smaller than the 0.0034084 difference between
the two original network histories, whose firing also differs. It is not a
general causal percentage decomposition or a calibrated pharmacological effect.

Neighbour, original simultaneous and unmodulated histories are invariant to these
interventions because no modulation calls occur at their cue. No-ACh and
NoTagCapture results match, as required by the current reduced rule.

There were 121 colocated updates that charged the limiter but did not change the
stored float weight, totalling 1.20149394e-6 charged units. Exact stored-float
arithmetic gives 0.0001131892204284668 net change on modulation calls. Earlier
summaries subtracted decimal JSON spellings, yielding approximately 0.00011372;
the replay reconstructs float values and is the appropriate numerical accounting
source. Neither value is a measured molecular concentration or biological energy.

## Numerical defect

For the same one-second positive-tag fixture, the continuous limit of the
declared unclipped rule is approximately 1.0000574711:

| Step (ms) | Stored final weight | Limiter charge |
|---:|---:|---:|
| 4 | 1.0000596 | 0.0000574567 |
| 1 | 1.0000242 | 0.0000574676 |
| 0.5 | 1.0000000 | 0.0000574697 |
| 0.0625 | 1.0000000 | 0.0000574716 |
| 0.03125 | 1.0000000 | 0 |

Negative-tag learning is already absent at 2 ms, although the continuous limit
is 0.9999861545; at 0.125 ms its limiter charge also disappears. Unpaired contacts
stay unchanged throughout.

The source explains two mechanisms: adding scaled increments directly to a float
weight rounds small changes away, and `ApplyCadenceInvariantBudget` rejects raw
increments at or below 1e-7. Trace state is also repeatedly rounded to float.
Increasing temporal resolution therefore does not converge to the declared
continuous learning model in this domain. This is a numerical defect, not evidence
that biology rejects weak learning or needs stronger stimulation. The checked
1/2/4 ms dopamine unit fixture used a much larger eligibility trace and did not
cover this low-amplitude tag-capture regime.

## Decision

Test an isolated double-state implementation with the same represented parameter
values, timing law and limiter, without the update-size cutoff or artificial
trace truncation. Keep actual event identities and local signals. First reproduce
this whole legacy baseline, then require convergence against closed forms for
both signs and blank controls. Preserve this original outcome and its numerical
limits. No gain increase or live learning change is justified by this result.
The limiter is still an uncalibrated reduction; receptor dynamics, retention,
interference, live integration and Avatar recovery remain separate requirements.
