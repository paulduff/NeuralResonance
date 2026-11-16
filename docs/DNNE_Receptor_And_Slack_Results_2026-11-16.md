# Receptor gate and active spindle force review

The operator reported both `f2c9a7` and `527839` complete. Their saved evidence
was then inspected; neither was monitored. Reviews execute no tests, kinetics
or new root search and preserve the original capture directories.

## Receptor gate passed

All 111 declared method/cases pass, and all five full original neuronal source
conditions reproduce their saved analysis exactly. Source identity, original
crossings, packet cadence and delivery accounting pass. The complete C:/D:
capture verifies 2,282 files per copy, 1,679 frozen Git files and the executed
test/probe binaries. Before/after managed-runtime fingerprints agree.

`artifacts/receptor-membrane-review-20261116` holds the review and managed-runtime
record; both files and their manifest are D: verified. The qualified probe has
20 files and is retained unchanged for the next comparison. No current C# or
build input changed after the gate. This qualifies the component controls and
preserved old source behavior, not active coupled receptor/membrane accuracy,
physiology, live brain control or Avatar recovery.

The next numerical package is **`receptor-membrane-comparison-20261116-f7ece6`**,
frozen at `ade8c0252749a204dee4715cb88ab850d1492fb5`. Its 1,729 initial files plus
manifest were D: verified before launch. The independent visible task is
`DNNE-Receptor-Comparison-3fc1a1c87fae`. It runs 18 raw receptor/membrane cases,
36 active source/Avatar receiver cases and 18 independent reference solves with
the previously declared controls and bounds. No post-launch result or process
has been inspected. Await the operator's report.

## Spindle failure diagnosed

The six analytic controls passed. Seven entire trajectories and 49 states of
the eighth were processed: **1,556 of 2,812 states**, with the final state
failing the root-count comparison. Full C:/D: evidence verifies 1,767 files
per copy, 80,765,915 bytes and 1,684 frozen Git files. The environment is unchanged.
The failed capture and its original status remain preserved.

At step 48 of `chain-stretch-shorten-500us`, the candidate counted:

- A knot at 1,200 nm, whose nonzero residual is about -1.185e-9 Pa.
- An adjacent interval root at 1,200.0000000000048 nm.
- Two distinct farther roots near 1,207.256586 and 1,242.201018 nm.

The independent checker retained only three entries because its proximity rule
merged the first pair. Candidate and reference disagreed about counting; force
residuals and force reconstruction passed their existing 1e-7 Pa limits.
The source cause is explicit: the candidate treated a small knot residual as
a root, whereas the independent checker merged nearby interval roots into it.
An accuracy tolerance does not establish that a knot is an additional zero.

The saved partial records contain multiple-root and zero-root cases. These
counts are provisional pending corrected full enumeration. The distinct farther
solutions already make clear why an arbitrary scalar-search root cannot be
promoted to a physiological slack update. No root is selected for live control.

`artifacts/spindle-slack-review-20261116/review.json` is D: verified. It checks
the saved controls, records, histograms and failure/source correspondence without
repeating searches. It records the full matrix as unqualified.

## Root identification repair

Version 2 separates root existence from numerical residual acceptance. Near a
zero-valued boundary, exact rational arithmetic over the stored binary inputs
certifies the candidate's affine interval and knot value. A nonzero knot remains
an explicit diagnostic probe, not an additional root. A true interval root that
rounds onto a knot remains associated with its open interval. Distinct roots
are never merged merely because their distance is below 1e-9 nm.

The independent path reconstructs an affine segment from two exact interior
force queries when endpoint cancellation requires it. The ordinary bracketed
Brent path and independent interpolation checks remain in use elsewhere. Exact
knot semantics and the source partition are shared; affine coefficients and the
reference interpolation construction are separate. All original acceptance
bounds are unchanged.

The next visible run has **eight controls**: the original six, the preserved
step-48 regression, and an analytic case with two distinct roots on opposite
sides of a knot within 1e-9 nm. The latter prevents a false repair by blanket
deduplication. It then repeats all 2,812 force queries from immutable recorded
states, without rerunning kinetics. The preparer requires the D:-verified failed
review and original failure record. No repaired test or root query runs inline.

## Replacement launch

The repaired source and rig pass syntax parsing; the acceptance dictionary is
statically identical to the failed version. Seven authoring files plus manifest
are D: verified in `spindle-knot-repair-authoring-20261116`. These are authoring
checks only; the eight controls and full query matrix remain prospective.

`spindle-slack-roots-20261116-ba3c0c` is frozen at
`e09d37db87b91a0e8df43f6db4fd66124be9228c`, with 1,746 initial files plus manifest
D: verified before launch. Its visible task is `DNNE-Spindle-Slack-2bb1c5df9d5a`.
No post-launch output or process was inspected. The only two pending operator
reports are this replacement and receptor comparison `f7ece6`. Keep both
unmonitored until their respective completion reports arrive.

## Replacement completed and reviewed

The operator subsequently reported `ba3c0c` complete. Saved-data review verifies
all **1,777 files per C:/D: copy**, 90,016,597 bytes, 1,745 protected package files
and 1,687 frozen Git files. Numerical dependency fingerprints agree before,
after and at review. All eight controls pass, including the original step-48
failure and the separate two-close-roots control. Every record in all twelve
traces reconciles with the saved analysis: **2,812 queries pass**. The review
does not rerun root searches, controls, kinetics or trajectories.

The largest force residual is 9.8062e-9 Pa against the original 1e-7 Pa bound;
the largest candidate/reference force difference is 4.6566e-10 Pa. Root
locations differ by at most 2.2737e-13 nm against the original 1e-9 nm bound.
No acceptance bound changed. The repaired enumeration is qualified for these
recorded states and the pinned finite-grid force rule.

| Zero-force solutions per saved state | States |
|---|---:|
| None | 31 |
| One | 418 |
| More than one | 2,363 |

All enumerated roots meet the declared population/domain checks. That is
algebraic admissibility, not evidence of dynamical stability or accessibility
from the previous state. Among the 699 states with negative original imposed
stress, 137 have one root, 534 have multiple roots and 28 have none. These
figures include three time resolutions of the same protocols; they are not
independent biological samples or estimates of real spindle failure rates.

All 31 no-root states contain a sign-changing force discontinuity. For example,
`bag-imposed-below-slack-1000us`, step 36, has no zero-force root in the declared
open domain. At trial length 1,069 nm the recorded force and left limit are
approximately -1,060.138 Pa, while the right limit is +1,137.633 Pa. A bracketing
solver must not report that jump as a zero. The pure trial helper uses linear
interpolation with zero outside the finite distribution support; occupied edge
bins therefore explain the recorded discontinuities. The independent reference
shares this source convention, so agreement does not validate that convention
as physiological mechanics.

Multiple roots also occur without any significant jump: at step 2 of
`bag-stretch-shorten-1000us`, three roots are approximately 1,050,
1,209.740813 and 1,222.376251 nm. The passive-tail solution retains no bound
cross-bridges; the others retain different bound populations. A local slope or
nearest-root choice alone is not a validated rule for advancing the muscle.

The next mechanical investigation should isolate distribution translation at
finite support and its effects on population conservation, force continuity and
unloading history. Compare any proposed conservative transport/boundary treatment
against analytic translation controls and increasing bin range/resolution before
coupling it to kinetics. Preserve the original rule and its qualified reference
as a baseline. Do not clip negative stress, relax bounds, discard roots or install
an arbitrary selected root in Avatar. A later slack trajectory must additionally
respect imposed geometry and previous mechanical state.

`tools/review-spindle-slack-v2-20261116.py` reproduces the saved-record review.
`artifacts/spindle-slack-v2-review-20261116/review.json` preserves its provenance,
counts, all no-root examples and one multiple-root example per trace. The review
is separately D: backed. Moving/slack trajectories, sensory firing, physiological
calibration and Avatar recovery remain unqualified.

Only **receptor comparison `f7ece6`** now awaits an operator completion report.
Its outputs and process remain uninspected. No further experiment was launched
as part of this completed-run review.
