# Completed sensory and neuronal motor comparisons

Both completion reports were received from the operator. No active run was read.
Saved-data review code: `tools/review-coupled-outcomes-20261118.py`.

## Sensory trajectories: 87562a

`artifacts/coupled-sensory-review-20261118` verifies all 2,818 files per C:/D: copy,
frozen Git source, test/probe binaries, unchanged managed/numerical dependencies,
and 120 component cases. The entire saved trajectory analysis was reproduced
using saved arrays and arithmetic, without evaluating membrane RHS equations,
integrating any model, or rerunning tests. Its two-file review has a verified D:
copy. The original FAILED result is preserved.

All seven conditions pass independent-reference agreement, sampled halving
behavior and charge/ion conservation. Six also pass finest-step accuracy. The
eight-patch condition fails only finest-step accuracy: normalized maximum errors
are `24.518246236227327`, `6.045237870445926`, `1.5018732660967322` at
0.002/0.001/0.0005 ms. The roughly fourfold decrease supports testing a smaller
step; it does not qualify an unmeasured resolution.

The worst finest-step error is cumulative apex charge at 0.01 ms:
`0.000322498945592109 fC` against `0.00021473113136254306 fC` allowed (including
the original relative term). Candidate `7.365888180217122 fC`, tight reference
`7.36556568127153 fC`. This is an initial-transient accuracy failure, while
conservation passes; do not erase it by changing initialization or bounds.

## Neuronal/native motor comparison: f5e01c

`artifacts/coupled-motor-review-20261118` verifies all 1,922 continuation files
per C:/D: copy and frozen Git source. The comparison's complete before/after
audit of the original 360,428-file native capture, 281 tests and binaries is
retained. This review rechecks the original manifests and the 21 native
initial/final/step-record files on both disks; it does not claim to repeat that
larger full audit. It re-parses every saved body/muscle endpoint and exactly
reproduces all comparison errors against the 15 completed independent references.
The one-file review has a verified D: copy. No ODE, neuronal model, physics or
test was rerun.

Quiet, unit-left and strong-cut pass. Strong-left/right and their packet/retry
controls fail only `yieldFraction`: maximum `1.039360238175302e-5` versus `1e-5`
allowed, an excess of about 3.94%. All three reference methods reproduce it.
The peak occurs at `264.937512583856 ms` in the **opposite unstimulated muscle**
(muscle 1 for left drive, muscle 0 for right drive). Source/body accounting,
opposite movement and packet/retry equality remain supported by the completed
native audit; sampled coupled accuracy still fails.

Current `AvatarCausalMotorPlant` advances the body using the previous tendon
force, then advances muscles with the previous path length and observes the
completed path afterward. Its first-order body/muscle partition is a plausible
source of the moving passive-muscle discrepancy. This is a hypothesis, not an
attribution. The next motor experiment should separately refine physical step
and muscle substeps under identical original neuronal inputs, compare common
endpoints and preserve the original limits. Do not alter biological yield
parameters to fit a numerical error. No new motor study is launched here.

## Finer sensory study

`cleft_refinement_gate.py` freezes the reviewed references and unchanged channel,
transport, integrator and earlier test source. The capture supports an explicit
second protocol, with 4,000 ticks/ms instead of 2,000; common samples and held
input times are identical in physical units. All seven conditions are captured
at 0.001/0.0005/0.00025 ms. The first two overlap the prior run and must reproduce
all saved error/ledger/fingerprint/work metrics exactly (tick integers differ).

All 21 completed Radau/BDF references are copied and hashed into the new package,
not solved again. The analysis rechecks their accepted nodes, common samples,
saved precision witnesses and accounting, then applies the **original** accuracy,
refinement and conservation limits. Every new candidate endpoint is retained.
The 132 focused component cases include the twelve stationary-search controls;
the actual trajectory initialization is unchanged and does not use that search.

Authoring builds and Python/PowerShell/static inventory checks pass. Numerical
execution remains exclusively in the visible independent PowerShell runner,
with frozen source, before/after dependency checks and final verified D: backup.
This is accuracy work, not a speed benchmark. Stationary stability, physiological
calibration, afferent firing, live sensory return and Avatar balance remain open.

Finer sensory study launched as `cleft-refinement-20261118-884934`, frozen at
pushed revision `fb61de2fd0af6b2ea01ec9b136c5e96fa0873415`. Initial 2,185 files
plus manifest verified on D:. Visible task `DNNE-Cleft-Refinement-96fcdeb342d8`.
This ONE window runs 132 component cases (including stationary-search controls)
and 21 native trajectories against copied saved references, with no reference
ODE reruns. No post-launch outputs, status or processes have been inspected.
Await operator completion. No other study is pending: sensory `87562a` and rubral
`f5e01c` are reviewed failures with original evidence preserved. No separate
stationary or motor window launched. Full goal remains active.

## Finer sensory result verified

The operator reported `cleft-refinement-20261118-884934` complete. Review
`artifacts/coupled-finer-review-20261118` verifies all 2,820 files on both C: and
D:, frozen Git source/test/probe binaries, exact prior and unchanged runtime, and
all 132 focused cases (including all twelve stationary controls). Recomputing
only saved-array/accounting comparisons exactly reproduces the final analysis.
The two-file review also has a verified D: copy.

All seven numerical conditions pass at the finest 0.00025 ms step. Worst allowed
fraction is `0.37435413377613114` for eight patches, down from
`1.5018732660967322` at 0.0005 ms. All repeated overlapping metrics, independent
reference, sampled refinement and ledger criteria pass unchanged. No fresh
reference solve was needed. This confirms the diagnosed finite-step error for
these sampled trajectories, not active physiological rest, afferent firing or
balance. All previous studies are now reviewed; `884934` is no longer pending.
